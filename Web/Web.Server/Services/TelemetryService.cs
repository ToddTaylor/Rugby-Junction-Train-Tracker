using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services.Rules;

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
        private readonly TelemetryRuleEngine _ruleEngine;

        public TelemetryService(
            IBeaconRailroadService beaconRailroadService,
            IBeaconService beaconService,
            IHubContext<NotificationHub> hubContext,
            IMapper mapper,
            IMapPinService mapPinsService,
            ITelemetryRepository telemetryRepository,
            ITimeProvider timeProvider,
            TelemetryRuleEngine ruleEngine)
        {
            _beaconRailroadService = beaconRailroadService;
            _beaconService = beaconService;
            _hubContext = hubContext;
            _mapper = mapper;
            _mapPinsService = mapPinsService;
            _telemetryRepository = telemetryRepository;
            _timeProvider = timeProvider;
            _ruleEngine = ruleEngine;
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
                throw new InvalidOperationException("Telemetry beacon not found."); // TODO: Not found exception.
            }

            var updatedBeaconRailroads = await _beaconRailroadService.UpdateAsync(beacon.BeaconRailroads);

            var beaconRailroadDTOs = _mapper.Map<ICollection<BeaconRailroadDTO>>(updatedBeaconRailroads);

            // Set online status to true since it's now updated.
            beaconRailroadDTOs.ToList().ForEach(beaconRailroadDTO => beaconRailroadDTO.Online = true);

            // Notify clients about the updated beacon railroads.
            await _hubContext.Clients.All.SendAsync(NotificationMethods.BeaconUpdate, beaconRailroadDTOs);

            // Set telemetry timestamps and default state
            telemetry.CreatedAt = _timeProvider.UtcNow;
            telemetry.Discarded = false;

            // Evaluate rules to determine if telemetry should be discarded
            var context = new TelemetryRuleContext
            {
                Telemetry = telemetry,
                RailroadBeacons = beacon.BeaconRailroads,
                RailroadId = beacon.BeaconRailroads.First().Subdivision.RailroadID
            };

            if (await _ruleEngine.ShouldDiscardAsync(context))
            {
                // Mark telemetry as discarded and save to database
                telemetry.Discarded = true;
                await _telemetryRepository.AddAsync(telemetry);

                return telemetry;
            }

            // Insert new telemetry for historical logging purposes.
            telemetry = await _telemetryRepository.AddAsync(telemetry);

            // Upsert map pin via Map Pin service (telemetry will be saved within)
            await _mapPinsService.UpsertMapPin(telemetry, beacon.BeaconRailroads);

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
    }
}

