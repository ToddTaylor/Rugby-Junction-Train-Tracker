namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Rule: Discard a map pin if a train must have traveled at an unrealistic speed (>35 mph) 
    /// between the previous beacon and the current beacon based on their coordinates and timestamps.
    /// This is a sanity check to catch GPS errors or data anomalies.
    /// </summary>
    public class TrainSpeedSanityCheckRule : IMapPinRule
    {
        public const string DISCARD_REASON = "Train Speed Sanity Check";
        private const int MAX_REALISTIC_SPEED_MPH = 35;

        public Task<MapPinRuleResult> ShouldDiscardAsync(MapPinRuleContext context)
        {
            // Calculate the distance between the two beacons in miles using Haversine formula
            var from = new Entities.GeoCoordinate(context.FromBeaconRailroad.Latitude, context.FromBeaconRailroad.Longitude);
            var to = new Entities.GeoCoordinate(context.ToBeaconRailroad.Latitude, context.ToBeaconRailroad.Longitude);
            var distanceMiles = DistanceService.GetDistanceBetweenInMiles(from, to);

            // Calculate the time difference between the two beacon readings
            var timeDifference = context.ToBeaconRailroad.LastUpdate - context.FromBeaconRailroad.LastUpdate;
            var timeMinutes = timeDifference.TotalMinutes;

            // If time difference is zero or negative, cannot calculate speed
            if (timeMinutes <= 0)
            {
                return Task.FromResult(MapPinRuleResult.NotDiscarded());
            }

            // Calculate speed in miles per hour
            var timeHours = timeMinutes / 60.0;
            var speedMph = (double)distanceMiles / timeHours;

            // If speed exceeds realistic threshold, discard
            if (speedMph > MAX_REALISTIC_SPEED_MPH)
            {
                return Task.FromResult(MapPinRuleResult.Discarded(DISCARD_REASON));
            }

            return Task.FromResult(MapPinRuleResult.NotDiscarded());
        }
    }
}
