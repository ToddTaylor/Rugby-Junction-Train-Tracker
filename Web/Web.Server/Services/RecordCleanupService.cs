using Web.Server.Data;

namespace Web.Server.Services
{
    public class RecordCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RecordCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);

        public RecordCleanupService(IServiceScopeFactory scopeFactory, ILogger<RecordCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Runs a cleanup once an hour. A failed cleanup is logged and the next one runs on schedule. An unhandled
        /// exception here would stop the whole web host.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
                    await CleanUpOnceAsync(dbContext, DateTime.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Record cleanup failed.");
                }

                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }

        /// <summary>
        /// Deletes telemetry and map pins older than 12 hours and map pin history older than 48 hours.
        /// </summary>
        protected internal static async Task CleanUpOnceAsync(TelemetryDbContext dbContext, DateTime utcNow, CancellationToken cancellationToken)
        {
            var expirationTime12Hours = utcNow.AddHours(-12);
            var expirationTime48Hours = utcNow.AddHours(-48);

            var oldRecords = dbContext.Telemetries.Where(t => t.LastUpdate < expirationTime12Hours);
            dbContext.Telemetries.RemoveRange(oldRecords);

            var oldMapPins = dbContext.MapPins.Where(mp => mp.LastUpdate < expirationTime12Hours);
            dbContext.MapPins.RemoveRange(oldMapPins);

            var oldMapPinHistories = dbContext.MapPinHistories.Where(mph => mph.LastUpdate < expirationTime48Hours);
            dbContext.MapPinHistories.RemoveRange(oldMapPinHistories);

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
