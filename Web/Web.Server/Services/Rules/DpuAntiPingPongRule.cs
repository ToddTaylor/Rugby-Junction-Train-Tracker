using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Repositories;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Rule: Discard DPU telemetry if the train was recently seen at another beacon
    /// and is now attempting to "ping-pong" back to a previous beacon within the time window.
    /// </summary>
    public class DpuAntiPingPongRule : ITelemetryRule
    {
        public const int TIME_WINDOW_MINUTES = 30;

        public const string DISCARD_REASON = "DPU Ping-Pong";

        private readonly ITelemetryRepository _telemetryRepository;

        public DpuAntiPingPongRule(ITelemetryRepository telemetryRepository)
        {
            _telemetryRepository = telemetryRepository;
        }

        public async Task<TelemetryRuleResult> ShouldDiscardAsync(TelemetryRuleContext context)
        {
            // Only applies to DPU telemetry with a TrainID
            if (context.Telemetry.Source != SourceEnum.DPU || !context.Telemetry.TrainID.HasValue)
            {
                return TelemetryRuleResult.NotDiscarded();
            }

            var minutesAgo = context.Telemetry.CreatedAt.AddMinutes(-TIME_WINDOW_MINUTES);

            // Get recent telemetry for this train within the time window.
            var recentTelemetry = await _telemetryRepository
                .GetRecentsForTrainWithinTimeOffsetAsync(context.Telemetry.TrainID.Value, context.RailroadId, minutesAgo);

            if (recentTelemetry == null || recentTelemetry.Count <= 1)
            {
                // Only zero or one previous telemetry exists, so no ping-pong possible.
                return TelemetryRuleResult.NotDiscarded();
            }

            recentTelemetry.RemoveAt(0); // Remove the most recent telemetry which is the current one being evaluated.
            recentTelemetry.RemoveAll(t => t.Discarded == true);

            if (await BeaconIdAlreadyUsed(context.Telemetry.BeaconID, recentTelemetry[0].BeaconID, recentTelemetry))
            {
                // Discard the telemetry as it is ping-ponging back to a previous beacon.
                return TelemetryRuleResult.Discarded(DISCARD_REASON);
            }

            // No ping-pong detected.
            return TelemetryRuleResult.NotDiscarded();
        }

        private async Task<bool> BeaconIdAlreadyUsed(int newBeaconId, int lastBeaconId, List<Telemetry> recentTelemetry)
        {
            if (newBeaconId == lastBeaconId)
            {
                // Same beacon, no ping-pong.
                return false;
            }

            foreach (var entry in recentTelemetry)
            {
                if (entry.BeaconID == newBeaconId)
                {
                    // Beacon already exists in recent history.
                    return true;
                }

                if (entry.BeaconID != lastBeaconId)
                {
                    // Stop checking, a different beacon hit.
                    break;
                }
            }

            // Beacon not found in recent history.
            return false;
        }
    }
}