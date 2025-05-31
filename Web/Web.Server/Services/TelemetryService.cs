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
        private readonly IMapPinsService _mapPinsService;
        private readonly ITimeProvider _timeProvider;

        public TelemetryService(
            ITelemetryRepository telemetryRepository,
            IBeaconService beaconService,
            IBeaconRailroadService beaconRailroadService,
            IHubContext<NotificationHub> hubContext,
            IMapper mapper,
            IMapPinsService mapPinsService,
            ITimeProvider timeProvider)
        {
            _telemetryRepository = telemetryRepository;
            _beaconService = beaconService;
            _beaconRailroadService = beaconRailroadService;
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
            if (telemetry.AddressID <= 0)
            {
                throw new InvalidOperationException("Telemetry must have an AddressID.");
            }

            var telemetryBeacon = await _beaconService.GetBeaconByIdAsync(telemetry.BeaconID);

            if (telemetryBeacon == null)
            {
                throw new InvalidOperationException("Telemetry beacon not found.");
            }

            await UpdateBeaconsTimestamps(telemetryBeacon);

            // Inserts new telemetry for historical logging purposes.
            telemetry = await _telemetryRepository.AddAsync(telemetry);

            // Map Pin service to deal with map pin update.
            await _mapPinsService.UpsertMapPin(telemetry, telemetryBeacon.BeaconRailroads);

            // Notify clients about the new telemetry.
            return telemetry;
        }

        private async Task UpdateBeaconsTimestamps(Beacon telemetryBeacon)
        {
            // Update's the telemetry beacon railroads with the current timestamp and notify clients that beacon railroads are online.
            foreach (var beaconRailroad in telemetryBeacon.BeaconRailroads)
            {
                await UpdateBeaconTimestamp(beaconRailroad);
            }
        }

        private async Task UpdateBeaconTimestamp(BeaconRailroad beaconRailroad)
        {
            beaconRailroad.LastUpdate = _timeProvider.UtcNow;

            await _beaconRailroadService.UpdateAsync(beaconRailroad);

            var beaconRailroadDTO = _mapper.Map<BeaconRailroadDTO>(beaconRailroad);
            beaconRailroadDTO.Online = true; // Set online status to true since it's now updated.

            await _hubContext.Clients.All.SendAsync(NotificationMethods.BeaconUpdate, beaconRailroadDTO);
        }
    }
}

