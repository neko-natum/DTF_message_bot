using System.Threading;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace DTF_message_bot
{
    internal abstract class ContinuousHostedService : IHostedService
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly IHostApplicationLifetime _host;
        private Task _serviceTask;

        public ContinuousHostedService(IHostApplicationLifetime host)
        {
            _host = host;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await OnBeforeStartAsync();
            _serviceTask = Task.Run(async () =>
            {
                try
                {
                    await RunServiceAsync(_cts.Token);
                }
                finally
                {
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

        protected virtual Task OnBeforeStartAsync() => Task.CompletedTask;
        protected virtual Task OnAfterStartAsync() => Task.CompletedTask;
        protected abstract Task RunServiceAsync(CancellationToken cancellationToken);
        protected virtual Task OnBeforeStopAsync() => Task.CompletedTask;
        protected virtual Task OnAfterStopAsync() => Task.CompletedTask;
    }
}

