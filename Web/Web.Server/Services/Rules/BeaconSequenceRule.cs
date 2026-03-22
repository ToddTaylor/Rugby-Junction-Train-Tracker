using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Rule: Discard map pin if the beacon jump skips intermediate beacons on the same railroad.
    /// 
    /// Prevents "phantom beacon" scenarios where a hardware malfunction causes a distant beacon
    /// to detect a train prematurely, stealing the pin from intermediate beacons that should
    /// naturally detect the train first.
    /// 
    /// Example: Train at FOND DU LAC (157.26), NEENAH (184.8) falsely detects it.
    /// Intermediate beacons NORTH FOND DU LAC (160.53), OSHKOSH (172.8) exist between them.
    /// This rule discards the NEENAH detection because a ~28-mile jump skips intermediates.
    /// </summary>
    public class BeaconSequenceRule : IMapPinRule
    {
        /// <summary>
        /// Minimum distance (miles) between beacons for the rule to apply.
        /// Gaps under 15 miles are considered naturally adjacent and do not trigger the rule.
        /// </summary>
        public const double SEQUENCE_GAP_THRESHOLD_MILES = 15.0;

        /// <summary>
        /// Skip (do not count) intermediate beacons that have not been heard from in this many hours.
        /// Allows the rule to bypass offline/dead beacons that are blocking legitimate train movements.
        /// </summary>
        public const int OFFLINE_BEACON_THRESHOLD_HOURS = 24;

        public const string DISCARD_REASON = "Beacon Sequence Skip";

        private readonly IBeaconRailroadRepository _beaconRailroadRepository;
        private readonly ITimeProvider _timeProvider;

        public BeaconSequenceRule(IBeaconRailroadRepository beaconRailroadRepository, ITimeProvider timeProvider)
        {
            _beaconRailroadRepository = beaconRailroadRepository;
            _timeProvider = timeProvider;
        }

        public async Task<MapPinRuleResult> ShouldDiscardAsync(MapPinRuleContext context)
        {
            var fromMilepost = context.FromBeaconRailroad.Milepost;
            var toMilepost   = context.ToBeaconRailroad.Milepost;
            var rawDistance  = Math.Abs(toMilepost - fromMilepost);

            // Only apply to significant gaps.
            if (rawDistance <= SEQUENCE_GAP_THRESHOLD_MILES)
                return MapPinRuleResult.NotDiscarded();

            // Only check within the same railroad (may span multiple subdivisions).
            var fromRailroadId = context.FromBeaconRailroad.Subdivision.RailroadID;
            var toRailroadId   = context.ToBeaconRailroad.Subdivision.RailroadID;
            
            if (fromRailroadId != toRailroadId)
                return MapPinRuleResult.NotDiscarded();

            var minMilepost = Math.Min(fromMilepost, toMilepost);
            var maxMilepost = Math.Max(fromMilepost, toMilepost);

            // Only count active (recently heard from) intermediate beacons.
            var activeAfterUtc = _timeProvider.UtcNow.AddHours(-OFFLINE_BEACON_THRESHOLD_HOURS);

            // Query all beacons on the railroad (regardless of subdivision) between the two mileposts,
            // excluding offline beacons that haven't been heard from in 24+ hours.
            var intermediates = await _beaconRailroadRepository
                .GetByRailroadBetweenMilepostsAsync(fromRailroadId, minMilepost, maxMilepost, activeAfterUtc);

            if (intermediates.Any())
                return MapPinRuleResult.Discarded(
                    $"{DISCARD_REASON} ({rawDistance:F0} miles, {intermediates.Count()} skipped beacon(s))");

            return MapPinRuleResult.NotDiscarded();
        }
    }
}
