using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Hubs;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services.Rules;

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
        private readonly ITelemetryRepository _telemetryRepository;
        private readonly IMapPinHistoryService _mapPinHistoryService;

        public MapPinService(
            IBeaconRailroadService beaconRailroadService,
            IMapPinHistoryService mapPinHistoryService,
            IMapPinRepository mapPinRepository,
            IHubContext<NotificationHub> hubContext,
            IMapper mapper,
            ITimeProvider timeProvider,
            ITelemetryRepository telemetryRepository,
            IConfiguration configuration)
        {
            _beaconRailroadService = beaconRailroadService;
            _hubContext = hubContext;
            _mapper = mapper;
            _mapPinRepository = mapPinRepository;
            _timeProvider = timeProvider;
            _telemetryRepository = telemetryRepository;
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

        public async Task<MapPin?> GetMapPinByIdAsync(int addressID, int? trainID)
        {
            return await _mapPinRepository.GetByAddressIdAsync(addressID, trainID);
        }

        public async Task UpsertMapPin(Telemetry telemetry)
        {
            MapPin? mapPin = null;
            bool isNewMapPin = false;

            // Will return map pin and related addresses if found.
            // HOT/EOT match on address ID only, DPU matches on address ID and Train ID.
            var existingExactMatchMapPin = await _mapPinRepository.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID);

            if (existingExactMatchMapPin == null)
            {
                // No previous map pin exists that matches HOT/EOT/DPU.

                var oneRailroadBeacon = (telemetry.Beacon.BeaconRailroads.Count == 1);
                var isSingleTrack = telemetry.Beacon.BeaconRailroads.FirstOrDefault()?.MultipleTracks == false;

                if (oneRailroadBeacon && isSingleTrack)
                {
                    // Single track, single railroad beacon has potential for time-threshold matching.

                    var railroadBeacon = telemetry.Beacon.BeaconRailroads.First();
                    var existingMapPinWithinTimeThreshold = await _mapPinRepository.GetByTimeThreshold(railroadBeacon.BeaconID, railroadBeacon.SubdivisionID, TIME_THRESHOLD_MINUTES);

                    if (existingMapPinWithinTimeThreshold != null)
                    {
                        // Existing map pin found for the same beacon and railroad subdivision within the time threshold. 

                        switch (telemetry.Source)
                        {
                            case SourceEnum.DPU:
                                {
                                    // Is there already a DPU train ID matching this one? Address does not have to match.
                                    // It'd be highly unlikely for 2 DPUs with the same train ID that weren't the same train
                                    // to be at the same beacon within the time threshold.
                                    var existingMapPinSameDPUTrainID = existingMapPinWithinTimeThreshold.Addresses
                                        .FirstOrDefault(a =>
                                            a.DpuTrainID == telemetry.TrainID &&
                                            a.Source == SourceEnum.DPU);

                                    // Can be combined with HOT/EOT within time threshold.
                                    var existingMapPinHasNoDPU = !existingMapPinWithinTimeThreshold.Addresses
                                        .Any(a => a.Source == SourceEnum.DPU);

                                    if (existingMapPinSameDPUTrainID != null || existingMapPinHasNoDPU)
                                    {
                                        // Add the DPU address to the existing map pin.

                                        existingMapPinWithinTimeThreshold.Addresses.Add(
                                            new Address
                                            {
                                                AddressID = telemetry.AddressID,
                                                DpuTrainID = telemetry.TrainID,
                                                Source = telemetry.Source,
                                                CreatedAt = _timeProvider.UtcNow,
                                                LastUpdate = _timeProvider.UtcNow
                                            });

                                        mapPin = existingMapPinWithinTimeThreshold;
                                    }

                                    break;
                                }
                            case SourceEnum.HOT:
                                {
                                    // Add the HOT address to the existing map pin, even if one already exists on a single track, single beacon railroad.

                                    // Note: WSOR seems to run trains with multiple HOT.
                                    // Since this is a single-track railroad beacon,
                                    // it's assumed to be the same train.

                                    existingMapPinWithinTimeThreshold.Addresses.Add(
                                        new Address
                                        {
                                            AddressID = telemetry.AddressID,
                                            Source = telemetry.Source,
                                            CreatedAt = _timeProvider.UtcNow,
                                            LastUpdate = _timeProvider.UtcNow
                                        });

                                    mapPin = existingMapPinWithinTimeThreshold;

                                    break;
                                }
                            case SourceEnum.EOT:
                                {
                                    var existingMapPinEOT = existingMapPinWithinTimeThreshold.Addresses
                                        .FirstOrDefault(a =>
                                            a.DpuTrainID == null &&
                                            a.Source == telemetry.Source);

                                    if (existingMapPinEOT == null)
                                    {
                                        // Train has no EOT yet - add it.
                                        existingMapPinWithinTimeThreshold.Addresses.Add(
                                            new Address
                                            {
                                                AddressID = telemetry.AddressID,
                                                Source = telemetry.Source,
                                                CreatedAt = _timeProvider.UtcNow,
                                                LastUpdate = _timeProvider.UtcNow
                                            });

                                        mapPin = existingMapPinWithinTimeThreshold;
                                    }
                                    else
                                    {
                                        // Train already has an EOT - discard duplicate EOT telemetry.
                                        return;
                                    }

                                    break;
                                }
                        }
                    }
                    else
                    {
                        // No previous map pin within time threshold.

                        // Fall through to create new map pin below.
                    }
                }
                else
                {
                    // Multiple railroad and multiple track beacons are ineligible for time-threshold matching.

                    if (telemetry.Source == SourceEnum.DPU)
                    {
                        // DPU must match on train ID on the same railroad to be the same train.

                        var existingMapPinByDpuTrainID = await _mapPinRepository.GetByTrainIdAsync(telemetry.TrainID.Value);

                        if (existingMapPinByDpuTrainID == null)
                        {
                            // No previous map pin matching DPU train ID.

                            // Fall through to create new map pin below.
                        }
                        else
                        {
                            // Existing map pin found matching DPU train ID.

                            var matchesTelemetryDpuCapableBeaconRailroad = telemetry.Beacon.BeaconRailroads
                                .Where(br =>
                                      br.BeaconID == existingMapPinByDpuTrainID.BeaconID &&
                                      br.Subdivision.DpuCapable)
                                .FirstOrDefault();

                            if (matchesTelemetryDpuCapableBeaconRailroad == null)
                            {
                                // Existing map pin is from different beacon, potentially a different railroad.

                                var matchingBeaconRailroad = telemetry.Beacon.BeaconRailroads
                                    .Where(br =>
                                        br.Subdivision.RailroadID == existingMapPinByDpuTrainID.BeaconRailroad.Subdivision.RailroadID &&
                                        br.Subdivision.DpuCapable)
                                    .FirstOrDefault();

                                if (matchingBeaconRailroad == null)
                                {
                                    // No matching railroad found between existing map pin and telemetry.

                                    // This is a different railroad's DPU train ID.  Update telemetry log with discard reason and exit.
                                    telemetry.DiscardReason = $"DPU Invalid Railroad";
                                    telemetry.Discarded = true;

                                    await _telemetryRepository.UpdateAsync(telemetry);

                                    return;
                                }
                            }

                            // Existing map pin is from same beacon railroad as the telemetry.

                            // Add the DPU address to the existing map pin.
                            existingMapPinByDpuTrainID.Addresses.Add(
                                new Address
                                {
                                    AddressID = telemetry.AddressID,
                                    DpuTrainID = telemetry.TrainID,
                                    Source = telemetry.Source,
                                    CreatedAt = _timeProvider.UtcNow,
                                    LastUpdate = _timeProvider.UtcNow

                                });

                            // Update the existing map pin with new telemetry data in case the map pin moved.
                            mapPin = await this.UpdateMapPin(telemetry, existingMapPinByDpuTrainID);

                            if (mapPin == null)
                            {
                                // Speed check failed, discard reason already recorded in telemetry
                                return;
                            }
                        }
                    }
                }
            }
            else
            {
                // Previous map pin exists containing address with matching address ID and/or train ID. However,
                // because only the address ID and/or train ID matched, check if the source matches as well for HOT/EOT.
                // DPU doesn't need an additional check since it matches on both address ID and train ID and the latter
                // indicates the source.

                if (telemetry.Source == SourceEnum.EOT || telemetry.Source == SourceEnum.HOT)
                {
                    var sourceDoesNotMatch = !existingExactMatchMapPin.Addresses.Any(a =>
                        a.AddressID == telemetry.AddressID &&
                        a.DpuTrainID == null &&
                        a.Source == telemetry.Source);

                    if (sourceDoesNotMatch)
                    {
                        // The address ID matches, but the source is different (e.g., HOT vs EOT).

                        // Add the new source address to the existing map pin.
                        existingExactMatchMapPin.Addresses.Add(
                            new Address
                            {
                                AddressID = telemetry.AddressID,
                                DpuTrainID = null,
                                Source = telemetry.Source,
                                CreatedAt = _timeProvider.UtcNow,
                                LastUpdate = _timeProvider.UtcNow
                            });
                    }
                }

                mapPin = await this.UpdateMapPin(telemetry, existingExactMatchMapPin);

                if (mapPin == null)
                {
                    // Speed check failed, discard reason already recorded in telemetry
                    return;
                }
            }

            if (mapPin == null)
            {
                // Note: A new map pin will never have a direction as it
                // can't be calculated without a previous map pin.
                mapPin = await this.CreateMapPin(telemetry);
                isNewMapPin = true;
            }

            var upsertedMapPin = await _mapPinRepository.UpsertAsync(mapPin!);

            // Save or update history record
            // - For new map pins: creates a new history record
            // - For existing map pins: updates a recent history record with new addresses (if within 5 min)
            await _mapPinHistoryService.CreateOrUpdateHistoryFromMapPin(upsertedMapPin, isNewMapPin);

            var mapPinDTO = _mapper.Map<MapPinDTO>(upsertedMapPin);

            await _hubContext.Clients.All.SendCoreAsync(NotificationMethods.MapPinUpdate, [mapPinDTO], default);
        }

        /// <summary>
        /// Creates a new map pin.
        /// - Direction cannot be determined without a previous map pin.
        /// </summary>
        private async Task<MapPin> CreateMapPin(Telemetry telemetry)
        {
            var mapPin = _mapper.Map<MapPin>(telemetry);

            mapPin.Addresses =
            [
                new Address {
                    AddressID = telemetry.AddressID,
                    DpuTrainID = telemetry.TrainID,
                    Source = telemetry.Source,
                    CreatedAt = _timeProvider.UtcNow,
                    LastUpdate = _timeProvider.UtcNow
                }
            ];

            var singleRailroadBeacon = telemetry.Beacon.BeaconRailroads.Count == 1;

            var beaconRailroad = telemetry.Beacon.BeaconRailroads.First();

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

        private async Task<MapPin?> UpdateMapPin(Telemetry telemetry, MapPin existingMapPin)
        {
            var differentBeacon = telemetry.BeaconID != existingMapPin.BeaconID;

            var toBeaconRailroad = await _beaconRailroadService.GetByIdAsync(telemetry.BeaconID, existingMapPin.SubdivisionId);

            // Clone existing map pin to modify.
            var newMapPin = existingMapPin.Clone();

            if (differentBeacon && toBeaconRailroad != null)
            {
                // Direction can be calculated since the beacon changed.

                var fromBeaconRailroad = await _beaconRailroadService.GetByIdAsync(existingMapPin.BeaconID, existingMapPin.SubdivisionId);

                // Apply speed sanity check rule before updating
                if (fromBeaconRailroad != null)
                {
                    var speedRule = new TrainSpeedSanityCheckRule();
                    var ruleContext = new MapPinRuleContext
                    {
                        FromBeaconRailroad = fromBeaconRailroad,
                        ToBeaconRailroad = toBeaconRailroad
                    };

                    var ruleResult = await speedRule.ShouldDiscardAsync(ruleContext);

                    if (ruleResult.ShouldDiscard)
                    {
                        // Speed check failed - update telemetry with discard reason and exit entirely
                        telemetry.DiscardReason = ruleResult.Reason;
                        telemetry.Discarded = true;

                        await _telemetryRepository.UpdateAsync(telemetry);

                        return null;  // Stop processing entirely
                    }
                }

                newMapPin.SubdivisionId = toBeaconRailroad.Subdivision.ID;
                newMapPin.Direction = CalculateDirection(fromBeaconRailroad, toBeaconRailroad);

                // Don't assume previous beacon's moving status.
                newMapPin.Moving = null;
            }
            else
            {
                // Beacon has not changed.

                // Check if the map pin has been stationary for configured threshold.

                await ResetStationaryTrainDirection(newMapPin);
            }

            newMapPin.BeaconRailroad = toBeaconRailroad;
            newMapPin.BeaconID = telemetry.BeaconID;
            newMapPin.LastUpdate = _timeProvider.UtcNow;

            foreach (var address in existingMapPin.Addresses)
            {
                address.LastUpdate = _timeProvider.UtcNow;
            }

            // Update IsLocal flag based on new subdivision
            newMapPin.IsLocal = IsLocalTrain(telemetry.AddressID, toBeaconRailroad.Subdivision);

            if (telemetry.Moving.HasValue)
            {
                newMapPin.Moving = telemetry.Moving;
            }

            await this.UpdateBeaconTimestamp(toBeaconRailroad);

            return newMapPin;
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
