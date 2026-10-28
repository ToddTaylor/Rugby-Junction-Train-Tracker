using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Repositories;
using Web.Server.Services.Rules;

namespace Web.Server.Services.Processors
{
    /// <summary>
    /// Processes HOT (head-of-train) and EOT (end-of-train) telemetry: finds the existing map pin for
    /// the address, adds the address if missing, and returns a candidate map pin.
    /// Delegates create/update/discard validation to MapPinService.
    ///
    /// More than one train on a railroad can report the same address ID. Each map pin holding the
    /// address is a candidate, and the telemetry goes to one whose train could have reached this
    /// beacon in time. When none could, the telemetry is held as a possible second train, and a new
    /// map pin is created once a second reading agrees with it.
    /// </summary>
    public class HotEotMapPinProcessor : IMapPinProcessor
    {
        /// <summary>
        /// Discard reason for telemetry that no existing map pin could have reached. It is held until
        /// another reading confirms a second train, so a single garbled address cannot create a pin.
        /// </summary>
        public const string PENDING_SECOND_TRAIN_REASON = "Possible Second Train (pending)";

        public string[] SupportedSources => new[] { SourceEnum.HOT, SourceEnum.EOT };

        private readonly IMapPinRepository _mapPinRepository;
        private readonly ITelemetryRepository _telemetryRepository;
        private readonly ILogger<HotEotMapPinProcessor> _logger;

        public HotEotMapPinProcessor(
            IMapPinRepository mapPinRepository,
            ITelemetryRepository telemetryRepository,
            ILogger<HotEotMapPinProcessor> logger)
        {
            _mapPinRepository = mapPinRepository;
            _telemetryRepository = telemetryRepository;
            _logger = logger;
        }

        public async Task<MapPinProcessingResult> ProcessAsync(Telemetry telemetry)
        {
            var candidates = await _mapPinRepository.GetAllByAddressIdAsync(telemetry.AddressID) ?? [];

            if (candidates.Count == 0)
            {
                // Return null MapPin to signal MapPinService to create new
                return new MapPinProcessingResult
                {
                    MapPin = null,
                    IsNewMapPin = true,
                    DiscardReason = null
                };
            }

            var existingMapPin = SelectReachableMapPin(telemetry, candidates);

            if (existingMapPin != null)
            {
                // Existing map pin found by address
                AddHotOrEotAddressIfMissing(existingMapPin, telemetry);

                // Return existing map pin; MapPinService will update it
                return new MapPinProcessingResult
                {
                    MapPin = existingMapPin,
                    IsNewMapPin = false,
                    DiscardReason = null
                };
            }

            // No train with this address could have reached this beacon in time. Either the reading
            // is bad or a second train is using the address.
            var pendingTelemetry = await FindConfirmingPendingTelemetryAsync(telemetry);

            if (pendingTelemetry == null)
            {
                return new MapPinProcessingResult
                {
                    MapPin = null,
                    IsNewMapPin = false,
                    DiscardReason = PENDING_SECOND_TRAIN_REASON
                };
            }

            _logger.LogInformation(
                "Second train confirmed for address {AddressID}: telemetry {PendingTelemetryID} at beacon {PendingBeaconID} and {TelemetryID} at beacon {BeaconID}.",
                telemetry.AddressID, pendingTelemetry.ID, pendingTelemetry.BeaconID, telemetry.ID, telemetry.BeaconID);

            return new MapPinProcessingResult
            {
                MapPin = null,
                IsNewMapPin = true,
                DiscardReason = null,
                SeedTelemetry = pendingTelemetry
            };
        }

        /// <summary>
        /// Picks the most recently updated candidate map pin whose train could have reached this beacon
        /// in time. A pin already at this beacon always qualifies. Newest wins over same-beacon so that
        /// a stale pin left at this beacon does not take telemetry from the train actually arriving.
        /// Returns null when no candidate could have.
        /// </summary>
        private static MapPin? SelectReachableMapPin(Telemetry telemetry, List<MapPin> candidates)
        {
            // Candidates are ordered newest first.
            return candidates.FirstOrDefault(mp =>
                mp.BeaconID == telemetry.BeaconID ||
                IsReachable(mp.BeaconRailroad, mp.LastUpdate, telemetry));
        }

        /// <summary>
        /// Finds a recent pending second-train reading for the same address that is consistent with
        /// this one: at the same beacon, or close enough that one train could have made both.
        /// </summary>
        private async Task<Telemetry?> FindConfirmingPendingTelemetryAsync(Telemetry telemetry)
        {
            var sinceUtc = telemetry.CreatedAt.AddMinutes(-TrainSpeedSanityCheckRule.TIME_WINDOW_MINUTES);
            var pending = await _telemetryRepository
                .GetRecentsDiscardedForAddressAsync(telemetry.AddressID, PENDING_SECOND_TRAIN_REASON, sinceUtc) ?? [];

            return pending.FirstOrDefault(p =>
                p.ID != telemetry.ID &&
                (p.BeaconID == telemetry.BeaconID ||
                 IsReachable(ResolvePendingBeaconRailroad(p, telemetry), p.CreatedAt, telemetry)));
        }

        /// <summary>
        /// Whether a train last seen at <paramref name="from"/> could have reached the telemetry's
        /// beacon by the time of the telemetry. Unknown positions count as reachable.
        /// </summary>
        private static bool IsReachable(BeaconRailroad? from, DateTime fromUtc, Telemetry telemetry)
        {
            var toBeaconRailroads = telemetry.Beacon?.BeaconRailroads;
            if (from == null || toBeaconRailroads == null)
            {
                return true;
            }

            // Prefer the row on the same subdivision so the distance is measured in mileposts, then
            // the same railroad, as MapPinService does when moving a pin.
            var to = toBeaconRailroads.FirstOrDefault(br => br.SubdivisionID == from.SubdivisionID)
                ?? toBeaconRailroads.FirstOrDefault(br => br.Subdivision?.RailroadID == from.Subdivision?.RailroadID)
                ?? toBeaconRailroads.FirstOrDefault();

            if (to == null)
            {
                return true;
            }

            return TrainSpeedSanityMath.IsReachable(from, fromUtc, to, telemetry.CreatedAt, MapPinService.SPEED_THRESHOLD_MPH);
        }

        /// <summary>
        /// Telemetry records no subdivision, so the pending reading's position is taken from its
        /// beacon's row on a subdivision shared with the current beacon, if any.
        /// </summary>
        private static BeaconRailroad? ResolvePendingBeaconRailroad(Telemetry pending, Telemetry telemetry)
        {
            var pendingBeaconRailroads = pending.Beacon?.BeaconRailroads;
            if (pendingBeaconRailroads == null)
            {
                return null;
            }

            var currentSubdivisionIds = telemetry.Beacon?.BeaconRailroads?.Select(br => br.SubdivisionID).ToHashSet() ?? [];

            return pendingBeaconRailroads.FirstOrDefault(br => currentSubdivisionIds.Contains(br.SubdivisionID))
                ?? pendingBeaconRailroads.FirstOrDefault();
        }

        private void AddHotOrEotAddressIfMissing(MapPin existingMapPinToUpdate, Telemetry telemetry)
        {
            var matchingAddress = existingMapPinToUpdate.Addresses.FirstOrDefault(a =>
                a.AddressID == telemetry.AddressID &&
                a.DpuTrainID == null &&
                a.Source == telemetry.Source);

            if (matchingAddress != null)
            {
                matchingAddress.LastUpdate = telemetry.LastUpdate;
                return;
            }

            existingMapPinToUpdate.Addresses.Add(new Address
            {
                AddressID = telemetry.AddressID,
                DpuTrainID = null,  // HOT/EOT sources have no train ID tracking
                Source = telemetry.Source,
                CreatedAt = telemetry.LastUpdate,
                LastUpdate = telemetry.LastUpdate
            });
        }
    }
}
