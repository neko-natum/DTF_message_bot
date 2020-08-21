using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
                    configuration.AddEnvironmentVariables("DTFMB__");
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

        public DtfMessageBotService(
            OsnovaClient osnova,
            IOptions<PersistentStateOptions> storageOptionsAccessor,
            ILogger<DtfMessageBotService> logger,
            IHostApplicationLifetime host) : base(host)
        {
            _osnova = osnova;
            _logger = logger;
            _stateDir = storageOptionsAccessor.Value.Directory;
        }

        private string ResolveAbsolutePath(string relativePath) => Path.Combine(_stateDir, relativePath);

        private async Task SaveUser(User user)
        {
            await File.WriteAllTextAsync(ResolveAbsolutePath("users/" + user.id + ".json"), JsonSerializer.Serialize(user));
        }

        private async Task<User> LoadUser(string id)
        {
            return JsonSerializer.Deserialize<User>(await File.ReadAllTextAsync(ResolveAbsolutePath("users/" + id + ".json")));
        }

        protected override async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Бот для мессенджера Очобы\nСделано долбоёбом Neko Natum");
            _logger.LogInformation("Поiхалi");
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
                                if (!File.Exists("users/" + chan.id + ".json"))
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
                                        Description = null,
                                        links = null,
                                        tags = null
                                    });
                                    await SaveUser(activeUsers.Last());
                                    _logger.LogInformation("Создан новый пользователь с id = {0}", chan.id);
                                }
                                else
                                {
                                    activeUsers.Add(await LoadUser(chan.id));
                                    _logger.LogInformation("Подключился пользователь с id = {0}", chan.id);
                                }
                                currentActive = activeUsers.Count - 1;
                            }
                            else
                            {
                                currentActive = activeUsers.FindIndex(x => string.Equals(x.id, chan.id));
                            }
                            activeUsers.ElementAt(currentActive).UpdateUser(chan);
                            _logger.LogInformation(chan.lastMessage.text);
                            _logger.LogInformation(activeUsers.ElementAt(currentActive).lastMessage);
                            activeUsers.ElementAt(currentActive).Actions(_osnova);
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
                await SaveUser(user);
            }
        }
    }
}

