using MapsterMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.DTOs;
using Web.Server.Hubs;

namespace Web.Server.Services
{
    public class BeaconRailroadHealthService : BackgroundService
    {
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(1);

        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IMapper _mapper;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly int _telemetryStaleHoursDefault;

        public BeaconRailroadHealthService(
            IHubContext<NotificationHub> hubContext,
            IMapper mapper,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration)
        {
            _hubContext = hubContext;
            _mapper = mapper;
            _scopeFactory = scopeFactory;
            _telemetryStaleHoursDefault = configuration.GetValue<int>("ApplicationSettings:TelemetryStaleHoursDefault", 6);
        }

        /// <summary>
        /// Executes the background service to deliver updated beacon data to the UI every minute.
        /// Sets Online based on the 15-minute health cutoff and TelemetryStale based on the
        /// effective telemetry-stale threshold (per-record override or app setting default).
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
                await ComputeAndSendBeaconStatusAsync(dbContext, stoppingToken);
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }

        /// <summary>
        /// Core iteration logic: compute Online and TelemetryStale for all beacon railroads
        /// and broadcast the result via SignalR.
        /// </summary>
        protected internal async Task ComputeAndSendBeaconStatusAsync(TelemetryDbContext dbContext, CancellationToken cancellationToken)
        {
            var healthCutoff = DateTime.UtcNow.AddMinutes(-15);

            var beaconRailroads = dbContext.BeaconRailroads
                .Include(br => br.Beacon)
                .Include(br => br.Subdivision)
                    .ThenInclude(s => s.Railroad)
                .ToList();

            // Build a map of (BeaconID, SubdivisionID) -> most recent train-passage timestamp.
            // Sourced from MapPinHistories rather than Telemetries: raw Telemetries rows are
            // purged after 12 hours by RecordCleanupService, which would make any beacon whose
            // last real train exceeded that window fall into "no telemetry ever received" and
            // incorrectly report TelemetryStale = false forever, regardless of the configured
            // override. MapPinHistories is retained for 48 hours and already carries a
            // per-subdivision SubdivisionId, so this also correctly scopes freshness per
            // railroad for beacons that serve more than one (e.g. junction beacons).
            var latestTelemetryByBeaconSubdivision = dbContext.MapPinHistories
                .GroupBy(mph => new { mph.BeaconID, mph.SubdivisionId })
                .Select(g => new { g.Key.BeaconID, g.Key.SubdivisionId, LastUpdate = g.Max(mph => mph.LastUpdate) })
                .ToDictionary(x => (x.BeaconID, x.SubdivisionId), x => x.LastUpdate);

            var beaconRailroadDTOs = _mapper.Map<IEnumerable<BeaconRailroadDTO>>(beaconRailroads);

            var updatedBeacons = new List<BeaconRailroadDTO>();

            foreach (var beaconRailroadDTO in beaconRailroadDTOs)
            {
                var isOffline = beaconRailroadDTO.LastUpdate != default && beaconRailroadDTO.LastUpdate <= healthCutoff;

                beaconRailroadDTO.Online = !isOffline;

                if (beaconRailroadDTO.Online)
                {
                    var effectiveThresholdHours = beaconRailroadDTO.TelemetryStaleHoursOverride ?? _telemetryStaleHoursDefault;
                    var telemetryCutoff = DateTime.UtcNow.AddHours(-effectiveThresholdHours);

                    if (latestTelemetryByBeaconSubdivision.TryGetValue((beaconRailroadDTO.BeaconID, beaconRailroadDTO.SubdivisionID), out var lastTelemetryTime))
                    {
                        beaconRailroadDTO.TelemetryStale = lastTelemetryTime <= telemetryCutoff;
                    }
                    else
                    {
                        // No train passage recorded for this beacon/subdivision within the
                        // MapPinHistories retention window — not considered stale
                        beaconRailroadDTO.TelemetryStale = false;
                    }
                }
                else
                {
                    beaconRailroadDTO.TelemetryStale = false;
                }

                updatedBeacons.Add(beaconRailroadDTO);
            }

            // Send all beacons as a single batch to match frontend expectation of Beacon[]
            if (updatedBeacons.Any())
            {
                await _hubContext.Clients.All.SendAsync(NotificationMethods.BeaconUpdate, updatedBeacons, cancellationToken: cancellationToken);
            }
        }
    }
}
