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
            var now = DateTime.UtcNow;


            // Update the beacon so it's known that the beacon is active
            var beacon = await _beaconRepository.GetByIdAsync(telemetry.Beacon.ID);
            if (beacon == null)
            {
                throw new InvalidOperationException("Beacon not found.");
            }

            beacon.Timestamp = now;
            await _beaconRepository.UpdateAsync(beacon);

            // Check if the beacon is multi-railroad
            var existingTelemetry = (await _telemetryRepository.GetAllAsync())
                .Where(x => x.Beacon.ID == telemetry.Beacon.ID)
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefault();

            var beaconIsMultiRailroad = existingTelemetry?.Beacon.Railroads.Count > 1;

            if (existingTelemetry == null && beaconIsMultiRailroad)
            {
                return;
            }

            // Update the telemetry
            telemetry.Timestamp = now;
            await _telemetryRepository.AddAsync(telemetry);

            // Prepare and send the map alert
            var fromGeoCoordinate = new GeoCoordinate(existingTelemetry.Beacon.Latitude, existingTelemetry.Beacon.Longitude);
            var toGeoCoordinate = new GeoCoordinate(telemetry.Beacon.Latitude, telemetry.Beacon.Longitude);
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

