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
                    services.AddSingleton<DtfMessageBotService>();
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

        private async Task CreateUser(User user, IMongoCollection<User> UserCollection)
        {
            //await File.WriteAllTextAsync(ResolveAbsolutePath("users/" + user.id + ".json"), JsonSerializer.Serialize(user));
            await UserCollection.InsertOneAsync(user);
        }

        private async Task UpdateUser(User user, IMongoCollection<User> UserCollection)
        {
            var filter = Builders<User>.Filter.Eq("id", user.id);
            //await File.WriteAllTextAsync(ResolveAbsolutePath("users/" + user.id + ".json"), JsonSerializer.Serialize(user));
            await UserCollection.ReplaceOneAsync(filter, user, new ReplaceOptions { IsUpsert = true });
        }

        private async Task<User> LoadUser(string id, IMongoCollection<User> UserCollection)
        {
            var filter = Builders<User>.Filter.Eq("id", id);
            //return JsonSerializer.Deserialize<User>(await File.ReadAllTextAsync(ResolveAbsolutePath("users/" + id + ".json")));
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

        protected override async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Бот для мессенджера Очобы\nСделано долбоёбом Neko Natum");
            _logger.LogInformation("Поiхалi");

            EnsureUsersDirectoryExists();
            // TODO вынести куда-нибудь это нахуй или сделать кошерней
            var client = new MongoClient(_mongoConnectionString);
            var db = client.GetDatabase("messagebot");
            var usersCollection = db.GetCollection<User>("Users");

            _logger.LogInformation("Знакомых пользователей: {0}", await usersCollection.EstimatedDocumentCountAsync());

            List<User> activeUsers = new List<User>();
            do
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
                                if (!IsUserExists(chan.id, usersCollection) /*!File.Exists(ResolveAbsolutePath("users/" + chan.id + ".json"))*/)
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
                                    _logger.LogInformation("Создан новый пользователь с id = {0}", chan.id);
                                }
                                else
                                {
                                    activeUsers.Add(await LoadUser(chan.id, usersCollection));
                                    _logger.LogInformation("Подключился пользователь с id = {0}", chan.id);
                                }
                                currentActive = activeUsers.Count - 1;
                            }
                            else
                            {
                                currentActive = activeUsers.FindIndex(x => string.Equals(x.id, chan.id));
                            }
                            activeUsers.ElementAt(currentActive).UpdateUser(chan);
                            //_logger.LogInformation(chan.lastMessage.text);
                            //_logger.LogInformation(activeUsers.ElementAt(currentActive).lastMessage);
                            activeUsers.ElementAt(currentActive).Actions(_osnova, db);
                            //await UpdateUser(activeUsers.ElementAt(currentActive), UsersCollection); //хз насколько правильно долбить базу после каждого чиха, но пусть будет
                        }
                    }
                }
                else if (_osnova.LastStatus == 0)
                {
                    await Task.Delay(1000);
                }
                else if (_osnova.LastStatus == -1)
                {
                    _logger.LogError("Произошла ошибка сети, бот будет остановлен");
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

