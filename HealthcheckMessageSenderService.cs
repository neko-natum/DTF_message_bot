using System.Threading;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;

namespace DTF_message_bot
{
    internal class HealthcheckMessageSenderService : ContinuousHostedService
    {
        private readonly HealthchecksOptions _options;
        private readonly OsnovaClient _client;

        public HealthcheckMessageSenderService(
            OsnovaClient client,
            IOptions<HealthchecksOptions> optionsAccessor, 
            IHostApplicationLifetime host):base(host)
        {
            _options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
            _client = client;
        }

        protected override Task<bool> OnBeforeStartAsync()
        {
            // sender will not start if there is no healthcheck configured
            if (string.IsNullOrWhiteSpace(_options.HealthcheckUri))
            {
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }

        protected override async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
#error TODO hc msg sender
            }
        }
    }
}

