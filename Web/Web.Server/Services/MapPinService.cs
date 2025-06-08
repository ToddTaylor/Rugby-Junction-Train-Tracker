using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class MapPinService : IMapPinService
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
            return await _mapPinRepository.GetByAddressIdAsync(addressID);
        }

        public async Task UpsertMapPin(Telemetry telemetry, ICollection<BeaconRailroad> beaconRailroads)
        {
            MapPin? mapPin = null;

            var previousMapPinByAddressID = await _mapPinRepository.GetByAddressIdAsync(telemetry.AddressID);

            if (previousMapPinByAddressID != null)
            {
                // Notes: With current logic, DPUs will never go here because their address ID is always unique.
                // With updated logic, DPUs might go here because their AddressID might match a previous map pin's AddressID.
                mapPin = await this.UpdateMapPin(previousMapPinByAddressID, telemetry, beaconRailroads);
            }

            if (previousMapPinByAddressID == null)
            {
                var singleRailroadBeacon = beaconRailroads.Count == 1;

                if (singleRailroadBeacon)
                {
                    // Single railroad beacon.

                    var beaconRailroad = beaconRailroads.First();

                    var previousMapPinByTimeThreshold = await _mapPinRepository.GetByTimeThreshold(beaconRailroad.BeaconID, beaconRailroad.RailroadID, minutesThreshold: 5);

                    // Same beacon, same railroad, within 5 minutes threshold.
                    // Unless there are multiple tracks, it's likely the same train.
                    if (previousMapPinByTimeThreshold != null)
                    {
                        previousMapPinByTimeThreshold.Addresses.Add(
                            new Address
                            {
                                AddressID = telemetry.AddressID,
                                Source = telemetry.Source,
                                LastUpdate = _timeProvider.UtcNow
                            });

                        mapPin = previousMapPinByTimeThreshold;
                    }
                }
                else
                {
                    // Multiple railroad beacon.

                    if (telemetry.Source == "DPU")
                    {
                        // If source is DPU and only 1 beacon railroad is DPU capable,
                        // then use that beacon railroad.
                        var dpuCapableBeaconRailroads = beaconRailroads.Where(br => br.Railroad.DpuCapable == true);

                        var singleDpuCapableRailroad = dpuCapableBeaconRailroads.Count() == 1;

                        if (singleDpuCapableRailroad)
                        {
                            var dpuCapableBeaconRailroad = dpuCapableBeaconRailroads.First();

                            var previousMapPinByTimeThreshold = await _mapPinRepository.GetByTimeThreshold(dpuCapableBeaconRailroad.BeaconID, dpuCapableBeaconRailroad.RailroadID, minutesThreshold: 5);

                            // Same beacon, same railroad, within 5 minutes threshold.
                            // Unless there are multiple tracks, it's likely the same train.
                            if (previousMapPinByTimeThreshold != null)
                            {
                                previousMapPinByTimeThreshold.Addresses.Add(
                                    new Address
                                    {
                                        AddressID = telemetry.AddressID,
                                        Source = telemetry.Source,
                                        LastUpdate = _timeProvider.UtcNow
                                    });

                                mapPin = previousMapPinByTimeThreshold;
                            }
                        }
                    }

                }
            }

            if (mapPin == null)
            {
                // Note: A new map pin will never have a direction as it
                // can't be calculated without a previous map pin.
                mapPin = await this.CreateMapPin(telemetry, beaconRailroads);
            }

            await _mapPinRepository.UpsertAsync(mapPin!);

            var mapPinDTO = _mapper.Map<MapPinDTO>(mapPin);

            await _hubContext.Clients.All.SendCoreAsync(NotificationMethods.MapPinUpdate, [mapPinDTO], default);
        }

        /// <summary>
        /// Creates a new map pin.
        /// - Direction cannot be determined without a previous map pin.
        /// </summary>
        private async Task<MapPin> CreateMapPin(Telemetry telemetry, ICollection<BeaconRailroad> beaconRailroads)
        {
            var mapPin = _mapper.Map<MapPin>(telemetry);

            mapPin.Addresses =
            [
                new Address {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        LastUpdate = _timeProvider.UtcNow
                    }
            ];

            var singleRailroadBeacon = beaconRailroads.Count == 1;

            var beaconRailroad = beaconRailroads.First();

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

            await this.UpdateBeaconTimestamp(beaconRailroad);

            return mapPin;
        }

        private async Task<MapPin> UpdateMapPin(MapPin previousMapPin, Telemetry telemetry, ICollection<BeaconRailroad> beaconRailroads)
        {
            var matchingAddressIDAndSourceNotFound = !(
                previousMapPin.Addresses.Any(a =>
                        a.AddressID == telemetry.AddressID
                            && a.Source == telemetry.Source
                    )
                 );

            if (matchingAddressIDAndSourceNotFound)
            {
                previousMapPin.Addresses.Add(new Address
                {
                    AddressID = telemetry.AddressID,
                    Source = telemetry.Source,
                    LastUpdate = _timeProvider.UtcNow
                });
            } // Else: This is just a timestamp update to the map pin with an existing address ID and source.

            var toBeaconRailroad = beaconRailroads
                .Where(br => br.BeaconID == telemetry.BeaconID)
                .Where(br => br.RailroadID == previousMapPin.RailroadID)
                .First();

            var differentBeacon = telemetry.BeaconID != previousMapPin.BeaconID;

            if (differentBeacon)
            {
                var fromBeaconRailroad = await _beaconRailroadService.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.RailroadID);

                previousMapPin.Direction = CalculateDirection(fromBeaconRailroad, toBeaconRailroad);
            }

            previousMapPin.BeaconRailroad = toBeaconRailroad;
            previousMapPin.BeaconID = telemetry.BeaconID;
            previousMapPin.RailroadID = previousMapPin.RailroadID;

            if (differentBeacon)
            {
                previousMapPin.Moving = null;
            }

            if (telemetry.Moving.HasValue)
            {
                previousMapPin.Moving = telemetry.Moving;
            }

            await this.UpdateBeaconTimestamp(toBeaconRailroad);

            return previousMapPin;
        }

        private static string CalculateDirection(BeaconRailroad fromBeaconRailroad, BeaconRailroad toBeaconRailroad)
        {
            var fromGeoCoordinate = new GeoCoordinate(fromBeaconRailroad.Latitude, fromBeaconRailroad.Longitude);
            var toGeoCoordinate = new GeoCoordinate(toBeaconRailroad.Latitude, toBeaconRailroad.Longitude);

            return DirectionService.GetDirection(fromGeoCoordinate, toGeoCoordinate, toBeaconRailroad.Direction).ToString();
        }

        private async Task UpdateBeaconTimestamp(BeaconRailroad beaconRailroad)
        {
            // Update the timestamp for beacon health calculations.
            beaconRailroad.LastUpdate = _timeProvider.UtcNow;
            await _beaconRailroadService.UpdateAsync(beaconRailroad);
        }
    }
}
