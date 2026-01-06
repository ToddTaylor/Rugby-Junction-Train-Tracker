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
        private readonly int _stationaryDirectionNullThresholdHours;

        private readonly IBeaconRailroadService _beaconRailroadService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IMapper _mapper;
        private readonly IMapPinRepository _mapPinRepository;
        private readonly ITimeProvider _timeProvider;
        private readonly IMapPinHistoryService _mapPinHistoryService;

        public MapPinService(
            IBeaconRailroadService beaconRailroadService,
            IMapPinHistoryService mapPinHistoryService,
            IMapPinRepository mapPinRepository,
            IHubContext<NotificationHub> hubContext,
            IMapper mapper,
            ITimeProvider timeProvider,
            IConfiguration configuration)
        {
            _beaconRailroadService = beaconRailroadService;
            _hubContext = hubContext;
            _mapper = mapper;
            _mapPinRepository = mapPinRepository;
            _timeProvider = timeProvider;
            _mapPinHistoryService = mapPinHistoryService;
            _stationaryDirectionNullThresholdHours = configuration.GetValue<int>("ApplicationSettings:StationaryDirectionNullThresholdHours", 6);
        }

        public async Task<bool> DeleteMapPinAsync(int id)
        {
            return await _mapPinRepository.DeleteAsync(id);
        }

        public async Task<IEnumerable<MapPin>> GetMapPinsAsync(int? minutes)
        {
            var mapPins = await _mapPinRepository.GetAllAsync(minutes);

            // Recalculate IsLocal flag for each map pin based on current subdivision settings
            foreach (var mapPin in mapPins)
            {
                if (mapPin.BeaconRailroad?.Subdivision != null && mapPin.Addresses.Any())
                {
                    var primaryAddressID = mapPin.Addresses.First().AddressID;
                    mapPin.IsLocal = IsLocalTrain(primaryAddressID, mapPin.BeaconRailroad.Subdivision);
                }
            }

            return mapPins;
        }

        public async Task<IEnumerable<MapPin>> GetMapPinsLatestAsync()
        {
            // Get latest from current MapPins table
            var mapPins = await _mapPinRepository.GetLatestAsync();

            // Recalculate IsLocal flag for each map pin based on current subdivision settings
            foreach (var mapPin in mapPins)
            {
                if (mapPin.BeaconRailroad?.Subdivision != null && mapPin.Addresses.Any())
                {
                    var primaryAddressID = mapPin.Addresses.First().AddressID;
                    mapPin.IsLocal = IsLocalTrain(primaryAddressID, mapPin.BeaconRailroad.Subdivision);
                }
            }

            // Get latest from MapPinHistory table for beacons that don't have a current MapPin
            var historyLatest = await _mapPinHistoryService.GetLatestPerBeaconAsync();
            
            // Create a set of beacon+subdivision keys that already have MapPin entries
            var existingKeys = new HashSet<string>(
                mapPins.Select(mp => $"{mp.BeaconID}|{mp.SubdivisionId}")
            );

            // Add history entries for beacons that don't have a current MapPin
            var historyMapPins = historyLatest
                .Where(h => !existingKeys.Contains($"{h.BeaconID}|{h.SubdivisionId}"))
                .Select(h => new MapPin
                {
                    BeaconID = h.BeaconID,
                    SubdivisionId = h.SubdivisionId,
                    Direction = h.Direction,
                    LastUpdate = h.LastUpdate,
                    BeaconRailroad = h.BeaconRailroad
                });

            return mapPins.Concat(historyMapPins);
        }

        public async Task<MapPin?> GetMapPinByIdAsync(int addressID)
        {
            return await _mapPinRepository.GetByAddressIdAsync(addressID);
        }

        public async Task UpsertMapPin(Telemetry telemetry, ICollection<BeaconRailroad> railroadBeacons)
        {
            MapPin? mapPin = null;
            bool isNewMapPin = false; // Track if this is a newly created map pin

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
                                    // Check if this would mix DPU with HOT/EOT (only valid on single-track beacons)
                                    var previousPinHasDPU = previousMapPinByTimeThreshold.Addresses.Any(a => a.Source == SourceEnum.DPU);
                                    var previousPinHasHOTorEOT = previousMapPinByTimeThreshold.Addresses.Any(a => a.Source == SourceEnum.HOT || a.Source == SourceEnum.EOT);
                                    var telemetryIsDPU = telemetry.Source == SourceEnum.DPU;
                                    var telemetryIsHOTorEOT = telemetry.Source == SourceEnum.HOT || telemetry.Source == SourceEnum.EOT;

                                    // Only allow mixing DPU with HOT/EOT if the beacon itself is single-track (MultipleTracks == false)
                                    var beaconIsMultiTrack = toBeaconRailroadIsSingleTracked.MultipleTracks == true;
                                    var wouldMixDPUWithHOTEOT = (telemetryIsDPU && previousPinHasHOTorEOT) || (telemetryIsHOTorEOT && previousPinHasDPU);

                                    if (beaconIsMultiTrack && wouldMixDPUWithHOTEOT)
                                    {
                                        // This is a multitrack beacon and we're trying to mix DPU with HOT/EOT.
                                        // Don't combine them - let a new map pin be created below.
                                    }
                                    else
                                    {
                                        var newMapPin = previousMapPinByTimeThreshold;
                                        // Only update LastUpdate, keep CreatedAt to track how long at this beacon
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

                                        // Check if the map pin has been stationary for threshold time
                                        await ResetStationaryTrainDirection(newMapPin);

                                        mapPin = newMapPin;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Previous map pin with same address ID.

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
                        // Check if we're trying to add a DPU to a map pin that has HOT/EOT on a multitrack beacon
                        var previousPinHasHOTorEOT = previousMapPinByTrainID.Addresses.Any(a => a.Source == SourceEnum.HOT || a.Source == SourceEnum.EOT);
                        var currentBeaconRailroad = railroadBeacons.FirstOrDefault(br => br.BeaconID == telemetry.BeaconID);
                        var beaconIsMultiTrack = currentBeaconRailroad?.MultipleTracks == true;

                        if (beaconIsMultiTrack && previousPinHasHOTorEOT)
                        {
                            // This is a multitrack beacon and the map pin has HOT/EOT addresses.
                            // Don't combine DPU with HOT/EOT - let a new map pin be created below.
                        }
                        else
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

                            mapPin = previousMapPinByTrainID;
                        }
                    }
                    else
                    {
                        // Simple last updates are handled in regular Address ID logic, no need to duplicate here.
                        mapPin = previousMapPinByTrainID;
                    }
                }
            }

            if (mapPin == null)
            {
                // Note: A new map pin will never have a direction as it
                // can't be calculated without a previous map pin.
                mapPin = await this.CreateMapPin(telemetry, railroadBeacons);
                isNewMapPin = true; // This is a new map pin
            }

            var upsertedMapPin = await _mapPinRepository.UpsertAsync(mapPin!);

            // Save or update history record
            // - For new map pins: create new history record
            // - For existing map pins: update recent history record with new addresses (if within 5 min)
            await _mapPinHistoryService.CreateOrUpdateHistoryFromMapPin(upsertedMapPin, isNewMapPin);

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
                mapPin.IsLocal = IsLocalTrain(telemetry.AddressID, beaconRailroad.Subdivision);
            }
            else
            {
                // Railroad cannot be determined without a previous map pin.

                // HACK: Use the first beacon railroad with no direction as a temporary solution.

                mapPin.BeaconRailroad = beaconRailroad;
                mapPin.BeaconID = beaconRailroad.BeaconID;
                mapPin.SubdivisionId = beaconRailroad.Subdivision.ID;
                mapPin.IsLocal = IsLocalTrain(telemetry.AddressID, beaconRailroad.Subdivision);
            }

            if (telemetry.Moving.HasValue)
            {
                mapPin.Moving = telemetry.Moving;
            }

            await this.UpdateBeaconTimestamp(beaconRailroad);

            return mapPin;
        }


        /// <summary>
        /// If a train has been at the same beacon for longer than the configured threshold,
        /// its direction should be removed (set to null).
        /// </summary>
        private async Task ResetStationaryTrainDirection(MapPin mapPin)
        {
            var currentMapPinHistory = await _mapPinHistoryService.GetHistoryByOriginalMapPinIdAsync(mapPin.ID);

            if (currentMapPinHistory == null) return;

            var timeSinceCreated = _timeProvider.UtcNow - currentMapPinHistory.CreatedAt;

            if (timeSinceCreated.TotalHours >= _stationaryDirectionNullThresholdHours)
            {
                mapPin.Direction = null;
            }
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
            else
            {
                // Same beacon - check if the map pin has been stationary for configured threshold
                await ResetStationaryTrainDirection(previousMapPin);
            }

            previousMapPin.BeaconRailroad = toBeaconRailroad;
            previousMapPin.BeaconID = telemetry.BeaconID;

            // Update IsLocal flag based on new subdivision
            previousMapPin.IsLocal = IsLocalTrain(telemetry.AddressID, toBeaconRailroad.Subdivision);

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

        /// <summary>
        /// Checks if the given address ID is in the subdivision's local train list.
        /// </summary>
        private static bool IsLocalTrain(int addressID, Subdivision subdivision)
        {
            if (string.IsNullOrWhiteSpace(subdivision.LocalTrainAddressIDs))
            {
                return false;
            }

            // Parse comma and line-separated list of address IDs
            var localAddressIDs = subdivision.LocalTrainAddressIDs
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            return localAddressIDs.Contains(addressID);
        }
    }
}
