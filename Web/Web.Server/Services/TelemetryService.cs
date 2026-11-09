using Web.Server.Entities;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class TelemetryService : ITelemetryService
    {
        private const int STALE_TELEMETRY_THRESHOLD_MINUTES = 5;

        private readonly ITelemetryRepository _telemetryRepository;
        private readonly IBeaconService _beaconService;
        private readonly IBeaconRailroadService _beaconRailroadService;
        private readonly IMapPinService _mapPinsService;
        private readonly ITimeProvider _timeProvider;

        public TelemetryService(
            IBeaconRailroadService beaconRailroadService,
            IBeaconService beaconService,
            IMapPinService mapPinsService,
            ITelemetryRepository telemetryRepository,
            ITimeProvider timeProvider)
        {
            _beaconRailroadService = beaconRailroadService;
            _beaconService = beaconService;
            _mapPinsService = mapPinsService;
            _telemetryRepository = telemetryRepository;
            _timeProvider = timeProvider;
        }

        public async Task<Telemetry> CreateMapPinAsync(Telemetry telemetry)
        {
            if (telemetry.AddressID <= 0)
            {
                throw new InvalidOperationException("Telemetry must have an AddressID.");
            }

            var beacon = await _beaconService.GetBeaconByIdAsync(telemetry.BeaconID);

            if (beacon == null)
            {
                throw new InvalidOperationException("Telemetry beacon not found.");
            }

            beacon = await UpdateBeaconHealth(beacon);

            telemetry.Beacon = beacon;

            // Set telemetry timestamps and default state
            telemetry.Discarded = false;

            // Discard telemetry if it's stale (i.e. if the beacon hasn't updated in a while, the
            // telemetry is likely inaccurate and not worth showing on the map).
            var staleThreshold = _timeProvider.UtcNow.AddMinutes(-STALE_TELEMETRY_THRESHOLD_MINUTES);

            if (telemetry.LastUpdate <= staleThreshold)
            {
                telemetry.Discarded = true;
                telemetry.DiscardReason = $"Stale Telemetry. (More than {STALE_TELEMETRY_THRESHOLD_MINUTES} minutes old.)";
            }

            // Insert new telemetry for historical logging purposes
            telemetry = await _telemetryRepository.AddAsync(telemetry);

            // Do not process stale telemetry into a map pin update
            if (telemetry.Discarded)
            {
                return telemetry;
            }

            // Upsert map pin via Map Pin service (telemetry will be saved within)
            await _mapPinsService.UpsertMapPin(telemetry);

            return telemetry;
        }

        public async Task<IEnumerable<Telemetry>> GetTelemetriesAsync()
        {
            return await _telemetryRepository.GetAllAsync();
        }

        public async Task<Telemetry?> GetTelemetryByIdAsync(int id)
        {
            return await _telemetryRepository.GetByIdAsync(id);
        }

        /// <summary>
        /// Refreshes the beacon's last-heard timestamps when telemetry arrives after a quiet period.
        /// </summary>
        /// <remarks>Telemetry proves the beacon PC is running, so the beacon and its railroads are touched, which
        /// Server Health counts as a check-in. The map status itself is broadcast by BeaconRailroadHealthService,
        /// which applies the Server Health rules.</remarks>
        /// <param name="beacon">The beacon to check and potentially update. Must not be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the updated beacon instance.</returns>
        private async Task<Beacon> UpdateBeaconHealth(Beacon beacon)
        {
            var cutoff = _timeProvider.UtcNow.AddMinutes(-BeaconServerStatusEvaluator.MaxOfflineThresholdMinutes);
            var beaconIsOffline = beacon.LastUpdate <= cutoff;

            if (beaconIsOffline)
            {
                // Update beacon's last update timestamp as it's the beacon that is actually online.
                beacon = await _beaconService.UpdateBeaconAsync(beacon.ID, beacon);

                // Update beacon railroads last update timestamps as well, since Server Health reads its check-ins from them.
                await _beaconRailroadService.UpdateAsync(beacon.BeaconRailroads);
            }

            return beacon;
        }
    }
}

