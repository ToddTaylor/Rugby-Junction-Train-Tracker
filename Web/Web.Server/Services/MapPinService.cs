using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class MapPinService : IMapPinsService
    {
        private readonly IBeaconRailroadService _beaconRailroadService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IMapper _mapper;
        private readonly IMapPinRepository _mapPinRepository;

        public MapPinService(
            IBeaconRailroadService beaconRailroadService,
            IHubContext<NotificationHub> hubContext,
            IMapper mapper,
            IMapPinRepository mapPinRepository)
        {
            _beaconRailroadService = beaconRailroadService;
            _hubContext = hubContext;
            _mapper = mapper;
            _mapPinRepository = mapPinRepository;
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

            var previousMapPin = await _mapPinRepository.GetByIdAsync(telemetry.AddressID);

            if (previousMapPin == null)
            {
                var newMapPin = _mapper.Map<MapPin>(telemetry);
                newMapPin.Addresses =
                [
                    new Address {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source
                    }
                ];

                newMapPin.Moving = telemetry.Moving;

                var oneRailroadBeacon = telemetryBeacon.BeaconRailroads.Count == 1;

                // Note: Direction cannot be determined without a previous map pin.
                if (oneRailroadBeacon)
                {
                    var beaconRailroad = telemetryBeacon.BeaconRailroads.First();

                    // Update the timestamp for beacon health calculations.
                    beaconRailroad.LastUpdate = DateTime.UtcNow;
                    await _beaconRailroadService.UpdateAsync(beaconRailroad);

                    newMapPin.BeaconRailroad = beaconRailroad;
                    newMapPin.BeaconID = beaconRailroad.BeaconID;
                    newMapPin.RailroadID = beaconRailroad.RailroadID;
                }
                else
                {
                    // Railroad cannot be determined without a previous map pin.

                    // HACK: Use the first beacon railroad with no direction as a temporary solution.
                    var beaconRailroad = telemetryBeacon.BeaconRailroads.First();

                    beaconRailroad.LastUpdate = DateTime.UtcNow;
                    await _beaconRailroadService.UpdateAsync(beaconRailroad);

                    newMapPin.BeaconRailroad = beaconRailroad;
                    newMapPin.BeaconID = beaconRailroad.BeaconID;
                    newMapPin.RailroadID = beaconRailroad.RailroadID;
                }

                finalMapPin = newMapPin;
            }
            else
            {
                var telemetryBeaconRailroad = telemetryBeacon.BeaconRailroads
                    .Where(br => br.BeaconID == telemetry.Beacon.ID)
                    .Where(br => br.RailroadID == previousMapPin.RailroadID)
                    .First();

                // Update the timestamp for beacon health calculations.
                telemetryBeaconRailroad.LastUpdate = DateTime.UtcNow;
                await _beaconRailroadService.UpdateAsync(telemetryBeaconRailroad);

                var differentBeacon = telemetry.Beacon.ID != previousMapPin.BeaconID;

                if (previousMapPin.Addresses != null
                    && previousMapPin.Addresses.Any(a => a.AddressID == telemetry.AddressID))
                {
                    // The collection contains the provided AddressID
                    var previousAddressSameSource = previousMapPin.Addresses
                        .Where(a => a.AddressID == telemetry.AddressID && a.Source == telemetry.Source)
                        .FirstOrDefault();

                    if (previousAddressSameSource != null)
                    {
                        previousAddressSameSource.Source = telemetry.Source;
                    }
                    else
                    {
                        previousMapPin.Addresses.Add(new Address
                        {
                            AddressID = telemetry.AddressID,
                            Source = telemetry.Source
                        });
                    }

                    previousMapPin.Moving = telemetry.Moving;
                }
                else
                {
                    previousMapPin.Addresses ??= [];

                    previousMapPin.Addresses.Add(new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source
                    });

                    if (telemetry.Moving.HasValue)
                    {
                        previousMapPin.Moving = telemetry.Moving;
                    }
                }

                if (differentBeacon || String.IsNullOrEmpty(previousMapPin.Direction))
                {
                    previousMapPin.Direction = await CalculateDirection(telemetry.Beacon.ID, telemetryBeaconRailroad, previousMapPin);
                }

                previousMapPin.BeaconID = telemetry.Beacon.ID;
                previousMapPin.RailroadID = previousMapPin.RailroadID;

                finalMapPin = previousMapPin;
            }

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
