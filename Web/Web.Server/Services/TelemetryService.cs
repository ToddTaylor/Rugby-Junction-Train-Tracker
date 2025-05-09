using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Providers;

namespace Web.Server.Services
{
    public class TelemetryService : ITelemetryService
    {
        private readonly ITelemetryRepository _telemetryRepository;
        private readonly IBeaconRepository _beaconRepository;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IMapper _mapper;
        private readonly ITimeProvider _timeProvider;

        public TelemetryService(
            IHubContext<NotificationHub> hubContext,
            ITelemetryRepository telemetryRepository,
            IBeaconRepository beaconRepository,
            IMapper mapper,
            ITimeProvider timeProvider)
        {
            _telemetryRepository = telemetryRepository;
            _beaconRepository = beaconRepository;
            _hubContext = hubContext;
            _mapper = mapper;
            _timeProvider = timeProvider;
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

            var now = _timeProvider.UtcNow;
            beacon.Timestamp = now;
            beacon = await _beaconRepository.UpdateAsync(beacon);

            // Get previous telemetry for same train address before inserting new telemetry
            var previousTelemetry = (await _telemetryRepository.GetAllAsync())
                .Where(x => x.AddressID == telemetry.AddressID)
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefault();

            var isNoPreviousTelemetry = previousTelemetry == null;
            var isNotSameBeacon = previousTelemetry?.Beacon.ID != telemetry.Beacon.ID;

            if (isNoPreviousTelemetry || isNotSameBeacon)
            {
                // Insert new telemetry
                telemetry.Timestamp = now;
                telemetry = await _telemetryRepository.AddAsync(telemetry);
            }
            else
            {
                // Update existing telemetry timestamp
                previousTelemetry.Timestamp = now;
                previousTelemetry = await _telemetryRepository.UpdateAsync(previousTelemetry);

                // Telemetry is from previous telemetry beacon.
                telemetry = previousTelemetry;
            }

            // Check if telemetry beacon is multi-railroad. 
            var telemetryBeaconIsMultiRailroad = telemetry.Beacon.BeaconRailroads.Count > 1;

            if (isNoPreviousTelemetry && telemetryBeaconIsMultiRailroad)
            {
                // There's no way to know which railroad the telemetry is on, so we can't send a map alert.
                return;
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

                direction = GetDirection(fromGeoCoordinate, toGeoCoordinate);
            }

            var mapAlert = _mapper.Map<MapAlert>(telemetry);
            mapAlert.Direction = direction;

            await _hubContext.Clients.All.SendAsync(NotificationMethods.MapAlert, mapAlert);
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

