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
                    services.AddSingleton<DtfMessageBotService>();
                    services.AddHostedService<DtfMessageBotService>();
                })
                .RunConsoleAsync();
        }
    }

    internal class DtfMessageBotService : ContinuousHostedService
    {
        private readonly ILogger<DtfMessageBotService> _logger;

        public DtfMessageBotService(
            ILogger<DtfMessageBotService> logger,
            IHostApplicationLifetime host) : base(host)
        {
            _logger = logger;
        }

        protected override async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Бот для мессенджера Очобы");
            _logger.LogInformation("Сделано долбоёбом Neko Natum");
            _logger.LogInformation("Поiхалi");
            BotConfig config = new BotConfig();
            MessageData data = new MessageData();
            Network worker = new Network();
            List<User> activeUsers = new List<User>();
            config.readConfig();
            worker.setupNetworkToken(config.site, config.version, config.token);

            do
            {
                worker.Listen();
                if (worker.LastStatus > 0)
                {
                    data = worker.requestChannelsData();
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
                                    activeUsers.Last().SaveUserJson();
                                    _logger.LogInformation("Создан новый пользователь с id = {0}", chan.id);
                                }
                                else
                                {
                                    activeUsers.Add(JsonSerializer.Deserialize<User>(File.ReadAllText("users/" + chan.id + ".json")));
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
                            activeUsers.ElementAt(currentActive).Actions(worker);
                        }
                    }
                }
                else if (worker.LastStatus == 0)
                {
                    await Task.Delay(1000);
                }
                else if (worker.LastStatus == -1)
                {
                    _logger.LogError("Произошла ошибка сети, бот будет остановлен");
                }
            }
            while (!cancellationToken.IsCancellationRequested && worker.LastStatus >= 0);

            foreach (User user in activeUsers)
            {
                user.SaveUserJson();
            }
        }
    }
}

