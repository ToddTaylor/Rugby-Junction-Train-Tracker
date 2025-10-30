using AutoMapper;
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
            if (telemetry.AddressID <= 0)
            {
                throw new InvalidOperationException("Telemetry must have an AddressID.");
            }

            var beacon = await _beaconService.GetBeaconByIdAsync(telemetry.BeaconID);

            if (beacon == null)
            {
                throw new InvalidOperationException("Telemetry beacon not found."); // TODO: Not found exception.
            }

            var updatedBeaconRailroads = await _beaconRailroadService.UpdateAsync(beacon.BeaconRailroads);

            var beaconRailroadDTOs = _mapper.Map<ICollection<BeaconRailroadDTO>>(updatedBeaconRailroads);

            // Set online status to true since it's now updated.
            beaconRailroadDTOs.ToList().ForEach(beaconRailroadDTO => beaconRailroadDTO.Online = true);

            // Notify clients about the updated beacon railroads.
            await _hubContext.Clients.All.SendAsync(NotificationMethods.BeaconUpdate, beaconRailroadDTOs);

            // Insert new telemetry for historical logging purposes.
            telemetry = await _telemetryRepository.AddAsync(telemetry);

            // Upsert map pin via Map Pin service.
            await _mapPinsService.UpsertMapPin(telemetry, beacon.BeaconRailroads);

            // Notify clients about the new telemetry.
            return telemetry;
        }
    }
}

