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
        /// Sets Online from the beacon PC's confirmed Server Health status and TelemetryStale from the
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
        /// Core iteration logic: compute the server status and TelemetryStale for all beacon railroads
        /// and broadcast the result via SignalR.
        /// </summary>
        protected internal async Task ComputeAndSendBeaconStatusAsync(TelemetryDbContext dbContext, CancellationToken cancellationToken)
        {
            var utcNow = DateTime.UtcNow;

            var beaconRailroads = await dbContext.BeaconRailroads
                .Include(br => br.Beacon)
                .Include(br => br.Subdivision)
                    .ThenInclude(s => s.Railroad)
                .ToListAsync(cancellationToken);

            // Every beacon railroad is loaded above, so each Beacon.BeaconRailroads is complete for GetLastCheckIn.
            var diagnosticsByBeacon = await dbContext.BeaconDiagnostics.ToDictionaryAsync(d => d.BeaconID, cancellationToken);
            var statesByBeacon = await dbContext.BeaconServerStates.ToDictionaryAsync(s => s.BeaconID, cancellationToken);
            var statusByBeacon = beaconRailroads
                .Where(br => br.Beacon != null)
                .Select(br => br.Beacon!)
                .DistinctBy(b => b.ID)
                .ToDictionary(
                    b => b.ID,
                    b => BeaconMapStatus.Resolve(b, diagnosticsByBeacon.GetValueOrDefault(b.ID), statesByBeacon.GetValueOrDefault(b.ID), utcNow));

            // Build a map of (BeaconID, SubdivisionID) -> most recent train-passage timestamp.
            // Sourced from MapPinHistories rather than Telemetries: raw Telemetries rows are
            // purged after 12 hours by RecordCleanupService, which would make any beacon whose
            // last real train exceeded that window fall into "no telemetry ever received".
            // MapPinHistories is retained for 48 hours and already carries a per-subdivision
            // SubdivisionId, so this also correctly scopes freshness per railroad for beacons
            // that serve more than one (e.g. junction beacons).
            var latestTelemetryByBeaconSubdivision = dbContext.MapPinHistories
                .GroupBy(mph => new { mph.BeaconID, mph.SubdivisionId })
                .Select(g => new { g.Key.BeaconID, g.Key.SubdivisionId, LastUpdate = g.Max(mph => mph.LastUpdate) })
                .ToDictionary(x => (x.BeaconID, x.SubdivisionId), x => x.LastUpdate);

            var beaconRailroadDTOs = _mapper.Map<IEnumerable<BeaconRailroadDTO>>(beaconRailroads).ToList();

            foreach (var beaconRailroadDTO in beaconRailroadDTOs)
            {
                if (statusByBeacon.TryGetValue(beaconRailroadDTO.BeaconID, out var status))
                {
                    beaconRailroadDTO.ApplyServerStatus(status);
                }

                if (beaconRailroadDTO.Online)
                {
                    var effectiveThresholdHours = beaconRailroadDTO.TelemetryStaleHoursOverride ?? _telemetryStaleHoursDefault;
                    var telemetryCutoff = utcNow.AddHours(-effectiveThresholdHours);

                    // No train passage ever recorded trivially satisfies "no telemetry within the
                    // effective threshold", so it is reported stale rather than exempted.
                    beaconRailroadDTO.TelemetryStale = !latestTelemetryByBeaconSubdivision.TryGetValue(
                            (beaconRailroadDTO.BeaconID, beaconRailroadDTO.SubdivisionID), out var lastTelemetryTime)
                        || lastTelemetryTime <= telemetryCutoff;
                }
                else
                {
                    beaconRailroadDTO.TelemetryStale = false;
                }
            }

            // Send all beacons as a single batch to match frontend expectation of Beacon[]
            if (beaconRailroadDTOs.Count > 0)
            {
                await _hubContext.Clients.All.SendAsync(NotificationMethods.BeaconUpdate, beaconRailroadDTOs, cancellationToken: cancellationToken);
            }
        }
    }
}
