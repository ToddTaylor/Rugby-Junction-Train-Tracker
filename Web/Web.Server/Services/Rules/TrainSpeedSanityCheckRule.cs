namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Rule: Discard a map pin if a train must have traveled at an unrealistic speed (>35 mph) 
    /// between the previous beacon and the current beacon based on their coordinates and timestamps.
    /// This is a sanity check to catch GPS errors or data anomalies.
    /// Accounts for beacon radio range when calculating actual distance traveled.
    /// </summary>
    public class TrainSpeedSanityCheckRule : IMapPinRule
    {
        public const string DISCARD_REASON = "Train Speed Sanity Check";

        /// <summary>
        /// Maximum realistic speed in miles per hour. Speeds above this threshold are considered unrealistic.
        /// </summary>
        private const int MAX_REALISTIC_SPEED_MPH = 60;

        /// <summary>
        /// Radio range for each beacon in miles. Each beacon can detect trains
        /// within this radius, so the actual distance traveled is reduced by
        /// this amount from each beacon.
        /// </summary>
        private const double BEACON_RADIO_RANGE_MILES = 5.0;

        public Task<MapPinRuleResult> ShouldDiscardAsync(MapPinRuleContext context)
        {
            // Calculate the distance between the two beacons in miles using Haversine formula
            var from = new Entities.GeoCoordinate(context.FromBeaconRailroad.Latitude, context.FromBeaconRailroad.Longitude);
            var to = new Entities.GeoCoordinate(context.ToBeaconRailroad.Latitude, context.ToBeaconRailroad.Longitude);
            var distanceMiles = DistanceService.GetDistanceBetweenInMiles(from, to);

            // Account for beacon radio range: each beacon can detect trains within
            // BEACON_RADIO_RANGE_MILES radius, so subtract the range from both beacons.
            // For example, if beacons are 15 miles apart with 5-mile range each,
            // the actual distance traveled is only 15 - 5 - 5 = 5 miles.
            var adjustedDistance = (double)distanceMiles - (2 * BEACON_RADIO_RANGE_MILES);

            // Ensure adjusted distance doesn't go negative
            if (adjustedDistance < 0)
            {
                adjustedDistance = 0;
            }

            // Calculate the time difference between the two beacon readings
            var timeDifference = context.ToBeaconRailroad.LastUpdate - context.FromBeaconRailroad.LastUpdate;
            var timeMinutes = timeDifference.TotalMinutes;

            // If time difference is zero or negative, cannot calculate speed
            if (timeMinutes <= 0)
            {
                return Task.FromResult(MapPinRuleResult.NotDiscarded());
            }

            // Calculate speed in miles per hour using adjusted distance
            var timeHours = timeMinutes / 60.0;
            var speedMph = adjustedDistance / timeHours;

            // If speed exceeds realistic threshold, discard
            if (speedMph > MAX_REALISTIC_SPEED_MPH)
            {
                return Task.FromResult(MapPinRuleResult.Discarded(DISCARD_REASON));
            }

            return Task.FromResult(MapPinRuleResult.NotDiscarded());
        }
    }
}
