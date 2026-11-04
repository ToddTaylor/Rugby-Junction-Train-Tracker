using Microsoft.AspNetCore.SignalR;
using Web.Server.Hubs;
using Web.Server.Services;

namespace Web.Server.BackgroundServices
{
    /// <summary>
    /// Checks every beacon PC once a minute and sends ServerStatusChanged to the Admins and Custodians who may see
    /// it when its status changes. The rules for when a change is announced are in ServerHealthService.
    /// </summary>
    public class ServerHealthMonitorService : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<ServerHealthMonitorService> _logger;

        public ServerHealthMonitorService(
            IServiceScopeFactory scopeFactory,
            IHubContext<NotificationHub> hubContext,
            ILogger<ServerHealthMonitorService> logger)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Server health monitor cycle failed.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        protected internal async Task CheckOnceAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var serverHealthService = scope.ServiceProvider.GetRequiredService<IServerHealthService>();

            foreach (var change in await serverHealthService.DetectStatusChangesAsync())
            {
                if (change.RecipientUserIds.Count == 0)
                {
                    continue;
                }

                var groups = change.RecipientUserIds.Select(NotificationHub.GetUserGroup).ToList();
                await _hubContext.Clients.Groups(groups)
                    .SendAsync(NotificationMethods.ServerStatusChanged, change.Notification, cancellationToken);
            }
        }
    }
}
