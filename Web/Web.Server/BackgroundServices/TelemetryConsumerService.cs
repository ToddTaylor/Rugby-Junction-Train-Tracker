using AutoMapper;
using System.Text.Json;
using Web.Server.Controllers.v1;
using Web.Server.DTOs;
using Web.Server.Services;

namespace Web.Server.BackgroundServices
{
    public class TelemetryConsumerService : BackgroundService
    {
        private readonly ILogger<TelemetryConsumerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private const int MaxConcurrency = 2; // Adjust as needed for throttling

        public TelemetryConsumerService(
            ILogger<TelemetryConsumerService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(async () =>
            {
                _logger.LogDebug("TelemetryConsumerService using in-memory queue with throttling");

                var semaphore = new SemaphoreSlim(MaxConcurrency);

                while (!stoppingToken.IsCancellationRequested)
                {
                    if (TelemetriesController._telemetryQueue.TryDequeue(out var message))
                    {
                        await semaphore.WaitAsync(stoppingToken);
                        _ = ProcessMessageAsync(message, semaphore, stoppingToken);
                    }
                    else
                    {
                        await Task.Delay(100, stoppingToken);
                    }
                }
            }, stoppingToken);
        }

        private async Task ProcessMessageAsync(string message, System.Threading.SemaphoreSlim semaphore, CancellationToken stoppingToken)
        {
            try
            {
                var telemetryDTO = JsonSerializer.Deserialize<CreateTelemetryDTO>(message);
                if (telemetryDTO == null)
                {
                    _logger.LogWarning("[InMemoryQueue] Deserialized telemetryDTO is null. Message: {Message}", message);
                    return;
                }

                _logger.LogDebug($"[InMemoryQueue] Deserialized telemetryDTO: BeaconID={telemetryDTO.BeaconID}, AddressID={telemetryDTO.AddressID}, Source={telemetryDTO.Source}");

                using (var scope = _serviceProvider.CreateScope())
                {
                    var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
                    var telemetryService = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
                    var telemetry = mapper.Map<Web.Server.Entities.Telemetry>(telemetryDTO);

                    _logger.LogDebug($"[InMemoryQueue] Mapped Telemetry entity: BeaconID={telemetry.BeaconID}, AddressID={telemetry.AddressID}, Source={telemetry.Source}");

                    var result = await telemetryService.CreateMapPinAsync(telemetry);

                    _logger.LogDebug($"[InMemoryQueue] Telemetry processed. Result ID: {result?.ID}, Discarded: {result?.Discarded}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InMemoryQueue] Error processing telemetry message: {Message}", message);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
