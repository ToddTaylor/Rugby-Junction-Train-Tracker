using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Repositories;
using Web.Server.Services.Rules;

namespace Web.Server.Services.Processors
{
    /// <summary>
    /// Processes DPU telemetry: matches by train ID, validates speed/railroad, and returns a candidate map pin.
    /// Delegates create/update/discard validation to MapPinService.
    /// </summary>
    public class DpuMapPinProcessor : IMapPinProcessor
    {
        public string[] SupportedSources => new[] { SourceEnum.DPU };

        public const int TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES = 60;
        public const int TIME_THRESHOLD_DPU_EXACT_MINUTES = 360;
        public const int TIME_THRESHOLD_DPU_SAME_BEACON_TRAIN_ONLY_MAX_AGE_MINUTES = 30;
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

        private readonly IMapPinRepository _mapPinRepository;
        private readonly ILogger<DpuMapPinProcessor> _logger;

        public DpuMapPinProcessor(
            IMapPinRepository mapPinRepository,
            ILogger<DpuMapPinProcessor> logger)
        {
            _mapPinRepository = mapPinRepository;
            _logger = logger;
        }

        public async Task<MapPinProcessingResult> ProcessAsync(Telemetry telemetry)
        {
            var dpuMatch = await ResolveDpuMatchAsync(telemetry);

            switch (dpuMatch.Status)
            {
                case DpuMatchStatus.Discarded:
                    // Discard the telemetry; MapPinService handles the actual discard call
                    return new MapPinProcessingResult
                    {
                        MapPin = null,
                        IsNewMapPin = false,
                        DiscardReason = dpuMatch.DiscardReason ?? "DPU Match Discarded"
                    };

                case DpuMatchStatus.NoMatch:
                    // Return null MapPin to signal MapPinService to create new
                    return new MapPinProcessingResult
                    {
                        MapPin = null,
                        IsNewMapPin = true,
                        DiscardReason = null
                    };

                case DpuMatchStatus.Matched:
                    var existingMapPin = dpuMatch.MatchedMapPin!;
                    AddDpuAddressIfMissing(existingMapPin, telemetry);
                    
                    // Return existing map pin; MapPinService will update it
                    return new MapPinProcessingResult
                    {
                        MapPin = existingMapPin,
                        IsNewMapPin = false,
                        DiscardReason = null
                    };

                default:
                    throw new InvalidOperationException($"Unsupported DPU match status '{dpuMatch.Status}'.");
            }
        }

        private void AddDpuAddressIfMissing(MapPin existingMapPinToUpdate, Telemetry telemetry)
        {
            var existingAddress = existingMapPinToUpdate.Addresses.FirstOrDefault(a =>
                a.AddressID == telemetry.AddressID &&
                a.DpuTrainID == telemetry.TrainID &&
                a.Source == telemetry.Source);

            if (existingAddress != null)
            {
                existingAddress.LastUpdate = telemetry.LastUpdate;
                return;
            }

            existingMapPinToUpdate.Addresses.Add(new Address
            {
                AddressID = telemetry.AddressID,
                DpuTrainID = telemetry.TrainID,
                Source = telemetry.Source,
                CreatedAt = telemetry.LastUpdate,
                LastUpdate = telemetry.LastUpdate
            });
        }

        private async Task<MapPin?> GetDpuMapPinByTrainIdAsync(int trainID)
        {
            return await _mapPinRepository.GetByTrainIdAsync(trainID, TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES);
        }

        private async Task<MapPin?> GetDpuMapPinByAddressAndTrainIdAsync(int addressID, int trainID)
        {
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
            return validation.Status == DpuMatchStatus.Matched
                ? new DpuMatchResult { Status = DpuMatchStatus.Matched, MatchedMapPin = matchedMapPin }
                : validation;
        }

        private Task<DpuMatchResult> ValidateMatchedDpuMapPinAsync(Telemetry telemetry, MapPin existingMapPinByDpuTrainID)
        {
            if (telemetry.BeaconID == existingMapPinByDpuTrainID.BeaconID)
            {
                var age = telemetry.CreatedAt - existingMapPinByDpuTrainID.LastUpdate;
                if (age.TotalMinutes > TIME_THRESHOLD_DPU_SAME_BEACON_TRAIN_ONLY_MAX_AGE_MINUTES)
                {
                    return Task.FromResult(new DpuMatchResult { Status = DpuMatchStatus.NoMatch });
                }
            }

            var notTheSameBeacon = !telemetry.Beacon.BeaconRailroads
                .Any(br => br.BeaconID == existingMapPinByDpuTrainID.BeaconID && br.Subdivision.DpuCapable);

            if (notTheSameBeacon)
            {
                var notTheSameRailroad = !telemetry.Beacon.BeaconRailroads
                    .Any(br => br.Subdivision.RailroadID == existingMapPinByDpuTrainID.BeaconRailroad.Subdivision.RailroadID && br.Subdivision.DpuCapable);

                if (notTheSameRailroad)
                {
                    return Task.FromResult(new DpuMatchResult
                    {
                        Status = DpuMatchStatus.Discarded,
                        DiscardReason = "DPU Invalid Railroad"
                    });
                }

                // Prefer the DPU-capable row on the SAME subdivision as the existing pin, then fall
                // back to same-railroad. A beacon at a junction has several rows on one railroad, and
                // matching on railroad alone would take a milepost from the wrong subdivision
                // (see issue #80).
                var toBeaconRailroad = telemetry.Beacon.BeaconRailroads
                    .FirstOrDefault(br => br.SubdivisionID == existingMapPinByDpuTrainID.BeaconRailroad.SubdivisionID && br.Subdivision.DpuCapable)
                    ?? telemetry.Beacon.BeaconRailroads
                        .FirstOrDefault(br => br.Subdivision.RailroadID == existingMapPinByDpuTrainID.BeaconRailroad.Subdivision.RailroadID && br.Subdivision.DpuCapable);

                if (toBeaconRailroad == null)
                {
                    // The guard above uses a different predicate than this lookup, so a row can pass
                    // there and still not resolve here. Treat as a different train rather than throwing.
                    return Task.FromResult(new DpuMatchResult { Status = DpuMatchStatus.NoMatch });
                }

                var distance = TrainSpeedSanityMath.TryGetDistanceMiles(existingMapPinByDpuTrainID.BeaconRailroad, toBeaconRailroad);

                if (distance == null)
                {
                    // Different subdivisions with no usable coordinates; positions are not comparable.
                    return Task.FromResult(new DpuMatchResult { Status = DpuMatchStatus.Matched });
                }

                var effectiveMilesApart = TrainSpeedSanityMath.GetAdjustedDistanceMiles(distance.Value.Miles);
                var speedMph = TrainSpeedSanityMath.TryGetSpeedMph(effectiveMilesApart, existingMapPinByDpuTrainID.LastUpdate, telemetry.CreatedAt);
                
                if (!speedMph.HasValue)
                {
                    return Task.FromResult(new DpuMatchResult { Status = DpuMatchStatus.Matched });
                }

                if (speedMph.Value > SPEED_THRESHOLD_MPH)
                {
                    return Task.FromResult(new DpuMatchResult { Status = DpuMatchStatus.NoMatch });
                }
            }

            return Task.FromResult(new DpuMatchResult { Status = DpuMatchStatus.Matched });
        }
    }
}
