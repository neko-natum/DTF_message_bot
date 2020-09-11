using DnsClient.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DTF_message_bot
{
    internal abstract class ContinuousHostedService : IHostedService
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly IHostApplicationLifetime _host;
        private readonly ILogger<ContinuousHostedService> _logger;
        private Task _serviceTask;

        public ContinuousHostedService(IHostApplicationLifetime host, ILogger<ContinuousHostedService> logger)
        {
            _host = host;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!await OnBeforeStartAsync())
            {
                return;
            }
            _serviceTask = Task.Run(async () =>
            {
                try
                {
                    await RunServiceAsync(_cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occured during the execution of the continuous service");
                }
                finally
                {
                    _logger.LogInformation($"Continuous service {GetType().Name} crashed or finished, stopping the application");
                    _host.StopApplication();
                }
            });
            await OnAfterStartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await OnBeforeStopAsync();
            _cts.Cancel();
            await _serviceTask;
            await OnAfterStopAsync();
        }

        protected virtual Task<bool> OnBeforeStartAsync() => Task.FromResult(true);
        protected virtual Task OnAfterStartAsync() => Task.CompletedTask;
        protected abstract Task RunServiceAsync(CancellationToken cancellationToken);
        protected virtual Task OnBeforeStopAsync() => Task.CompletedTask;
        protected virtual Task OnAfterStopAsync() => Task.CompletedTask;
    }
}

