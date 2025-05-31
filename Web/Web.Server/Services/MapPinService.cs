using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class MapPinService : IMapPinsService
    {
        private readonly IBeaconRailroadService _beaconRailroadService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IMapper _mapper;
        private readonly IMapPinRepository _mapPinRepository;
        private readonly ITimeProvider _timeProvider;

        public MapPinService(
            IBeaconRailroadService beaconRailroadService,
            IHubContext<NotificationHub> hubContext,
            IMapper mapper,
            IMapPinRepository mapPinRepository,
            ITimeProvider timeProvider)
        {
            _beaconRailroadService = beaconRailroadService;
            _hubContext = hubContext;
            _mapper = mapper;
            _mapPinRepository = mapPinRepository;
            _timeProvider = timeProvider;
        }

        public async Task<IEnumerable<MapPin>> GetMapPinsAsync(int? minutes)
        {
            return await _mapPinRepository.GetAllAsync(minutes);
        }

        public async Task<MapPin?> GetMapPinByIdAsync(int addressID)
        {
            return await _mapPinRepository.GetByIdAsync(addressID);
        }

        public async Task UpsertMapPin(Telemetry telemetry, Beacon telemetryBeacon)
        {
            MapPin? finalMapPin;
            BeaconRailroad? telemetryBeaconRailroad;

            var previousMapPin = await _mapPinRepository.GetByIdAsync(telemetry.AddressID);
            var noPreviousMapPinExists = previousMapPin == null;

            if (noPreviousMapPinExists)
            {
                // Create a new map pin.

                var newMapPin = _mapper.Map<MapPin>(telemetry);

                newMapPin.Addresses =
                [
                    new Address {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source
                    }
                ];

                var singleRailroadBeacon = telemetryBeacon.BeaconRailroads.Count == 1;

                if (singleRailroadBeacon)
                {
                    telemetryBeaconRailroad = telemetryBeacon.BeaconRailroads.First();

                    newMapPin.BeaconRailroad = telemetryBeaconRailroad;
                    newMapPin.BeaconID = telemetryBeaconRailroad.BeaconID;
                    newMapPin.RailroadID = telemetryBeaconRailroad.RailroadID;
                }
                else
                {
                    // Railroad cannot be determined without a previous map pin.

                    // HACK: Use the first beacon railroad with no direction as a temporary solution.

                    telemetryBeaconRailroad = telemetryBeacon.BeaconRailroads.First();

                    newMapPin.BeaconRailroad = telemetryBeaconRailroad;
                    newMapPin.BeaconID = telemetryBeaconRailroad.BeaconID;
                    newMapPin.RailroadID = telemetryBeaconRailroad.RailroadID;
                }

                // Note: Direction cannot be determined without a previous map pin.
                finalMapPin = newMapPin;
            }
            else
            {
                // Update the existing map pin with the new telemetry data.

                var matchingAddressIDAndSourceNotFound = !(previousMapPin.Addresses != null
                     && previousMapPin.Addresses.Any(a => a.AddressID == telemetry.AddressID && a.Source == telemetry.Source));

                if (matchingAddressIDAndSourceNotFound)
                {
                    previousMapPin.Addresses?.Add(new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source
                    });
                }

                var differentBeacon = telemetry.Beacon.ID != previousMapPin.BeaconID;

                telemetryBeaconRailroad = telemetryBeacon.BeaconRailroads
                    .Where(br => br.BeaconID == telemetry.Beacon.ID)
                    .Where(br => br.RailroadID == previousMapPin.RailroadID)
                    .First();

                if (differentBeacon || String.IsNullOrEmpty(previousMapPin.Direction))
                {
                    previousMapPin.Direction = await CalculateDirection(telemetry.Beacon.ID, telemetryBeaconRailroad, previousMapPin);
                }

                if (differentBeacon)
                {
                    previousMapPin.Moving = null;
                }

                previousMapPin.BeaconID = telemetry.Beacon.ID;
                previousMapPin.RailroadID = previousMapPin.RailroadID;

                finalMapPin = previousMapPin;
            }

            if (telemetry.Moving.HasValue)
            {
                finalMapPin.Moving = telemetry.Moving;
            }

            // Update the timestamp for beacon health calculations.
            telemetryBeaconRailroad.LastUpdate = _timeProvider.UtcNow;
            await _beaconRailroadService.UpdateAsync(telemetryBeaconRailroad);

            await _mapPinRepository.UpsertAsync(finalMapPin!);

            var mapPinDTO = _mapper.Map<MapPinDTO>(finalMapPin);

            await _hubContext.Clients.All.SendAsync(NotificationMethods.MapPinUpdate, mapPinDTO);
        }

        private async Task<string> CalculateDirection(int telemetryBeaconID, BeaconRailroad telemetryBeaconRailroad, MapPin previousMapPin)
        {
            var mapPinBeaconRailroad = await _beaconRailroadService.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.RailroadID);

            var fromGeoCoordinate = new GeoCoordinate(mapPinBeaconRailroad.Latitude, mapPinBeaconRailroad.Longitude);
            var toGeoCoordinate = new GeoCoordinate(telemetryBeaconRailroad.Latitude, telemetryBeaconRailroad.Longitude);

            return DirectionService.GetDirection(fromGeoCoordinate, toGeoCoordinate, telemetryBeaconRailroad.Direction).ToString();
        }
    }
}
