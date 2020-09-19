using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

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
                    services.Configure<OsnovaOptions>(ctx.Configuration.GetSection("Osnova"));
                    services.Configure<MongoOptions>(ctx.Configuration.GetSection("Mongo"));
                    services.Configure<HealthchecksOptions>(ctx.Configuration.GetSection("Healthchecks"));
                    services.AddSingleton<OsnovaClient>();
                    services.AddSingleton<IBotUserRepository, MongoBotUserRepository>();
                    services.AddSingleton<IRepostRequestRepository, MongoRepostRequestRepository>();
                    services.AddSingleton<ICreatorCardRepository, MongoCreatorCardRepository>();
                    services.AddHostedService<DtfMessageBotService>();
                    services.AddHostedService<HealthcheckMessageSenderService>();
                })
                .RunConsoleAsync();
        }
    }
}

