using Web.Server.Enums;
using Web.Server.Repositories;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Rule: Discard DPU telemetry if the train was recently seen at another beacon
    /// and is now attempting to "ping-pong" back to a previous beacon within 5 minutes.
    /// </summary>
    public class DpuAntiPingPongRule : ITelemetryRule
    {
        private const int TIME_WINDOW_MINUTES = 60;

        private readonly ITelemetryRepository _telemetryRepository;

        public DpuAntiPingPongRule(ITelemetryRepository telemetryRepository)
        {
            _telemetryRepository = telemetryRepository;
        }

        public async Task<bool> ShouldDiscardAsync(TelemetryRuleContext context)
        {
            // Only applies to DPU telemetry with a TrainID
            if (context.Telemetry.Source != SourceEnum.DPU || !context.Telemetry.TrainID.HasValue)
            {
                return false;
            }

            var minutesAgo = context.Telemetry.CreatedAt.AddMinutes(-TIME_WINDOW_MINUTES);

            // Get most recent telemetry for this train at this beacon (currentBeacon) within alloted time.
            var currentBeacon = await _telemetryRepository.GetRecentWithinTimeOffsetAsync(
                context.Telemetry.TrainID.Value,
                context.Telemetry.BeaconID,
                context.RailroadId,
                minutesAgo);

            if (currentBeacon == null)
            {
                return false;
            }

            // Get most recent telemetry for this train at any other beacon (previousBeacon) within alloted time.
            var previousBeacon = await _telemetryRepository.GetRecentForOtherBeaconWithinTimeOffsetAsync(
                context.Telemetry.TrainID.Value,
                context.Telemetry.BeaconID,
                context.RailroadId,
                minutesAgo);

            if (previousBeacon == null)
            {
                return false;
            }

            // If previousBeacon is more recent than currentBeacon, discard (ping-pong detected).
            return previousBeacon.CreatedAt > currentBeacon.CreatedAt;
        }
    }
}