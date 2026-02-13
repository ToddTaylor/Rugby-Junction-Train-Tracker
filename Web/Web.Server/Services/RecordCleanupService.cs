using Web.Server.Data;

namespace Web.Server.Services
{
    public class RecordCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);

        public RecordCleanupService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Executes the background service to clean up old telemetry and map pin records every 12 hours.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

                var expirationTime12Hours = DateTime.UtcNow.AddHours(-12);
                var expirationTime48Hours = DateTime.UtcNow.AddHours(-48);

                var oldRecords = dbContext.Telemetries.Where(t => t.LastUpdate < expirationTime12Hours);
                dbContext.Telemetries.RemoveRange(oldRecords);

                var oldMapPins = dbContext.MapPins.Where(mp => mp.LastUpdate < expirationTime12Hours);
                dbContext.MapPins.RemoveRange(oldMapPins);

                var oldMapPinHistories = dbContext.MapPinHistories.Where(mph => mph.LastUpdate < expirationTime48Hours);
                dbContext.MapPinHistories.RemoveRange(oldMapPinHistories);

                await dbContext.SaveChangesAsync(stoppingToken);

                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }
    }
}
