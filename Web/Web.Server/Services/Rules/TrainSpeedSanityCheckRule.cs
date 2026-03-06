using Web.Server.Repositories;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Rule: Discard telemetry if a train must have traveled at an unrealistic speed (>60 mph) 
    /// between the current beacon and the most recent prior beacon based on their milepost and timestamps.
    /// This is a sanity check to catch GPS errors or data anomalies.
    /// Accounts for beacon radio range and average train length when calculating actual distance traveled.
    /// </summary>
    public class TrainSpeedSanityCheckRule : ITelemetryRule
    {
        public const string DISCARD_REASON = "Train Speed";

        /// <summary> 
        /// Time window in minutes to look back for prior telemetry to compare against. 
        /// A longer window allows for more time to have passed between beacons, which can help catch unrealistic 
        /// speeds over longer distances, but may also allow for more variability in train movement. 
        /// A shorter window is more strict but may miss some unrealistic speeds if the train had a long gap between 
        /// beacons. 30 minutes is a reasonable compromise to allow for some variability in train movement while 
        /// still catching unrealistic speeds. 
        /// </summary>
        public const int TIME_WINDOW_MINUTES = 30;

        /// <summary>
        /// Maximum realistic speed in miles per hour. Speeds above this threshold are considered unrealistic.
        /// </summary>
        private const int MAX_REALISTIC_SPEED_MPH = 50;

        /// <summary>
        /// Radio range for each train's telemetry in miles. The train emits telemetry that is captured by beacons
        /// within this radius, so the actual distance traveled is reduced by this amount from each beacon.
        /// 
        /// Number is doubled for the HOT and EOT both reaching to the beacon.
        /// </summary>
        private const double TRAIN_RADIO_RANGE_MILES = 3.0 * 2;

        /// <summary>
        /// Some trains are shorter, some are longer, but a mile can make a difference in the speed calculation.
        /// </summary>
        private const double TRAIN_LENGTH_IN_MILES = 1;

        private readonly ITelemetryRepository _telemetryRepository;

        public TrainSpeedSanityCheckRule(ITelemetryRepository telemetryRepository)
        {
            _telemetryRepository = telemetryRepository;
        }

        public async Task<TelemetryRuleResult> ShouldDiscardAsync(TelemetryRuleContext context)
        {
            // Get recent telemetry for this address within the time window.
            var minutesAgo = context.Telemetry.CreatedAt.AddMinutes(-TIME_WINDOW_MINUTES);
            var recentTelemetry = await _telemetryRepository
                .GetRecentsWithinTimeOffsetAsync(context.Telemetry.AddressID, context.RailroadId, minutesAgo);

            if (recentTelemetry == null || recentTelemetry.Count <= 1)
            {
                // Need at least two telemetry entries to compare speeds.
                return TelemetryRuleResult.NotDiscarded();
            }

            // The most recent telemetry is the current one being evaluated (first in list).
            var currentTelemetry = recentTelemetry[0];
            var priorTelemetry = recentTelemetry[1];

            // Use passed-in milepost values from context.
            if (double.IsNaN(context.ToMilepost) || double.IsNaN(context.FromMilepost))
            {
                return TelemetryRuleResult.NotDiscarded();
            }

            // Calculate the distance between the two beacons using provided milepost values.
            var distanceMiles = Math.Abs(context.ToMilepost - context.FromMilepost);

            // Account for train radio range and train length
            var adjustedDistance = distanceMiles - TRAIN_RADIO_RANGE_MILES - TRAIN_LENGTH_IN_MILES;

            // Ensure adjusted distance doesn't go negative
            if (adjustedDistance < 0)
            {
                adjustedDistance = 0;
            }

            // Calculate the time difference between the two telemetry readings
            var timeDifference = currentTelemetry.CreatedAt - priorTelemetry.CreatedAt;
            var timeMinutes = timeDifference.TotalMinutes;

            // If time difference is zero or negative, cannot calculate speed
            if (timeMinutes <= 0)
            {
                return TelemetryRuleResult.NotDiscarded();
            }

            // Calculate speed in miles per hour using adjusted distance.
            var timeHours = timeMinutes / 60.0;
            var speedMph = adjustedDistance / timeHours;

            // If speed exceeds realistic threshold, discard
            if (speedMph > MAX_REALISTIC_SPEED_MPH)
            {
                var discardReason = $"{DISCARD_REASON} ({distanceMiles:F0} miles in {timeMinutes:F0} minutes = {speedMph:F0} MPH > {MAX_REALISTIC_SPEED_MPH} MPH)";
                return TelemetryRuleResult.Discarded(discardReason);
            }

            return TelemetryRuleResult.NotDiscarded();
        }
    }
}
