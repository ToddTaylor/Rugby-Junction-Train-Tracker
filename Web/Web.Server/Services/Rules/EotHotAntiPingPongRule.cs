using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Repositories;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Rule: Discard EOT/HOT telemetry if the train was recently seen at another beacon
    /// and is now attempting to "ping-pong" back to a previous beacon within 5 minutes.
    /// </summary>
    public class EotHotAntiPingPongRule : ITelemetryRule
    {
        public const int TIME_WINDOW_MINUTES = 30;

        private const string DISCARD_REASON = "EOT/HOT Ping-Pong";

        private readonly ITelemetryRepository _telemetryRepository;

        public EotHotAntiPingPongRule(ITelemetryRepository telemetryRepository)
        {
            _telemetryRepository = telemetryRepository;
        }

        public async Task<TelemetryRuleResult> ShouldDiscardAsync(TelemetryRuleContext context)
        {
            if (context.Telemetry.Source != SourceEnum.EOT && context.Telemetry.Source != SourceEnum.HOT)
            {
                return TelemetryRuleResult.NotDiscarded();
            }

            var minutesAgo = context.Telemetry.CreatedAt.AddMinutes(-TIME_WINDOW_MINUTES);

            // Get recent telemetry for this address within the time window.
            var recentTelemetry = await _telemetryRepository
                .GetRecentsWithinTimeOffsetAsync(context.Telemetry.AddressID, context.RailroadId, minutesAgo);

            if (recentTelemetry == null || recentTelemetry.Count <= 1)
            {
                // Only zero or one previous telemetry exists, so no ping-pong possible.
                return TelemetryRuleResult.NotDiscarded();
            }

            if (await beaconIdAlreadyUsed(context.Telemetry.BeaconID, recentTelemetry[0].BeaconID, recentTelemetry))
            {
                // Discard the telemetry as it is ping-ponging back to a previous beacon.
                return TelemetryRuleResult.Discarded(DISCARD_REASON);
            }

            // Discard if the train is switching beacons and trying to return to a previous beacon.
            return TelemetryRuleResult.NotDiscarded();
        }

        private async Task<bool> beaconIdAlreadyUsed(int newBeaconId, int lastBeaconId, List<Telemetry> recentTelemetry)
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