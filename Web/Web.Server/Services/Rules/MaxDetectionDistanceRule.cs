namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Rule: Discard map pin if the distance between from and to beacons exceeds
    /// the to beacon's configured MaxDetectionDistanceMiles limit (Failure Mode B prevention).
    /// Only applies to same railroad movements.
    /// </summary>
    public class MaxDetectionDistanceRule : IMapPinRule
    {
        public const string DISCARD_REASON = "Max Detection Distance";

        public async Task<MapPinRuleResult> ShouldDiscardAsync(MapPinRuleContext context)
        {
            // Get the to beacon's max detection distance setting
            var toBeaconRailroad = context.ToBeaconRailroad;

            if (toBeaconRailroad == null)
            {
                return MapPinRuleResult.NotDiscarded();
            }

            // If max detection distance is not set, no limit applies
            if (toBeaconRailroad.MaxDetectionDistanceMiles == null)
            {
                return MapPinRuleResult.NotDiscarded();
            }

            var fromBeaconRailroad = context.FromBeaconRailroad;

            if (fromBeaconRailroad == null)
            {
                return MapPinRuleResult.NotDiscarded();
            }

            // Only enforce for same railroad movements
            // Different railroads are legitimate moves, not "steals"
            if (fromBeaconRailroad.Subdivision?.RailroadID != toBeaconRailroad.Subdivision?.RailroadID)
            {
                return MapPinRuleResult.NotDiscarded();
            }

            // Calculate distance between beacons using milepost difference
            var distance = Math.Abs(fromBeaconRailroad.Milepost - toBeaconRailroad.Milepost);
            var maxDistance = toBeaconRailroad.MaxDetectionDistanceMiles.Value;

            if (distance > maxDistance)
            {
                var discardReason = string.Format("{0} ({1:F1} mi > {2:F1} mi limit)", 
                    DISCARD_REASON, distance, maxDistance);
                return MapPinRuleResult.Discarded(discardReason);
            }

            return MapPinRuleResult.NotDiscarded();
        }
    }
}
