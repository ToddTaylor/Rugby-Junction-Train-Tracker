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
            var existingTelemetry = (await _telemetryRepository.GetAllAsync())
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefault();

            var beaconIsMultiRailroad = existingTelemetry?.Beacon.Railroads.Count > 1;

            if (existingTelemetry == null && beaconIsMultiRailroad)
            {
                return;
            }

            await _telemetryRepository.AddAsync(telemetry);

            var beacon = await _beaconRepository.GetByIdAsync(telemetry.Beacon.ID);
            if (beacon == null)
            {
                throw new InvalidOperationException("Beacon not found.");
            }

            beacon.Timestamp = DateTime.UtcNow;
            await _beaconRepository.UpdateAsync(beacon);

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

