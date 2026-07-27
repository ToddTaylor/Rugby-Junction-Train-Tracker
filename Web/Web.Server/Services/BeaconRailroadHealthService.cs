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
        public const int HealthCutoffMinutes = 15;

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
            var utcNow = DateTime.UtcNow;

            var beaconRailroads = dbContext.BeaconRailroads
                .Include(br => br.Beacon)
                .Include(br => br.Subdivision)
                    .ThenInclude(s => s.Railroad)
                .ToList();

            var beaconRailroadsByKey = beaconRailroads.ToDictionary(br => (br.BeaconID, br.SubdivisionID));

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
            var notesCleared = false;

            foreach (var beaconRailroadDTO in beaconRailroadDTOs)
            {
                var isOffline = IsOffline(beaconRailroadDTO.LastUpdate, utcNow);

                beaconRailroadDTO.Online = !isOffline;

                var entity = beaconRailroadsByKey[(beaconRailroadDTO.BeaconID, beaconRailroadDTO.SubdivisionID)];

                var hasTelemetryRecord = latestTelemetryByBeaconSubdivision.TryGetValue(
                    (beaconRailroadDTO.BeaconID, beaconRailroadDTO.SubdivisionID), out var lastTelemetryTime);
                var latestTelemetry = hasTelemetryRecord ? lastTelemetryTime : (DateTime?)null;

                var isTrulyOffline = IsTrulyOffline(beaconRailroadDTO.LastUpdate, latestTelemetry, utcNow);
                if (!isTrulyOffline && entity.OfflineNote != null)
                {
                    entity.OfflineNote = null;
                    notesCleared = true;
                }
                beaconRailroadDTO.OfflineNote = entity.OfflineNote;

                if (beaconRailroadDTO.Online)
                {
                    var effectiveThresholdHours = beaconRailroadDTO.TelemetryStaleHoursOverride ?? _telemetryStaleHoursDefault;
                    var telemetryCutoff = utcNow.AddHours(-effectiveThresholdHours);

                    if (hasTelemetryRecord)
                    {
                        beaconRailroadDTO.TelemetryStale = lastTelemetryTime <= telemetryCutoff;
                    }
                    else
                    {
                        // No train passage ever recorded for this beacon/subdivision — that
                        // trivially satisfies "no telemetry within the effective threshold",
                        // so it must be reported stale rather than exempted.
                        beaconRailroadDTO.TelemetryStale = true;
                    }
                }
                else
                {
                    beaconRailroadDTO.TelemetryStale = false;
                }

                updatedBeacons.Add(beaconRailroadDTO);
            }

            if (notesCleared)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Send all beacons as a single batch to match frontend expectation of Beacon[]
            if (updatedBeacons.Any())
            {
                await _hubContext.Clients.All.SendAsync(NotificationMethods.BeaconUpdate, updatedBeacons, cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Shared "is offline" rule used by both this background service and
        /// BeaconRailroadsController (to gate when an OfflineNote may be set).
        /// </summary>
        public static bool IsOffline(DateTime lastUpdate, DateTime utcNow)
        {
            return lastUpdate != default && lastUpdate <= utcNow.AddMinutes(-HealthCutoffMinutes);
        }

        /// <summary>
        /// A beacon railroad is "truly" offline — the state an OfflineNote is tied to — only
        /// when neither a health check nor telemetry has been received recently. This is
        /// deliberately broader than the Online flag (which is health-check-only, driving the
        /// gray/blue-ring map visuals): telemetry receipt alone must also be able to clear a
        /// note, per the offline-note feature's requirements.
        /// </summary>
        public static bool IsTrulyOffline(DateTime lastHealthUpdate, DateTime? lastTelemetryUpdate, DateTime utcNow)
        {
            var healthOffline = IsOffline(lastHealthUpdate, utcNow);
            var telemetryOffline = !lastTelemetryUpdate.HasValue || IsOffline(lastTelemetryUpdate.Value, utcNow);
            return healthOffline && telemetryOffline;
        }
    }
}
