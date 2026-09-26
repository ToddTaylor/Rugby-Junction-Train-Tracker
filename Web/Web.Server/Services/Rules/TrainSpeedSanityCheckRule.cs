using Web.Server.Entities;
using Web.Server.Repositories;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Rule: Discard telemetry if a train must have traveled at an unrealistic speed between the
    /// current beacon and the most recent prior beacon based on their positions and timestamps.
    /// This is a sanity check to catch GPS errors or data anomalies.
    /// Accounts for beacon radio range and average train length when calculating actual distance traveled.
    ///
    /// Distance is measured by milepost subtraction only when both readings are on the SAME
    /// subdivision; otherwise it falls back to straight-line distance. See
    /// <see cref="TrainSpeedSanityMath.TryGetDistanceMiles"/>.
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

            // Calculate the time difference between the two telemetry readings.
            var timeDifference = currentTelemetry.CreatedAt - priorTelemetry.CreatedAt;
            var timeMinutes = timeDifference.TotalMinutes;

            // If time difference is zero or negative, cannot calculate speed.
            if (timeMinutes <= 0)
            {
                return TelemetryRuleResult.NotDiscarded();
            }

            // Resolve a concrete beacon railroad (beacon + subdivision) for each end. The telemetry
            // record itself carries no subdivision, so identity comes from the rule context where
            // available and is inferred from the beacon's rows otherwise.
            var toBeaconRailroad = ResolveToBeaconRailroad(context, currentTelemetry);
            var fromBeaconRailroad = ResolveFromBeaconRailroad(context, priorTelemetry, toBeaconRailroad);

            if (toBeaconRailroad == null || fromBeaconRailroad == null)
            {
                // Cannot establish a position for one end; never discard on unknown position.
                return TelemetryRuleResult.NotDiscarded();
            }

            var distance = TrainSpeedSanityMath.TryGetDistanceMiles(fromBeaconRailroad, toBeaconRailroad);

            if (distance == null)
            {
                // Different subdivisions with no usable coordinates; the two positions are not comparable.
                return TelemetryRuleResult.NotDiscarded();
            }

            // Account for train radio range and train length.
            var adjustedDistance = TrainSpeedSanityMath.GetAdjustedDistanceMiles(distance.Value.Miles);

            // Calculate speed in miles per hour using adjusted distance.
            var speedMph = TrainSpeedSanityMath.TryGetSpeedMph(adjustedDistance, priorTelemetry.CreatedAt, currentTelemetry.CreatedAt);
            if (!speedMph.HasValue)
            {
                return TelemetryRuleResult.NotDiscarded();
            }

            // If speed exceeds realistic threshold, discard.
            if (speedMph.Value > MAX_REALISTIC_SPEED_MPH)
            {
                // Report the adjusted distance the speed was actually derived from, and name the basis
                // so a discard can be triaged.
                var basis = distance.Value.Basis == DistanceBasis.Milepost ? "milepost" : "straightline";
                var discardReason = $"{DISCARD_REASON} ({adjustedDistance:F0} {basis} miles in {timeMinutes:F0} minutes = {speedMph.Value:F0} MPH > {MAX_REALISTIC_SPEED_MPH} MPH)";
                return TelemetryRuleResult.Discarded(discardReason);
            }

            return TelemetryRuleResult.NotDiscarded();
        }

        /// <summary>
        /// Resolves the beacon railroad for the current reading. The context's "to" end is
        /// authoritative when it refers to the same beacon as the telemetry being evaluated.
        /// </summary>
        private static BeaconRailroad? ResolveToBeaconRailroad(TelemetryRuleContext context, Telemetry currentTelemetry)
        {
            if (context.ToBeaconRailroad != null &&
                context.ToBeaconRailroad.BeaconID == currentTelemetry.BeaconID)
            {
                return context.ToBeaconRailroad;
            }

            return FirstOnRailroad(currentTelemetry, context.RailroadId);
        }

        /// <summary>
        /// Resolves the beacon railroad for the prior reading. Telemetry records no subdivision, so
        /// this walks three tiers in descending order of reliability.
        /// </summary>
        private static BeaconRailroad? ResolveFromBeaconRailroad(
            TelemetryRuleContext context,
            Telemetry priorTelemetry,
            BeaconRailroad? toBeaconRailroad)
        {
            // 1. The map pin's own beacon railroad, when it refers to the same beacon as the prior
            //    reading. MapPin.SubdivisionId is persisted, making this the strongest signal for
            //    which subdivision the train was actually on.
            if (context.FromBeaconRailroad != null &&
                context.FromBeaconRailroad.BeaconID == priorTelemetry.BeaconID)
            {
                return context.FromBeaconRailroad;
            }

            // 2. The prior beacon's row on the SAME subdivision as the current reading. Trains
            //    normally stay on one subdivision, and this keeps the comparison in milepost space,
            //    which is more accurate than straight-line distance.
            if (toBeaconRailroad != null)
            {
                var sameSubdivision = priorTelemetry.Beacon?.BeaconRailroads?
                    .FirstOrDefault(br => br.SubdivisionID == toBeaconRailroad.SubdivisionID);

                if (sameSubdivision != null)
                {
                    return sameSubdivision;
                }
            }

            // 3. Any row on the railroad. The subdivisions will differ, so the distance falls back to
            //    straight line. That is tolerant of picking the "wrong" row here: a beacon's rows for
            //    different subdivisions sit within a few hundred feet of each other even when their
            //    mileposts differ by a hundred miles or more.
            return FirstOnRailroad(priorTelemetry, context.RailroadId);
        }

        private static BeaconRailroad? FirstOnRailroad(Telemetry telemetry, int railroadId)
        {
            return telemetry.Beacon?.BeaconRailroads?
                .FirstOrDefault(br => br.Subdivision?.RailroadID == railroadId);
        }
    }
}
