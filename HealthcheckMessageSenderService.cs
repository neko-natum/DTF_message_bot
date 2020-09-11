using DnsClient.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DTF_message_bot
{
    internal class HealthcheckMessageSenderService : ContinuousHostedService
    {
        private readonly bool _hcEnabled;
        private readonly double _hcIntervalMinutes;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly MultipartFormDataContent _requestContent = new MultipartFormDataContent();

        public HealthcheckMessageSenderService(
            IOptions<HealthchecksOptions> optionsAccessor,
            IOptions<OsnovaOptions> osnovaOptionsAccessor,
            IHostApplicationLifetime host,
            ILogger<ContinuousHostedService> baseLogger) : base(host, baseLogger)
        {
            var osnovaOptions = osnovaOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(osnovaOptionsAccessor));
            var hcOptions = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));

            if (!(_hcEnabled = hcOptions.HealthchecksEnabled ?? false))
            {
                return;
            }

            _hcIntervalMinutes = hcOptions.HealthcheckIntervalInMinutes.Value;

            _httpClient.BaseAddress = new Uri("https://api." + osnovaOptions.Host + ".ru/" + osnovaOptions.Version + "/");
            _httpClient.DefaultRequestHeaders.Add("X-Device-Token", hcOptions.SenderAccountToken);

            foreach (var (k, v) in new[]
                {
                    ("channelId", hcOptions.BotSelfId.Value.ToString(CultureInfo.InvariantCulture)),
                    ("text", hcOptions.HealthcheckMessage),
                    ("ts", "1"),
                    ("idTmp", "1"),
                    ("media", "[]")
                })
            {
                _requestContent.Add(new StringContent(v), '"' + k + '"');
            }
        }

        protected override Task<bool> OnBeforeStartAsync() => Task.FromResult(_hcEnabled);

        protected override async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(_hcIntervalMinutes), cancellationToken);
                await Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(
                        new[] { 1, 2, 4 }
                        .Cast<double>()
                        .Select(TimeSpan.FromSeconds))
                    .ExecuteAsync(async () =>
                    {
                        using var response = await _httpClient.PostAsync("m/send", _requestContent);
                    });
            }
        }

        protected override Task OnAfterStopAsync()
        {
            _httpClient?.Dispose();
            return Task.CompletedTask;
        }
    }
}