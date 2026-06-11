using Web.Server.Entities;
using Web.Server.Repositories;
using Web.Server.Services;

namespace Web.Server.BackgroundServices
{
    public class PassengerRailPollingService : BackgroundService
    {
        private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(15);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PassengerRailPollingService> _logger;

        public PassengerRailPollingService(IServiceScopeFactory scopeFactory, ILogger<PassengerRailPollingService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = DefaultPollInterval;

                try
                {
                    delay = await PollOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Passenger rail polling cycle failed.");
                }

                await Task.Delay(delay, stoppingToken);
            }
        }

        private async Task<TimeSpan> PollOnceAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var configRepository = scope.ServiceProvider.GetRequiredService<IAmtrakPollingConfigurationRepository>();
            var trackedTrainRepository = scope.ServiceProvider.GetRequiredService<IAmtrakTrackedTrainRepository>();
            var passengerMapPinService = scope.ServiceProvider.GetRequiredService<IPassengerMapPinService>();
            var passengerMapPinRepository = scope.ServiceProvider.GetRequiredService<IPassengerMapPinRepository>();
            var providerClient = scope.ServiceProvider.GetRequiredService<IPassengerRailProviderClient>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<PassengerRailPollingService>>();

            var config = await configRepository.GetAsync() ?? await configRepository.UpsertAsync(2);
            var trackedTrains = (await trackedTrainRepository.GetActiveAsync()).ToList();

            if (trackedTrains.Count == 0)
            {
                logger.LogDebug("PassengerRailPollingService idle: no configured Amtrak train numbers.");
                return TimeSpan.FromMinutes(config.PollIntervalMinutes);
            }

            foreach (var trackedTrain in trackedTrains)
            {
                var snapshots = await FetchWithRetryAsync(providerClient, trackedTrain.TrainNumber, stoppingToken);
                if (!snapshots.Any())
                {
                    await passengerMapPinService.DeleteByTrainNumberAsync(trackedTrain.TrainNumber);
                    continue;
                }

                foreach (var snapshot in snapshots)
                {
                    var isStale = DateTime.UtcNow - snapshot.UpdatedAtUtc >= StaleThreshold;
                    if (isStale)
                    {
                        await passengerMapPinService.DeleteByTrainIdAsync(snapshot.TrainId);
                        continue;
                    }

                    await passengerMapPinService.UpsertAsync(new PassengerMapPin
                    {
                        Provider = snapshot.Provider,
                        RouteName = snapshot.RouteName,
                        TrainNum = snapshot.TrainNum,
                        TrainId = snapshot.TrainId,
                        Heading = snapshot.Heading,
                        Latitude = snapshot.Latitude,
                        Longitude = snapshot.Longitude,
                        Velocity = snapshot.Velocity,
                        UpdatedAt = snapshot.UpdatedAtUtc,
                        IsStale = isStale
                    });
                }
            }

            return TimeSpan.FromMinutes(config.PollIntervalMinutes);
        }

        private async Task<IEnumerable<PassengerProviderTrainSnapshot>> FetchWithRetryAsync(IPassengerRailProviderClient providerClient, string trainNumber, CancellationToken stoppingToken)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return await providerClient.GetTrainsAsync(trainNumber, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (attempt >= RetryDelays.Length)
                    {
                        _logger.LogWarning(ex, "Amtraker request failed for train {TrainNumber} after retries.", trainNumber);
                        return [];
                    }

                    _logger.LogWarning(ex, "Amtraker request failed for train {TrainNumber}; retrying in {DelaySeconds} seconds.", trainNumber, RetryDelays[attempt].TotalSeconds);
                    await Task.Delay(RetryDelays[attempt], stoppingToken);
                }
            }
        }
    }
}