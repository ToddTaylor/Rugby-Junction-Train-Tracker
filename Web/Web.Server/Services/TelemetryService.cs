using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class TelemetryService : ITelemetryService
    {
        private readonly ITelemetryRepository _telemetryRepository;
        private readonly IBeaconRepository _beaconRepository;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IMapper _mapper;
        private readonly IMapPinsService _mapPinsService;
        private readonly ITimeProvider _timeProvider;

        public TelemetryService(
            IHubContext<NotificationHub> hubContext,
            ITelemetryRepository telemetryRepository,
            IBeaconRepository beaconRepository,
            IMapper mapper,
            IMapPinsService mapPinsService,
            ITimeProvider timeProvider)
        {
            _telemetryRepository = telemetryRepository;
            _beaconRepository = beaconRepository;
            _hubContext = hubContext;
            _mapper = mapper;
            _mapPinsService = mapPinsService;
            _timeProvider = timeProvider;
        }

        public async Task<IEnumerable<Telemetry>> GetTelemetriesAsync()
        {
            return await _telemetryRepository.GetAllAsync();
        }

        public async Task<Telemetry?> GetTelemetryByIdAsync(int id)
        {
            return await _telemetryRepository.GetByIdAsync(id);
        }

        public async Task<Telemetry> CreateTelemetryAsync(Telemetry telemetry)
        {
            // Update the beacon so it's known that the beacon is active
            var beacon = await _beaconRepository.GetByIdAsync(telemetry.Beacon.ID);
            if (beacon == null)
            {
                throw new InvalidOperationException("Beacon not found.");
            }

            var now = _timeProvider.UtcNow;
            beacon.LastUpdate = now;
            beacon = await _beaconRepository.UpdateAsync(beacon);

            // Get previous telemetry for same train address before inserting new telemetry
            // TODO: Create list of ID values instead of just AddressID to include HOT/EOT ID
            // and DPU ADDR IDs. This would involve an assumption that multiple telemetry values
            // at the same location and calculated to be heading in the same direction are 
            // actually the same train.
            var previousTelemetry = (await _telemetryRepository.GetAllAsync())
                .Where(x => x.AddressID == telemetry.AddressID)
                .OrderByDescending(x => x.LastUpdate)
                .FirstOrDefault();

            var isNoPreviousTelemetry = previousTelemetry == null;
            var isNotSameBeacon = previousTelemetry?.Beacon.ID != telemetry.Beacon.ID;

            if (isNoPreviousTelemetry || isNotSameBeacon)
            {
                // Insert new telemetry
                telemetry.LastUpdate = now;
                telemetry = await _telemetryRepository.AddAsync(telemetry);
            }
            else
            {
                // Update existing telemetry timestamp, source, and moving status
                previousTelemetry.Moving = telemetry.Moving;
                previousTelemetry.Source = telemetry.Source;
                previousTelemetry.LastUpdate = now;
                previousTelemetry = await _telemetryRepository.UpdateAsync(previousTelemetry);

                // Telemetry is from previous telemetry beacon.
                telemetry = previousTelemetry;

                // Upsert map pin for previous telemetry to update it's timestamp, source and moving status.
                var existingMapPin = await _mapPinsService.GetMapPinByIdAsync(telemetry.AddressID);
                existingMapPin.Moving = telemetry.Moving;
                existingMapPin.Source = telemetry.Source;
                _ = await _mapPinsService.UpsertMapPin(existingMapPin);

                // Send map alert for previous telemetry to update it's timestamp.
                await _hubContext.Clients.All.SendAsync(NotificationMethods.MapAlert, existingMapPin);

                return telemetry;
            }

            // Check if telemetry beacon is multi-railroad. 
            var telemetryBeaconIsMultiRailroad = telemetry.Beacon.BeaconRailroads.Count > 1;

            if (isNoPreviousTelemetry && telemetryBeaconIsMultiRailroad)
            {
                // There's no way to know which railroad the telemetry is on, so we can't send a map alert.
                return telemetry;
            }

            var direction = string.Empty;

            if (previousTelemetry != null)
            {
                // Get the same beacon for the same railroad as the previous telemetry.
                var telemetryBeaconRailroad = telemetry.Beacon.BeaconRailroads
                    .FirstOrDefault(br => previousTelemetry?.Beacon.BeaconRailroads
                        .Any(prevBr => prevBr.RailroadID == br.RailroadID) == true);

                var previousTelemetryBeaconRailroad = previousTelemetry?.Beacon.BeaconRailroads.Where(beaconRailroad => beaconRailroad.RailroadID == telemetryBeaconRailroad.RailroadID).First();
                var fromGeoCoordinate = new GeoCoordinate(previousTelemetryBeaconRailroad.Latitude, previousTelemetryBeaconRailroad.Longitude);
                var toGeoCoordinate = new GeoCoordinate(telemetryBeaconRailroad.Latitude, telemetryBeaconRailroad.Longitude);

                direction = DirectionService.GetDirection(fromGeoCoordinate, toGeoCoordinate, telemetryBeaconRailroad.Direction);
            }

            var mapPin = _mapper.Map<MapPin>(telemetry);
            mapPin.Direction = direction;

            await _mapPinsService.UpsertMapPin(mapPin);

            await _hubContext.Clients.All.SendAsync(NotificationMethods.MapAlert, mapPin);

            return telemetry;
        }
    }
}

