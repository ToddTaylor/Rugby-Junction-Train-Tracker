using MapsterMapper;
using Microsoft.AspNetCore.SignalR;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Hubs;
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
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IMapper _mapper;
        private readonly IMapPinService _mapPinsService;
        private readonly ITimeProvider _timeProvider;

        public TelemetryService(
            IBeaconRailroadService beaconRailroadService,
            IBeaconService beaconService,
            IHubContext<NotificationHub> hubContext,
            IMapper mapper,
            IMapPinService mapPinsService,
            ITelemetryRepository telemetryRepository,
            ITimeProvider timeProvider)
        {
            _beaconRailroadService = beaconRailroadService;
            _beaconService = beaconService;
            _hubContext = hubContext;
            _mapper = mapper;
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
        /// Checks the health status of the specified beacon and updates its state if it is determined to be offline.
        /// </summary>
        /// <remarks>If the beacon is considered offline based on its last update timestamp, this method
        /// updates the beacon and its associated railroads, and notifies connected clients of the change.</remarks>
        /// <param name="beacon">The beacon to check and potentially update. Must not be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the updated beacon instance.</returns>
        private async Task<Beacon> UpdateBeaconHealth(Beacon beacon)
        {
            var cutoff = _timeProvider.UtcNow.AddMinutes(-15);
            var beaconIsOffline = beacon.LastUpdate <= cutoff;

            if (beaconIsOffline)
            {
                // Update beacon's last update timestamp as it's the beacon that is actually online.
                beacon = await _beaconService.UpdateBeaconAsync(beacon.ID, beacon);

                // Update beacon railroads last update timestamps as well bcause they are what is visible on the map.
                var updatedBeaconRailroads = await _beaconRailroadService.UpdateAsync(beacon.BeaconRailroads);

                var beaconRailroadDTOs = _mapper.Map<ICollection<BeaconRailroadDTO>>(updatedBeaconRailroads);
                beaconRailroadDTOs.ToList().ForEach(beaconRailroadDTO => beaconRailroadDTO.Online = true);

                // Notify clients.
                await _hubContext.Clients.All.SendAsync(NotificationMethods.BeaconUpdate, beaconRailroadDTOs);
            }

            return beacon;
        }
    }
}

