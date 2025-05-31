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

        public async Task UpsertMapPin(Telemetry telemetry, ICollection<BeaconRailroad> beaconRailroads)
        {
            MapPin? mapPin;

            var previousMapPin = await _mapPinRepository.GetByIdAsync(telemetry.AddressID);

            if (previousMapPin != null)
            {
                mapPin = await this.UpdateMapPin(previousMapPin, telemetry, beaconRailroads);
            }
            else
            {
                mapPin = await this.CreateMapPin(telemetry, beaconRailroads);
            }

            await _mapPinRepository.UpsertAsync(mapPin!);

            var mapPinDTO = _mapper.Map<MapPinDTO>(mapPin);

            await _hubContext.Clients.All.SendAsync(NotificationMethods.MapPinUpdate, mapPinDTO);
        }

        /// <summary>
        /// Creates a new map pin.
        /// - Direction cannot be determined without a previous map pin.
        /// </summary>
        private async Task<MapPin> CreateMapPin(Telemetry telemetry, ICollection<BeaconRailroad> beaconRailroads)
        {
            var beaconRailroad = beaconRailroads.First();

            var mapPin = _mapper.Map<MapPin>(telemetry);

            mapPin.Addresses =
            [
                new Address {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source
                    }
            ];

            var singleRailroadBeacon = beaconRailroads.Count == 1;

            if (singleRailroadBeacon)
            {
                mapPin.BeaconRailroad = beaconRailroad;
                mapPin.BeaconID = beaconRailroad.BeaconID;
                mapPin.RailroadID = beaconRailroad.RailroadID;
            }
            else
            {
                // Railroad cannot be determined without a previous map pin.

                // HACK: Use the first beacon railroad with no direction as a temporary solution.

                mapPin.BeaconRailroad = beaconRailroad;
                mapPin.BeaconID = beaconRailroad.BeaconID;
                mapPin.RailroadID = beaconRailroad.RailroadID;
            }

            if (telemetry.Moving.HasValue)
            {
                mapPin.Moving = telemetry.Moving;
            }

            // Update the timestamp for beacon health calculations.
            beaconRailroad.LastUpdate = _timeProvider.UtcNow;
            await _beaconRailroadService.UpdateAsync(beaconRailroad);

            return mapPin;
        }

        private async Task<MapPin> UpdateMapPin(MapPin mapPin, Telemetry telemetry, ICollection<BeaconRailroad> beaconRailroads)
        {
            var toBeaconRailroad = beaconRailroads
                .Where(br => br.BeaconID == telemetry.Beacon.ID)
                .Where(br => br.RailroadID == mapPin?.RailroadID)
                .First();

            var matchingAddressIDAndSourceNotFound = !(mapPin.Addresses != null
                 && mapPin.Addresses.Any(a => a.AddressID == telemetry.AddressID && a.Source == telemetry.Source));

            if (matchingAddressIDAndSourceNotFound)
            {
                mapPin.Addresses?.Add(new Address
                {
                    AddressID = telemetry.AddressID,
                    Source = telemetry.Source
                });
            }

            var differentBeacon = telemetry.Beacon.ID != mapPin.BeaconID;

            if (differentBeacon || String.IsNullOrEmpty(mapPin.Direction))
            {
                var fromBeaconRailroad = await _beaconRailroadService.GetByIdAsync(mapPin.BeaconID, mapPin.RailroadID);

                mapPin.Direction = CalculateDirection(fromBeaconRailroad, toBeaconRailroad);
            }

            mapPin.BeaconID = telemetry.Beacon.ID;
            mapPin.RailroadID = mapPin.RailroadID;

            if (differentBeacon)
            {
                mapPin.Moving = null;
            }

            if (telemetry.Moving.HasValue)
            {
                mapPin.Moving = telemetry.Moving;
            }

            // Update the timestamp for beacon health calculations.
            toBeaconRailroad.LastUpdate = _timeProvider.UtcNow;
            await _beaconRailroadService.UpdateAsync(toBeaconRailroad);

            return mapPin;
        }

        private static string CalculateDirection(BeaconRailroad fromBeaconRailroad, BeaconRailroad toBeaconRailroad)
        {
            var fromGeoCoordinate = new GeoCoordinate(fromBeaconRailroad.Latitude, fromBeaconRailroad.Longitude);
            var toGeoCoordinate = new GeoCoordinate(toBeaconRailroad.Latitude, toBeaconRailroad.Longitude);

            return DirectionService.GetDirection(fromGeoCoordinate, toGeoCoordinate, toBeaconRailroad.Direction).ToString();
        }
    }
}
