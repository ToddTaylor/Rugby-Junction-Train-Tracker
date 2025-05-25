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
        private readonly IBeaconService _beaconService;
        private readonly IMapper _mapper;
        private readonly IMapPinsService _mapPinsService;
        private readonly ITimeProvider _timeProvider;

        public TelemetryService(
            IHubContext<NotificationHub> hubContext,
            ITelemetryRepository telemetryRepository,
            IBeaconService beaconService,
            IMapper mapper,
            IMapPinsService mapPinsService,
            ITimeProvider timeProvider)
        {
            _telemetryRepository = telemetryRepository;
            _beaconService = beaconService;
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
            if (telemetry.AddressID == null)
            {
                throw new InvalidOperationException("Telemetry must have an AddressID.");
            }


            var telemetryBeacon = await _beaconService.GetBeaconByIdAsync(telemetry.Beacon.ID);

            if (telemetryBeacon == null)
            {
                throw new InvalidOperationException("Telemetry beacon not found.");
            }

            var now = _timeProvider.UtcNow;

            // Update's the telemetry beacon with the current timestamp.
            telemetryBeacon = await _beaconService.UpdateBeaconAsync(telemetryBeacon.ID, telemetryBeacon);

            // Inserts new telemetry for historical logging purposes.
            telemetry = await _telemetryRepository.AddAsync(telemetry);

            // Map Pin service to deal with map pin update.
            await _mapPinsService.UpsertMapPin(telemetry, telemetryBeacon);

            // Notify clients about the new telemetry.
            return telemetry;
        }
    }
}

