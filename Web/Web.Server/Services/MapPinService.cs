using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Hubs;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class MapPinService : IMapPinService
    {
        public const int TIME_THRESHOLD_MINUTES = 2;

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

        public async Task<bool> DeleteMapPinAsync(int id)
        {
            return await _mapPinRepository.DeleteAsync(id);
        }

        public async Task<IEnumerable<MapPin>> GetMapPinsAsync(int? minutes)
        {
            return await _mapPinRepository.GetAllAsync(minutes);
        }
        public async Task<IEnumerable<MapPin>> GetMapPinsLatestAsync()
        {
            return await _mapPinRepository.GetLatestAsync();
        }

        public async Task<MapPin?> GetMapPinByIdAsync(int addressID)
        {
            return await _mapPinRepository.GetByAddressIdAsync(addressID);
        }

        public async Task UpsertMapPin(Telemetry telemetry, ICollection<BeaconRailroad> railroadBeacons)
        {
            MapPin? mapPin = null;

            // HOT / EOT logic depends on whether a previous map pin exists with the same address ID.
            var previousMapPinByAddressID = await _mapPinRepository.GetByAddressIdAsync(telemetry.AddressID);

            if (previousMapPinByAddressID == null)
            {
                // No previous map pin exists with same address ID (HOT / EOT).

                var singleRailroadBeacon = railroadBeacons.Count == 1;

                if (singleRailroadBeacon)
                {
                    // Single railroad beacon.

                    var railroadBeacon = railroadBeacons.First();

                    if (railroadBeacon.MultipleTracks == false)
                    {
                        // Single track railroad beacon.

                        var previousMapPinWithinTimeThreshold = await _mapPinRepository.GetByTimeThreshold(railroadBeacon.BeaconID, railroadBeacon.SubdivisionID, TIME_THRESHOLD_MINUTES);

                        if (previousMapPinWithinTimeThreshold != null)
                        {
                            // Existing map pin found for the same beacon and railroad subdivision within the time threshold. 

                            // Determine how to handle each source type for time threashold matches.
                            switch (telemetry.Source)
                            {
                                case SourceEnum.DPU:
                                    {
                                        // Add the DPU address to the existing map pin.

                                        previousMapPinWithinTimeThreshold.Addresses.Add(
                                            new Address
                                            {
                                                AddressID = telemetry.AddressID,
                                                DpuTrainID = telemetry.TrainID,
                                                Source = telemetry.Source,
                                                CreatedAt = telemetry.CreatedAt,
                                                LastUpdate = telemetry.LastUpdate
                                            });

                                        mapPin = previousMapPinWithinTimeThreshold;

                                        break;
                                    }
                                case SourceEnum.HOT:
                                    {
                                        // Add the HOT address to the existing map pin.

                                        // Note: WSOR seems to run trains with multiple HOT.
                                        // Since this is a single-track railroad beacon,
                                        // it's assumed to be the same train.

                                        previousMapPinWithinTimeThreshold.Addresses.Add(
                                            new Address
                                            {
                                                AddressID = telemetry.AddressID,
                                                Source = telemetry.Source,
                                                CreatedAt = telemetry.CreatedAt,
                                                LastUpdate = telemetry.LastUpdate
                                            });

                                        mapPin = previousMapPinWithinTimeThreshold;
                                        break;
                                    }
                                case SourceEnum.EOT:
                                    {
                                        var previousMapPinHasEOT = previousMapPinWithinTimeThreshold.Addresses
                                            .FirstOrDefault(a => a.Source == SourceEnum.EOT);

                                        if (previousMapPinHasEOT == null)
                                        {
                                            // The previous map pin does not have an EOT address ID, so add it.

                                            previousMapPinWithinTimeThreshold.Addresses.Add(
                                                new Address
                                                {
                                                    AddressID = telemetry.AddressID,
                                                    Source = telemetry.Source,
                                                    CreatedAt = telemetry.CreatedAt,
                                                    LastUpdate = telemetry.LastUpdate
                                                });

                                            mapPin = previousMapPinWithinTimeThreshold;
                                        }
                                        else
                                        {
                                            // The previous map pin already has an EOT address ID. Is it a match?

                                            if (telemetry.AddressID == previousMapPinHasEOT.AddressID)
                                            {
                                                // The EOT address ID is the same, update the timestamp.
                                                mapPin = previousMapPinWithinTimeThreshold;
                                                mapPin.LastUpdate = telemetry.LastUpdate;
                                            }
                                            else
                                            {
                                                // The EOT address ID is different, so it's not the same train.
                                                // Do nothing and a new map pin with new address will be created below.
                                            }
                                        }
                                        break;
                                    }
                            }
                        } // Else: Multiple track railroad beacon.  Can't assume it's the same train.
                    }
                    else
                    {
                        // Multiple railroad beacon.

                        var dpuCapableBeaconRailroads = railroadBeacons.Where(br => br.Subdivision.DpuCapable == true);

                        var oneBeaconRailroadThatIsDpuCapable = dpuCapableBeaconRailroads.Count() == 1;

                        if (oneBeaconRailroadThatIsDpuCapable)
                        {
                            // Since there's only one DPU-capable beacon railroad, DPU logic can be applied without concern of grouping
                            // two DPU equipped trains from different railroads together.

                            var dpuCapableBeaconRailroad = dpuCapableBeaconRailroads.First();

                            if (telemetry.TrainID.HasValue)
                            {
                                // This is a DPU.  See if it can be combined with the other DPU with the same Train ID.

                                var previousMapPinByDpuTrainID = await _mapPinRepository.GetByTrainIdAsync(telemetry.TrainID.Value);

                                if (previousMapPinByDpuTrainID != null)
                                {
                                    var matchingAddressIDAndSourceNotFound = !(
                                        previousMapPinByDpuTrainID.Addresses.Any(a =>
                                                a.AddressID == telemetry.AddressID
                                                    && a.Source == telemetry.Source
                                            )
                                     );

                                    if (matchingAddressIDAndSourceNotFound)
                                    {
                                        previousMapPinByDpuTrainID.Addresses.Add(
                                        new Address
                                        {
                                            AddressID = telemetry.AddressID,
                                            Source = telemetry.Source,
                                            DpuTrainID = telemetry.TrainID,
                                            CreatedAt = telemetry.CreatedAt,
                                            LastUpdate = telemetry.LastUpdate
                                        });
                                    } // Else: This is just a timestamp update to the map pin with an existing address ID and source

                                    mapPin = previousMapPinByDpuTrainID;
                                }
                            }
                        }

                        var toBeaconRailroadIsSingleTracked = railroadBeacons.Where(br => br.BeaconID == telemetry.BeaconID && br.MultipleTracks == false).FirstOrDefault();

                        if (toBeaconRailroadIsSingleTracked != null)
                        {
                            // Since the "to" beacon railroad is single-tracked, time threshold logic can be applied without concern
                            // of grouping two trains from different tracks together.

                            var previousMapPinByTimeThreshold = await _mapPinRepository.GetByTimeThreshold(
                                toBeaconRailroadIsSingleTracked.BeaconID,
                                toBeaconRailroadIsSingleTracked.Subdivision.ID,
                                TIME_THRESHOLD_MINUTES);

                            if (previousMapPinByTimeThreshold != null)
                            {
                                // Single beacon, same railroad, single track within 5 minutes threshold... it's likely the same train.

                                var previousPinHasEOT = previousMapPinByTimeThreshold.Addresses.Any(a => a.Source == SourceEnum.EOT);

                                if (telemetry.Source == SourceEnum.EOT && previousPinHasEOT)
                                {
                                    // The previous map pin already has an EOT with a differet address ID, so it's not the same train.
                                    // Do nothing.
                                }
                                else
                                {
                                    var newMapPin = previousMapPinByTimeThreshold;
                                    newMapPin.CreatedAt = telemetry.CreatedAt;
                                    newMapPin.LastUpdate = telemetry.LastUpdate;

                                    newMapPin.Addresses.Add(
                                        new Address
                                        {
                                            AddressID = telemetry.AddressID,
                                            Source = telemetry.Source,
                                            CreatedAt = telemetry.CreatedAt,
                                            LastUpdate = telemetry.LastUpdate
                                        });

                                    newMapPin.BeaconID = telemetry.BeaconID;
                                    newMapPin.BeaconRailroad = toBeaconRailroadIsSingleTracked;

                                    if (telemetry.Moving.HasValue)
                                    {
                                        newMapPin.Moving = telemetry.Moving;
                                    }

                                    mapPin = newMapPin;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Previous map pin with same address ID.

                var previousMapPinHotAddress = previousMapPinByAddressID.Addresses.Where(a => a.Source == SourceEnum.HOT).FirstOrDefault();

                if (previousMapPinHotAddress != null)
                {
                    var telemetryEotSource = telemetry.Source == SourceEnum.EOT;
                    var beaconsAreDifferent = previousMapPinByAddressID.BeaconID != telemetry.BeaconID;
                    var telemetryEotAfterPreviousHot = previousMapPinHotAddress.LastUpdate <= telemetry.LastUpdate;

                    if (telemetryEotSource && beaconsAreDifferent && telemetryEotAfterPreviousHot)
                    {
                        // If telemetry is EOT from a different beacon than the previous map pin that reported an HOT
                        // and the EOT timestamp is after the HOT timestamp, do nothing since the previous beacon
                        // is still reporting the end of the train while the head of the train is already at the next beacon.
                        return;
                    }
                }

                // Notes: With current logic, DPUs will never go here because their address ID is always unique.
                mapPin = await this.UpdateMapPin(telemetry, previousMapPinByAddressID, railroadBeacons);
            }

            // Check for new DPU telemetry by Train ID.
            if (telemetry.TrainID.HasValue && previousMapPinByAddressID == null)
            {
                var previousMapPinByTrainID = await _mapPinRepository.GetByTrainIdAsync(telemetry.TrainID.Value);

                if (previousMapPinByTrainID != null)
                {
                    var previousDpuAddress = previousMapPinByTrainID.Addresses.FirstOrDefault(a => a.Source == SourceEnum.DPU && a.AddressID == telemetry.AddressID);

                    if (previousDpuAddress == null)
                    {
                        previousMapPinByTrainID.Addresses.Add(
                            new Address
                            {
                                AddressID = telemetry.AddressID,
                                DpuTrainID = telemetry.TrainID,
                                Source = telemetry.Source,
                                CreatedAt = telemetry.CreatedAt,
                                LastUpdate = telemetry.LastUpdate
                            });
                    } // Simple last updates are handled in regular Address ID logic, no need to duplicate here.

                    mapPin = previousMapPinByTrainID;
                }
            }

            if (mapPin == null)
            {
                // Note: A new map pin will never have a direction as it
                // can't be calculated without a previous map pin.
                mapPin = await this.CreateMapPin(telemetry, railroadBeacons);
            }

            var upsertedMapPin = await _mapPinRepository.UpsertAsync(mapPin!);

            var mapPinDTO = _mapper.Map<MapPinDTO>(upsertedMapPin);

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
                    DpuTrainID = telemetry.TrainID,
                    Source = telemetry.Source,
                    CreatedAt = telemetry.CreatedAt,
                    LastUpdate = telemetry.LastUpdate
                }
            ];

            var singleRailroadBeacon = beaconRailroads.Count == 1;

            var beaconRailroad = beaconRailroads.First();

            if (singleRailroadBeacon)
            {
                mapPin.BeaconRailroad = beaconRailroad;
                mapPin.BeaconID = beaconRailroad.BeaconID;
                mapPin.SubdivisionId = beaconRailroad.Subdivision.ID;
            }
            else
            {
                // Railroad cannot be determined without a previous map pin.

                // HACK: Use the first beacon railroad with no direction as a temporary solution.

                mapPin.BeaconRailroad = beaconRailroad;
                mapPin.BeaconID = beaconRailroad.BeaconID;
                mapPin.SubdivisionId = beaconRailroad.Subdivision.ID;
            }

            if (telemetry.Moving.HasValue)
            {
                mapPin.Moving = telemetry.Moving;
            }

            await this.UpdateBeaconTimestamp(beaconRailroad);

            return mapPin;
        }

        private async Task<MapPin> UpdateMapPin(Telemetry telemetry, MapPin previousMapPin, ICollection<BeaconRailroad> beaconRailroads)
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
                    DpuTrainID = telemetry.TrainID,
                    Source = telemetry.Source,
                    CreatedAt = telemetry.CreatedAt,
                    LastUpdate = telemetry.LastUpdate
                });
            } // Else: This is just a timestamp update to the map pin with an existing address ID and source.

            var toBeaconRailroad = beaconRailroads
                .Where(br => br.BeaconID == telemetry.BeaconID)
                .First();

            var differentBeacon = telemetry.BeaconID != previousMapPin.BeaconID;

            previousMapPin.LastUpdate = telemetry.LastUpdate;

            foreach (var address in previousMapPin.Addresses)
            {
                address.LastUpdate = telemetry.LastUpdate;
            }

            if (differentBeacon)
            {
                var fromBeaconRailroad = await _beaconRailroadService.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.SubdivisionId);

                previousMapPin.SubdivisionId = toBeaconRailroad.Subdivision.ID;
                previousMapPin.Direction = CalculateDirection(fromBeaconRailroad, toBeaconRailroad);

                // Don't assume previous beacon's moving status.
                previousMapPin.Moving = null;
            }

            previousMapPin.BeaconRailroad = toBeaconRailroad;
            previousMapPin.BeaconID = telemetry.BeaconID;

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
