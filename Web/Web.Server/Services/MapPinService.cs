using MapsterMapper;
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
        public const int TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES = 60;
        public const int TIME_THRESHOLD_DPU_EXACT_MINUTES = 360;
        public const int TIME_THRESHOLD_DPU_SAME_BEACON_TRAIN_ONLY_MAX_AGE_MINUTES = 30;
        public const int TIME_THRESHOLD_DPU_MINUTES = TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES;
        public const int SPEED_THRESHOLD_MPH = 50;

        private enum DpuMatchStatus
        {
            Matched,
            NoMatch,
            Discarded
        }

        private sealed class DpuMatchResult
        {
            public DpuMatchStatus Status { get; init; }

            public MapPin? MatchedMapPin { get; init; }

            public string? DiscardReason { get; init; }
        }

        private enum MapPinDiscardStatus
        {
            Keep,
            Discard
        }

        private sealed class MapPinDiscardDecision
        {
            public MapPinDiscardStatus Status { get; init; }

            public string? Reason { get; init; }
        }

        private readonly int _stationaryDirectionNullThresholdHours;

        private readonly IBeaconRailroadService _beaconRailroadService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IMapper _mapper;
        private readonly IMapPinRepository _mapPinRepository;
        private readonly ITimeProvider _timeProvider;
        private readonly ITelemetryRepository _telemetryRepository;
        private readonly IMapPinHistoryService _mapPinHistoryService;
        private readonly IMapPinRuleEngine _mapPinRuleEngine;
        private readonly ITelemetryRuleEngine _telemetryRuleEngine;
        private readonly ISubdivisionTrackageRightRepository _trackageRightRepository;
        private readonly IUserTrackedPinRepository _userTrackedPinRepository;
        private readonly ILogger<MapPinService> _logger;
        private readonly Dictionary<string, Web.Server.Services.Processors.IMapPinProcessor> _processorMap;

        public MapPinService(
            IBeaconRailroadService beaconRailroadService,
            IMapPinHistoryService mapPinHistoryService,
            IMapPinRepository mapPinRepository,
            IHubContext<NotificationHub> hubContext,
            IMapper mapper,
            ITimeProvider timeProvider,
            ITelemetryRepository telemetryRepository,
            IMapPinRuleEngine mapPinRuleEngine,
            ITelemetryRuleEngine telemetryRuleEngine,
            ISubdivisionTrackageRightRepository trackageRightRepository,
            IUserTrackedPinRepository userTrackedPinRepository,
            ILogger<MapPinService> logger,
            IConfiguration configuration,
            Dictionary<string, Web.Server.Services.Processors.IMapPinProcessor> processorMap)
        {
            _beaconRailroadService = beaconRailroadService;
            _hubContext = hubContext;
            _mapper = mapper;
            _mapPinRepository = mapPinRepository;
            _timeProvider = timeProvider;
            _telemetryRepository = telemetryRepository;
            _mapPinHistoryService = mapPinHistoryService;
            _mapPinRuleEngine = mapPinRuleEngine;
            _telemetryRuleEngine = telemetryRuleEngine;
            _trackageRightRepository = trackageRightRepository;
            _userTrackedPinRepository = userTrackedPinRepository;
            _logger = logger;
            _processorMap = processorMap;
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
                .Select(h =>
                {
                    int? primaryAddressID = null;
                    if (!string.IsNullOrWhiteSpace(h.AddressesJson))
                    {
                        try
                        {
                            var addresses = System.Text.Json.JsonSerializer.Deserialize<List<dynamic>>(h.AddressesJson);
                            if (addresses != null && addresses.Count > 0 && addresses[0].AddressID != null)
                            {
                                primaryAddressID = (int)addresses[0].AddressID;
                            }
                        }
                        catch { /* ignore parse errors */ }
                    }
                    bool isLocal = false;
                    if (primaryAddressID.HasValue && h.BeaconRailroad?.Subdivision != null)
                    {
                        isLocal = IsLocalTrain(primaryAddressID.Value, h.BeaconRailroad.Subdivision);
                    }
                    return new MapPin
                    {
                        BeaconID = h.BeaconID,
                        SubdivisionId = h.SubdivisionId,
                        CreatedRailroadID = h.CreatedRailroadID,
                        Direction = h.Direction,
                        LastUpdate = h.LastUpdate,
                        BeaconRailroad = h.BeaconRailroad,
                        IsLocal = isLocal
                    };
                });

            // Concatenate and sort by Direction (northbound, southbound, etc.)
            var allPins = mapPins.Concat(historyMapPins);

            // Nulls last, then alphabetically
            var sortedPins = allPins.OrderBy(mp => mp.Direction == null ? 1 : 0)
                                   .ThenBy(mp => mp.Direction ?? "");

            return sortedPins;
        }

        public async Task<MapPin?> GetMapPinByIdAsync(int addressID)
        {
            return await _mapPinRepository.GetByAddressIdAsync(addressID);
        }

        public async Task UpsertMapPin(Telemetry telemetry)
        {
            // Get the processor for this telemetry source
            if (!_processorMap.TryGetValue(telemetry.Source, out var processor))
            {
                throw new InvalidOperationException($"No processor registered for telemetry source '{telemetry.Source}'.");
            }

            // Run source-specific matching and validation
            var processorResult = await processor.ProcessAsync(telemetry);

            // If processor recommends discard, mark telemetry as discarded and exit
            if (processorResult.DiscardReason != null)
            {
                await DiscardTelemetryAsync(telemetry, processorResult.DiscardReason);
                return;
            }

            MapPin? mapPin = null;
            bool isNewMapPin = processorResult.IsNewMapPin;

            if (processorResult.IsNewMapPin)
            {
                // Create new map pin
                // Note: A new map pin will never have a direction as it can't be calculated without a previous map pin.
                mapPin = await this.CreateMapPin(telemetry);
            }
            else
            {
                // Existing map pin found; validate before update
                var existingMapPin = processorResult.MapPin!;

                if ((await ShouldDiscardMapPin(telemetry, existingMapPin)).Status == MapPinDiscardStatus.Discard)
                {
                    return;
                }

                mapPin = await UpdateMapPin(telemetry, existingMapPin);
            }

            if (mapPin == null)
            {
                await DiscardTelemetryAsync(telemetry, "Failed to create or update map pin.");
                return;
            }

            var upsertedMapPin = await _mapPinRepository.UpsertAsync(mapPin, telemetry.LastUpdate);

            // If at a single-track beacon, merge any other map pins at the same beacon into this one.
            upsertedMapPin = await MergeSingleTrackDuplicatesAsync(upsertedMapPin, telemetry);

            // Save or update history record
            // - For new map pins: creates a new history record
            // - For existing map pins: updates a recent history record with new addresses (if within 5 min)
            await _mapPinHistoryService.CreateOrUpdateHistoryFromMapPin(upsertedMapPin, isNewMapPin);

            var mapPinDTO = _mapper.Map<MapPinDTO>(upsertedMapPin);

            await _hubContext.Clients.All.SendCoreAsync(NotificationMethods.MapPinUpdate, [mapPinDTO], default);
        }

        private async Task<MapPin?> GetExistingHotOrEotMapPinAsync(Telemetry telemetry)
        {
            // Address match is only for HOT/EOT addresses (DpuTrainID == null).
            return await _mapPinRepository.GetByAddressIdAsync(telemetry.AddressID);
        }

        private void AddHotOrEotAddressIfMissing(MapPin existingMapPinToUpdate, Telemetry telemetry)
        {
            var sourceAlreadyPresent = existingMapPinToUpdate.Addresses.Any(a =>
                a.AddressID == telemetry.AddressID &&
                a.DpuTrainID == null &&
                a.Source == telemetry.Source);

            if (!sourceAlreadyPresent)
            {
                // Address matches existing map pin, but this HOT/EOT source is new.
                existingMapPinToUpdate.Addresses.Add(CreateAddress(telemetry));
            }
        }

        private void AddDpuAddressIfMissing(MapPin existingMapPinToUpdate, Telemetry telemetry)
        {
            var dpuAddressAlreadyPresent = existingMapPinToUpdate.Addresses.Any(a =>
                a.AddressID == telemetry.AddressID &&
                a.DpuTrainID == telemetry.TrainID &&
                a.Source == telemetry.Source);

            if (!dpuAddressAlreadyPresent)
            {
                existingMapPinToUpdate.Addresses.Add(CreateAddress(telemetry));
            }
        }

        /// <summary>
        /// At single-track beacons, only one map pin should exist. If the just-upserted map pin is at a
        /// single-track beacon and other map pins exist there, their addresses are merged into the current
        /// map pin and the duplicates are deleted.
        /// 
        /// Any tracking data on duplicate pins is preserved by migrating to the final merged pin. If the
        /// final merged pin has no tracking but a duplicate does, the duplicate's tracking info is transferred.
        /// </summary>
        private async Task<MapPin> MergeSingleTrackDuplicatesAsync(MapPin upsertedMapPin, Telemetry telemetry)
        {
            if (upsertedMapPin.BeaconRailroad?.MultipleTracks != false)
            {
                return upsertedMapPin;
            }

            var allPinsAtBeacon = await _mapPinRepository.GetAllByBeaconAsync(upsertedMapPin.BeaconID, upsertedMapPin.SubdivisionId, TIME_THRESHOLD_MINUTES);

            // "Duplicates" are defined as other map pins at the same beacon, excluding the just-upserted map pin.
            var duplicates = allPinsAtBeacon.Where(mp => mp.ID != upsertedMapPin.ID).ToList();

            if (duplicates.Count == 0)
            {
                return upsertedMapPin;
            }

            foreach (var duplicate in duplicates)
            {
                // DPU: only merge duplicates that are compatible with this DPU train.
                // If a duplicate already has a DPU with a different train ID, keep it separate.
                if (telemetry.Source == SourceEnum.DPU)
                {
                    var duplicateDpuTrainIds = duplicate.Addresses
                        .Where(a => a.Source == SourceEnum.DPU && a.DpuTrainID.HasValue)
                        .Select(a => a.DpuTrainID!.Value)
                        .Distinct()
                        .ToList();

                    if (duplicateDpuTrainIds.Count != 0 &&
                        (!telemetry.TrainID.HasValue || duplicateDpuTrainIds.Any(id => id != telemetry.TrainID.Value)))
                    {
                        continue;
                    }
                }

                foreach (var address in duplicate.Addresses)
                {
                    upsertedMapPin.Addresses.Add(address);
                }

                // Inherit direction from the duplicate if the merged pin has none.
                if (upsertedMapPin.Direction == null && duplicate.Direction != null)
                {
                    upsertedMapPin.Direction = duplicate.Direction;
                }

                // Delete the duplicate's history so it doesn't create a stale orphan record.
                await _mapPinHistoryService.DeleteHistoryByOriginalMapPinIdAsync(duplicate.ID);

                // Preserve tracking data before deleting the duplicate pin.
                // Migrate all tracked records from the duplicate to the final merged pin.
                await _userTrackedPinRepository.UpdateMapPinIdAsync(duplicate.ID, upsertedMapPin.ID);

                await _mapPinRepository.DeleteAsync(duplicate.ID);

                // Notify clients to remove any stale tracking labels for the deleted pin.
                await _hubContext.Clients.All.SendCoreAsync(NotificationMethods.TrackedPinRemoved, [duplicate.ID], default);
            }

            return await _mapPinRepository.UpsertAsync(upsertedMapPin, telemetry.LastUpdate);
        }

        /// <summary>
        /// Creates an Address object from telemetry data.
        /// </summary>
        private Address CreateAddress(Telemetry telemetry)
        {
            return new Address
            {
                AddressID = telemetry.AddressID,
                DpuTrainID = telemetry.TrainID,
                Source = telemetry.Source,
                CreatedAt = telemetry.LastUpdate,
                LastUpdate = telemetry.LastUpdate
            };
        }

        /// <summary>
        /// Discards telemetry with a specific reason and updates the repository.
        /// </summary>
        private async Task DiscardTelemetryAsync(Telemetry telemetry, string reason)
        {
            telemetry.DiscardReason = reason;
            telemetry.Discarded = true;
            await _telemetryRepository.UpdateAsync(telemetry);
        }

        private async Task<MapPin?> GetDpuMapPinByTrainIdAsync(int trainID)
        {
            // Get an existing map pin with the same DPU train ID within the DPU time threshold,
            // even if it's a different beacon. SoftDPU uses a 1 hour time threshold for the movements window.
            return await _mapPinRepository.GetByTrainIdAsync(trainID, TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES);
        }

        private async Task<MapPin?> GetDpuMapPinByAddressAndTrainIdAsync(int addressID, int trainID)
        {
            // Prefer an exact DPU address+train match within the DPU time threshold before falling back
            // to the broader train-only lookup.
            return await _mapPinRepository.GetByAddressAndTrainIdAsync(addressID, trainID, TIME_THRESHOLD_DPU_EXACT_MINUTES);
        }

        private async Task<DpuMatchResult> ResolveDpuMatchAsync(Telemetry telemetry)
        {
            if (!telemetry.TrainID.HasValue)
            {
                return new DpuMatchResult
                {
                    Status = DpuMatchStatus.Discarded,
                    DiscardReason = "DPU Missing TrainID"
                };
            }

            var exactMatchedMapPin = await GetDpuMapPinByAddressAndTrainIdAsync(telemetry.AddressID, telemetry.TrainID.Value);
            if (exactMatchedMapPin != null)
            {
                // Exact match on AddressID + TrainID confirms the same physical DPU device - no speed
                // or railroad validation is needed. Ambiguity about CN train-ID reuse only arises on
                // the train-only path (different AddressID, same TrainID). Post-match ShouldDiscardMapPin
                // still validates the movement via the full rule engine.
                return new DpuMatchResult
                {
                    Status = DpuMatchStatus.Matched,
                    MatchedMapPin = exactMatchedMapPin
                };
            }

            var matchedMapPin = await GetDpuMapPinByTrainIdAsync(telemetry.TrainID.Value);
            if (matchedMapPin == null)
            {
                return new DpuMatchResult
                {
                    Status = DpuMatchStatus.NoMatch
                };
            }

            var validation = await ValidateMatchedDpuMapPinAsync(telemetry, matchedMapPin);
            if (validation.Status == DpuMatchStatus.Matched)
            {
                return new DpuMatchResult
                {
                    Status = DpuMatchStatus.Matched,
                    MatchedMapPin = matchedMapPin
                };
            }

            return validation;
        }

        /// <summary>
        /// Validates that a DPU map pin matched by train ID is still a valid candidate for the incoming telemetry.
        /// Returns an explicit match result status.
        /// </summary>
        private Task<DpuMatchResult> ValidateMatchedDpuMapPinAsync(Telemetry telemetry, MapPin existingMapPinByDpuTrainID)
        {
            // Existing map pin found matching DPU train ID.

            // Train-only fallback can over-merge when train IDs are reused at the same beacon hours later.
            // If this is same-beacon and the existing pin is stale, treat as no-match and start a new logical pin.
            if (telemetry.BeaconID == existingMapPinByDpuTrainID.BeaconID)
            {
                var age = telemetry.CreatedAt - existingMapPinByDpuTrainID.LastUpdate;
                if (age.TotalMinutes > TIME_THRESHOLD_DPU_SAME_BEACON_TRAIN_ONLY_MAX_AGE_MINUTES)
                {
                    return Task.FromResult(new DpuMatchResult
                    {
                        Status = DpuMatchStatus.NoMatch
                    });
                }
            }

            var notTheSameBeacon = !telemetry.Beacon.BeaconRailroads
                .Any(br =>
                      br.BeaconID == existingMapPinByDpuTrainID.BeaconID &&
                      br.Subdivision.DpuCapable);

            if (notTheSameBeacon)
            {
                // Existing map pin is from different beacon, potentially a different railroad.

                var notTheSameRailroad = !telemetry.Beacon.BeaconRailroads
                    .Any(br =>
                        br.Subdivision.RailroadID == existingMapPinByDpuTrainID.BeaconRailroad.Subdivision.RailroadID &&
                        br.Subdivision.DpuCapable);

                if (notTheSameRailroad)
                {
                    // No matching railroad found between existing map pin and telemetry.
                    // This is a different railroad's DPU train ID.
                    return Task.FromResult(new DpuMatchResult
                    {
                        Status = DpuMatchStatus.Discarded,
                        DiscardReason = "DPU Invalid Railroad"
                    });
                }

                // Different beacon, same railroad.

                // Apply speed sanity test to make sure the same train ID isn't being used twice
                // on the same railroad on the same day (This DOES happen on CN!)

                var milesApart = Math.Abs(existingMapPinByDpuTrainID.BeaconRailroad.Milepost - telemetry.Beacon.BeaconRailroads.First(br => br.Subdivision.RailroadID == existingMapPinByDpuTrainID.BeaconRailroad.Subdivision.RailroadID).Milepost);

                var effectiveMilesApart = TrainSpeedSanityMath.GetAdjustedDistanceMiles(milesApart);
                var speedMph = TrainSpeedSanityMath.TryGetSpeedMph(effectiveMilesApart, existingMapPinByDpuTrainID.LastUpdate, telemetry.CreatedAt);
                if (!speedMph.HasValue)
                {
                    return Task.FromResult(new DpuMatchResult
                    {
                        Status = DpuMatchStatus.Matched
                    });
                }

                if (speedMph.Value > SPEED_THRESHOLD_MPH)
                {
                    // The existing map pin and telemetry are too far apart given the time difference, indicating that the
                    // same train ID is being used for different trains on the same railroad.

                    // Treat as no-match so a new map pin can be created.
                    return Task.FromResult(new DpuMatchResult
                    {
                        Status = DpuMatchStatus.NoMatch
                    });
                }
            }

            // Existing map pin is from same railroad as the telemetry.
            return Task.FromResult(new DpuMatchResult
            {
                Status = DpuMatchStatus.Matched
            });
        }

        /// <summary>
        /// Creates a new map pin.
        /// - Direction cannot be determined without a previous map pin.
        /// - CreatedRailroadID is set once at creation and never updated.
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
                    CreatedAt = telemetry.LastUpdate,
                    LastUpdate = telemetry.LastUpdate
                }
            ];

            var singleRailroadBeacon = telemetry.Beacon.BeaconRailroads.Count == 1;

            var beaconRailroad = telemetry.Beacon.BeaconRailroads.First();

            if (singleRailroadBeacon)
            {
                mapPin.BeaconRailroad = beaconRailroad;
                mapPin.BeaconID = beaconRailroad.BeaconID;
                mapPin.SubdivisionId = beaconRailroad.Subdivision.ID;
                mapPin.CreatedRailroadID = beaconRailroad.Subdivision.RailroadID;
                mapPin.IsLocal = IsLocalTrain(telemetry.AddressID, beaconRailroad.Subdivision);
            }
            else
            {
                // Railroad cannot be determined without a previous map pin.

                // HACK: Use the first beacon railroad with no direction as a temporary solution.

                mapPin.BeaconRailroad = beaconRailroad;
                mapPin.BeaconID = beaconRailroad.BeaconID;
                mapPin.SubdivisionId = beaconRailroad.Subdivision.ID;
                mapPin.CreatedRailroadID = beaconRailroad.Subdivision.RailroadID;
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

        private async Task<MapPinDiscardDecision> ShouldDiscardMapPin(Telemetry telemetry, MapPin existingMapPinToUpdate)
        {
            var toBeaconRailroad = (BeaconRailroad?)null;

            if (telemetry.Beacon.BeaconRailroads.Count() == 1)
            {
                toBeaconRailroad = telemetry.Beacon.BeaconRailroads.First();
            }
            else
            {
                // Is this a safe assumption that the "to" beacon railroad will be on the same railroad
                // as the "from" beacon railroad if the "to" beacon railroad is multi-railroad?
                //
                // Yes. The existing map pin has a known beacon railroad ("from" beacon). If the telemetry beacon ("to" beacon)
                // has multiple beacon railroads, the "from" beacon railroad's subdivision railroad ID can be used to find the
                // correct "to" beacon railroad among the multiple beacon railroads.

                toBeaconRailroad = telemetry.Beacon.BeaconRailroads
                    .Where(br => br.Subdivision.RailroadID == existingMapPinToUpdate.BeaconRailroad.Subdivision.RailroadID)
                    .FirstOrDefault();

                if (toBeaconRailroad == null)
                {
                    // Check trackage rights rules before discarding due to missing beacon railroad.

                    var trackageRights = await _trackageRightRepository.GetByFromSubdivisionAsync(existingMapPinToUpdate.BeaconRailroad.SubdivisionID);

                    if (trackageRights != null)
                    {
                        var hasRights = trackageRights.Where(tr => telemetry.Beacon.BeaconRailroads.Any(br => br.SubdivisionID == tr.ToSubdivisionID)).FirstOrDefault();

                        if (hasRights != null)
                        {
                            toBeaconRailroad = telemetry.Beacon.BeaconRailroads
                                .Where(br => br.Subdivision.ID == hasRights.ToSubdivisionID)
                                .FirstOrDefault();
                        }
                    }
                }
            }

            if (toBeaconRailroad == null)
            {
                return new MapPinDiscardDecision
                {
                    Status = MapPinDiscardStatus.Discard,
                    Reason = $"To beacon railroad not found for map pin ID {existingMapPinToUpdate.ID}. Telemetry BeaconID: {telemetry.BeaconID}, SubdivisionID: {existingMapPinToUpdate.SubdivisionId}"
                };
            }

            // Update the timestamp for discarding rules that rely on the time between beacons.
            toBeaconRailroad.LastUpdate = _timeProvider.UtcNow;

            var fromBeaconRailroad = await _beaconRailroadService.GetByIdAsync(existingMapPinToUpdate.BeaconID, existingMapPinToUpdate.SubdivisionId);

            if (fromBeaconRailroad == null)
            {
                return new MapPinDiscardDecision
                {
                    Status = MapPinDiscardStatus.Discard,
                    Reason = $"From beacon railroad not found for map pin ID {existingMapPinToUpdate.ID}. MapPin BeaconID: {existingMapPinToUpdate.BeaconID}, SubdivisionID: {existingMapPinToUpdate.SubdivisionId}"
                };
            }

            var differentBeacon = toBeaconRailroad.BeaconID != fromBeaconRailroad.BeaconID;

            if (differentBeacon)
            {
                // Ping pong telemetry rules.

                var context = new TelemetryRuleContext
                {
                    Telemetry = telemetry,
                    RailroadId = toBeaconRailroad.Subdivision.RailroadID,
                    ToMilepost = toBeaconRailroad.Milepost,
                    FromMilepost = fromBeaconRailroad.Milepost
                };

                var telemetryRuleResult = await _telemetryRuleEngine.ShouldDiscardAsync(context);

                if (telemetryRuleResult.ShouldDiscard)
                {
                    // Rule failed
                    telemetry.DiscardReason = telemetryRuleResult.Reason;
                    telemetry.Discarded = true;

                    await _telemetryRepository.UpdateAsync(telemetry);

                    return new MapPinDiscardDecision
                    {
                        Status = MapPinDiscardStatus.Discard,
                        Reason = telemetryRuleResult.Reason
                    };
                }

                // Train speed sanity check and trackage right rules.

                var ruleContext = new MapPinRuleContext
                {
                    FromBeaconRailroad = fromBeaconRailroad,
                    ToBeaconRailroad = toBeaconRailroad,
                    CreatedRailroadID = existingMapPinToUpdate.CreatedRailroadID!.Value,
                };

                var mapPinRuleResult = await _mapPinRuleEngine.ShouldDiscardAsync(ruleContext);

                if (mapPinRuleResult.ShouldDiscard)
                {
                    // Rule failed
                    telemetry.DiscardReason = mapPinRuleResult.Reason;
                    telemetry.Discarded = true;

                    await _telemetryRepository.UpdateAsync(telemetry);

                    return new MapPinDiscardDecision
                    {
                        Status = MapPinDiscardStatus.Discard,
                        Reason = mapPinRuleResult.Reason
                    };
                }
            }

            return new MapPinDiscardDecision
            {
                Status = MapPinDiscardStatus.Keep
            };
        }

        private async Task<MapPin?> UpdateMapPin(Telemetry telemetry, MapPin existingMapPinToUpdate)
        {
            var toBeaconRailroad = (BeaconRailroad?)null;

            if (telemetry.Beacon.BeaconRailroads.Count() == 1)
            {
                toBeaconRailroad = telemetry.Beacon.BeaconRailroads.First();
            }
            else
            {
                // Is this a safe assumption that the "to" beacon railroad will be on the same railroad
                // as the "from" beacon railroad if the "to" beacon railroad is multi-railroad?
                //
                // Yes. The existing map pin has a known beacon railroad ("from" beacon). If the telemetry beacon ("to" beacon)
                // has multiple beacon railroads, the "from" beacon railroad's subdivision railroad ID can be used to find the
                // correct "to" beacon railroad among the multiple beacon railroads.

                toBeaconRailroad = telemetry.Beacon.BeaconRailroads
                    .FirstOrDefault(br => br.Subdivision.RailroadID == existingMapPinToUpdate.BeaconRailroad.Subdivision.RailroadID);

                if (toBeaconRailroad == null)
                {
                    // Check trackage rights rules before discarding due to missing beacon railroad.

                    var trackageRights = await _trackageRightRepository.GetByFromSubdivisionAsync(existingMapPinToUpdate.BeaconRailroad.SubdivisionID);

                    if (trackageRights != null)
                    {
                        var hasRights = trackageRights.Where(tr => telemetry.Beacon.BeaconRailroads.Any(br => br.SubdivisionID == tr.ToSubdivisionID)).FirstOrDefault();

                        if (hasRights != null)
                        {
                            toBeaconRailroad = telemetry.Beacon.BeaconRailroads
                                .Where(br => br.Subdivision.ID == hasRights.ToSubdivisionID)
                                .FirstOrDefault();
                        }
                    }
                }
            }

            var differentBeacon = telemetry.BeaconID != existingMapPinToUpdate.BeaconID;

            // Clone existing map pin to modify.
            var newMapPin = existingMapPinToUpdate.Clone();

            if (differentBeacon)
            {
                // Direction can be calculated since the beacon changed.

                var fromBeaconRailroad = await _beaconRailroadService.GetByIdAsync(existingMapPinToUpdate.BeaconID, existingMapPinToUpdate.SubdivisionId);

                if (fromBeaconRailroad == null)
                {
                    _logger.LogError($"From beacon railroad not found for map pin ID {existingMapPinToUpdate.ID}. MapPin BeaconID: {existingMapPinToUpdate.BeaconID}, SubdivisionID: {existingMapPinToUpdate.SubdivisionId}");

                    return null;
                }

                newMapPin.Direction = CalculateDirection(fromBeaconRailroad, toBeaconRailroad);
                newMapPin.SubdivisionId = toBeaconRailroad.Subdivision.ID;

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

            // Update IsLocal flag based on new subdivision
            newMapPin.IsLocal = IsLocalTrain(telemetry.AddressID, toBeaconRailroad.Subdivision);
            newMapPin.Moving = CalculateMotion(existingMapPinToUpdate, telemetry);

            await this.UpdateBeaconTimestamp(toBeaconRailroad);

            return newMapPin;
        }

        private static string CalculateDirection(BeaconRailroad fromBeaconRailroad, BeaconRailroad toBeaconRailroad)
        {
            var fromGeoCoordinate = new GeoCoordinate(fromBeaconRailroad.Latitude, fromBeaconRailroad.Longitude);
            var toGeoCoordinate = new GeoCoordinate(toBeaconRailroad.Latitude, toBeaconRailroad.Longitude);

            var constrainedDirection = DirectionService.GetDirection(fromGeoCoordinate, toGeoCoordinate, toBeaconRailroad.Direction);

            if (!string.IsNullOrWhiteSpace(constrainedDirection))
            {
                return constrainedDirection;
            }

            // Fallback to unrestricted geometry so direction is still populated when beacon direction
            // constraints are missing/misaligned for an otherwise valid move.
            return DirectionService.GetDirection(fromGeoCoordinate, toGeoCoordinate, Direction.All);
        }

        private static bool? CalculateMotion(MapPin existingMapPin, Telemetry telemetry)
        {
            var sameBeacon = existingMapPin.BeaconID == telemetry.BeaconID;
            var sourceContainsNoMovementInfo = (telemetry.Source == SourceEnum.HOT) || (telemetry.Source == SourceEnum.DPU && telemetry.BrakePipePressure == null);

            if (sameBeacon && sourceContainsNoMovementInfo)
            {
                return existingMapPin.Moving;
            }

            var minimumBrakePSI = 82;

            if (telemetry.Source == SourceEnum.DPU && telemetry.BrakePipePressure.HasValue)
            {
                return telemetry.BrakePipePressure.Value >= minimumBrakePSI;
            }

            if (telemetry.Source == SourceEnum.EOT && telemetry.Moving.HasValue)
            {
                return telemetry.Moving.Value;
            }

            // No brake pipe pressure, no movement status, or moving status is unknown HOT - default to null (unknown).
            return null;
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
