using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Web.Server.Entities;
using Web.Server.Hubs;

namespace Web.Server.Services
{
    public class TelemetryService : ITelemetryService
    {
        private readonly ITelemetryRepository _telemetryRepository;
        private readonly IBeaconRepository _beaconRepository;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IMapper _mapper;

        public TelemetryService(
            IHubContext<NotificationHub> hubContext,
            ITelemetryRepository telemetryRepository,
            IBeaconRepository beaconRepository,
            IMapper mapper)
        {
            _telemetryRepository = telemetryRepository;
            _beaconRepository = beaconRepository;
            _hubContext = hubContext;
            _mapper = mapper;
        }

        public async Task<IEnumerable<Telemetry>> GetTelemetries()
        {
            return await _telemetryRepository.GetAllAsync();
        }

        public async Task CreateTelemetry(Telemetry telemetry)
        {
            // Update the beacon so it's known that the beacon is active
            var beacon = await _beaconRepository.GetByIdAsync(telemetry.Beacon.ID);
            if (beacon == null)
            {
                throw new InvalidOperationException("Beacon not found.");
            }

            var now = DateTime.UtcNow;
            beacon.Timestamp = now;
            await _beaconRepository.UpdateAsync(beacon);

            // Get previous telemetry for same train address before inserting new telemetry
            var previousTelemetry = (await _telemetryRepository.GetAllAsync())
                .Where(x => x.AddressID == telemetry.AddressID)
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefault();

            // Insert new telemetry
            telemetry.Timestamp = now;
            telemetry = await _telemetryRepository.AddAsync(telemetry);

            // Check if telemetry beacon is multi-railroad. 
            var telemetryBeaconIsMultiRailroad = telemetry.Beacon.BeaconRailroads.Count > 1;

            if (previousTelemetry == null && telemetryBeaconIsMultiRailroad)
            {
                // There's no way to know which railroad the telemetry is on, so we can't send a map alert.
                return;
            }

            var telemetryBeaconRailroad = telemetry.Beacon.BeaconRailroads.First();

            // Match the current telemetry beacon railroad with the previous telemetry beacon railroad to get the geo coordinates.
            var previousTelemetryBeaconRailroad = previousTelemetry?.Beacon.BeaconRailroads.Where(beaconRailroad => beaconRailroad.RailroadID == telemetryBeaconRailroad.RailroadID).First();

            // Prepare and send the map alert
            var fromGeoCoordinate = new GeoCoordinate(previousTelemetryBeaconRailroad.Latitude, previousTelemetryBeaconRailroad.Longitude);
            var toGeoCoordinate = new GeoCoordinate(telemetryBeaconRailroad.Latitude, telemetryBeaconRailroad.Longitude);
            var direction = GetDirection(fromGeoCoordinate, toGeoCoordinate);

            var mapAlert = _mapper.Map<MapAlert>(telemetry);
            mapAlert.Direction = direction;

            await _hubContext.Clients.All.SendAsync("MapAlert", mapAlert);
        }

        private static string GetDirection(GeoCoordinate from, GeoCoordinate to)
        {
            double latDiff = to.Latitude - from.Latitude;
            double lonDiff = to.Longitude - from.Longitude;

            if (Math.Abs(latDiff) > Math.Abs(lonDiff))
            {
                return latDiff > 0 ? "N" : "S";
            }
            else
            {
                return lonDiff > 0 ? "E" : "W";
            }
        }
    }
}

