using System.Threading;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Globalization;

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
            IHostApplicationLifetime host) : base(host)
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

        protected override Task<bool> OnBeforeStartAsync()
        {
            // sender will not start if there is no healthcheck configured
            return Task.FromResult(_hcEnabled);
        }

        protected override async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(_hcIntervalMinutes), cancellationToken);
                using var response = await _httpClient.PostAsync("m/send", _requestContent);
            }
        }

        protected override Task OnAfterStopAsync()
        {
            _httpClient?.Dispose();
            return Task.CompletedTask;
        }
    }
}