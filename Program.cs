using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;

namespace DTF_message_bot
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            await Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(configuration =>
                {
                    configuration.AddJsonFile("appsettings.json");
                    configuration.AddJsonFile("appsettings.secrets.json", optional: true);
                    configuration.AddEnvironmentVariables("DTFMB_");
                    configuration.AddCommandLine(args);
                })
                .ConfigureLogging((ctx, logging) =>
                {
                    logging.AddConsole();
                    logging.AddConfiguration(ctx.Configuration.GetSection("Logging"));
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddOptions();
                    services.Configure<PersistentStateOptions>(ctx.Configuration.GetSection("PersistentState"));
                    services.Configure<OsnovaOptions>(ctx.Configuration.GetSection("Osnova"));
                    services.Configure<MongoOptions>(ctx.Configuration.GetSection("Mongo"));
                    services.AddTransient<OsnovaClient>();
                    services.AddHostedService<DtfMessageBotService>();
                })
                .RunConsoleAsync();
        }
    }

    internal class DtfMessageBotService : ContinuousHostedService
    {
        private readonly OsnovaClient _osnova;
        private readonly ILogger<DtfMessageBotService> _logger;
        private readonly string _stateDir;
        private readonly string _mongoConnectionString;

        public DtfMessageBotService(
            OsnovaClient osnova,
            IOptions<PersistentStateOptions> storageOptionsAccessor,
            IOptions<MongoOptions> mongoOptionsAccessor,
            ILogger<DtfMessageBotService> logger,
            IHostApplicationLifetime host) : base(host)
        {
            _osnova = osnova;
            _logger = logger;
            _stateDir = storageOptionsAccessor.Value.Directory;
            _mongoConnectionString = mongoOptionsAccessor.Value.ConnectionString;
        }

        private string ResolveAbsolutePath(string relativePath) => Path.Combine(_stateDir, relativePath);

        private async Task CreateUser(User user, IMongoCollection<User> UserCollection) //создание нового пользователя в бд
        {
            await UserCollection.InsertOneAsync(user);
        }

        private async Task UpdateUser(User user, IMongoCollection<User> UserCollection) //обновление данных пользователя в бд
        {
            var filter = Builders<User>.Filter.Eq("id", user.id);
            await UserCollection.ReplaceOneAsync(filter, user, new ReplaceOptions { IsUpsert = true });
        }

        private async Task<User> LoadUser(string id, IMongoCollection<User> UserCollection) //ищет в бд и отдаёт данные подключившегося пользователя
        {
            var filter = Builders<User>.Filter.Eq("id", id);
            var result = await UserCollection.Find(filter).ToListAsync();
            if (result.Count > 1) _logger.LogInformation("Что за хуйня? Почему больше одного ID?");
            return result.First();
        }
        
        private bool IsUserExists(string id, IMongoCollection<User> UserCollection)
        {
            var filter = Builders<User>.Filter.Eq("id", id);
            var result = UserCollection.Find(filter).ToList();
            return result.Any();
        }

        private void EnsureUsersDirectoryExists()
        {
            var dir = Path.Combine(_stateDir, "users");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        /*
         * Рабочий цикл бота
         * Здесь инициируется прослушка сокетов и, если сокеты как обычно лежат, спам запросами
         * а также вызов логики самого бота
         */
        protected override async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Bot for Osnova-based messenger\nStarted up!");
            bool firstRun = true; //первый прогон после запуска всегда прямым запросом чтобы отследить входящие до включения
            EnsureUsersDirectoryExists();
            var client = new MongoClient(_mongoConnectionString);
            var db = client.GetDatabase("messagebot");
            var usersCollection = db.GetCollection<User>("Users");

            _logger.LogInformation("Known users: {0}", await usersCollection.EstimatedDocumentCountAsync());
            await _osnova.StartAsync();
            List<User> activeUsers = new List<User>();
            do
            {
                if (_osnova.isConnected&&firstRun==false) //работа на сокетах
                {
                    int currentActive;
                    if (_osnova.socketTasks.TryDequeue(out var queuedUser))
                    {
                        //TODO вынести это в отдельную функцию чтобы не выглядеть как конченный дебил
                        if (!activeUsers.Exists(x => x.id == queuedUser.id))
                        {
                            if(!IsUserExists(queuedUser.id, usersCollection))
                            {
                                activeUsers.Add(new User()
                                {
                                    id = queuedUser.id,
                                    username = queuedUser.username,
                                    imagePath = queuedUser.imagePath,
                                    lastMessageTime = queuedUser.lastMessageTime,
                                    lastMessage = queuedUser.lastMessage,
                                    lastAction = UserActions.Undefined,
                                    isCardExists = false,
                                    isAdmin = false
                                });
                                await CreateUser(activeUsers.Last(), usersCollection);
                                _logger.LogInformation("New user with id = {0} was created", queuedUser.id);
                            }
                            else
                            {
                                activeUsers.Add(await LoadUser(queuedUser.id, usersCollection));
                                _logger.LogInformation("Connection of user with id = {0}", queuedUser.id);
                            }
                            currentActive = activeUsers.Count - 1;
                        }
                        else
                        {
                            currentActive = activeUsers.FindIndex(x => string.Equals(x.id, queuedUser.id));
                        }
                        activeUsers.ElementAt(currentActive).UpdateUser(queuedUser.id, queuedUser.username, queuedUser.imagePath, queuedUser.lastMessageTime, queuedUser.lastMessage);
                        _osnova.MarkAsRead(queuedUser.id);
                        string answer = activeUsers.ElementAt(currentActive).Actions(_osnova, db);
                        if (answer != "") _osnova.AnswerUser(queuedUser.id, answer);
                    }
                }
                else //работа на прямых запросах
                {
                    _osnova.Listen();
                    if (_osnova.LastStatus > 0)
                    {
                        var data = _osnova.RequestChannelsData();
                        foreach (Channels chan in data.result.channels)
                        {
                            if (chan.unreadCount != 0)
                            {
                                int currentActive;
                                if (!activeUsers.Exists(x => x.id == chan.id))
                                {
                                    if (!IsUserExists(chan.id, usersCollection))
                                    {
                                        activeUsers.Add(new User()
                                        {
                                            id = chan.id,
                                            username = chan.lastMessage.author.title,
                                            imagePath = chan.lastMessage.author.picture,
                                            lastMessageTime = chan.lastMessage.dtCreated,
                                            lastMessage = chan.lastMessage.text,
                                            lastAction = UserActions.Undefined,
                                            isCardExists = false,
                                            isAdmin = false
                                        });
                                        await CreateUser(activeUsers.Last(), usersCollection);
                                        _logger.LogInformation("New user with id = {0} was created", chan.id);
                                    }
                                    else
                                    {
                                        activeUsers.Add(await LoadUser(chan.id, usersCollection));
                                        _logger.LogInformation("Connection of user with id = {0}", chan.id);
                                    }
                                    currentActive = activeUsers.Count - 1;
                                }
                                else
                                {
                                    currentActive = activeUsers.FindIndex(x => string.Equals(x.id, chan.id));
                                }
                                activeUsers.ElementAt(currentActive).UpdateUser(chan.id, chan.lastMessage.author.title, chan.lastMessage.author.picture, chan.lastMessage.dtCreated, chan.lastMessage.text);
                                _osnova.MarkAsRead(chan.id);
                                string answer = activeUsers.ElementAt(currentActive).Actions(_osnova, db);
                                if (answer != "") _osnova.AnswerUser(chan.id, answer);
                            }
                        }
                    }
                    else if (_osnova.LastStatus == 0)
                    {
                        await Task.Delay(1000);
                    }
                    else if (_osnova.LastStatus == -1)
                    {
                        _logger.LogError("Network error. Shutdown.");
                    }
                    firstRun = false;
                }
                if (_osnova.isError)
                {
                    _logger.LogError("Error in sockets");
                    _osnova.isError = false;
                }
                //Производится выгрузка неактивных дольше часа из списка
                var selectedUsers = from user in activeUsers
                                    where (double)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds - user.lastMessageTime > 3600
                                    select activeUsers.IndexOf(user);
                if (selectedUsers.Any())
                {
                    foreach (int del in selectedUsers)
                    {
                        _logger.LogInformation("User {0} was removed from active memory due to inactivity", activeUsers.ElementAt(del).id);
                        await UpdateUser(activeUsers.ElementAt(del), usersCollection);
                        activeUsers.RemoveAt(del);
                    }
                }
            }
            while (!cancellationToken.IsCancellationRequested && _osnova.LastStatus >= 0);

            foreach (User user in activeUsers)
            {
                await UpdateUser(user, usersCollection);
            }
        }
    }
}

