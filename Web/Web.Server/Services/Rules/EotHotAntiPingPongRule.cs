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
        private const int TIME_WINDOW_MINUTES = 60;

        private readonly ITelemetryRepository _telemetryRepository;

        public EotHotAntiPingPongRule(ITelemetryRepository telemetryRepository)
        {
            _telemetryRepository = telemetryRepository;
        }

        public async Task<bool> ShouldDiscardAsync(TelemetryRuleContext context)
        {
            // Only applies to EOT or HOT telemetry.
            if (context.Telemetry.Source != SourceEnum.EOT && context.Telemetry.Source != SourceEnum.HOT)
            {
                return false;
            }

            var minutesAgo = context.Telemetry.CreatedAt.AddMinutes(-TIME_WINDOW_MINUTES);

            // Check if address ID exists at another beacon within alloted time.
            var recentTelemetry = await _telemetryRepository
                .GetRecentsWithinTimeOffsetAsync(context.Telemetry.AddressID, context.RailroadId, minutesAgo);

            var atLeastTwoRecentTelemetry = recentTelemetry.Any() && recentTelemetry.Count >= 2;

            if (!atLeastTwoRecentTelemetry)
            {
                return false;
            }

            var newestLoggedTelemetry = recentTelemetry.First();
            var oldestLoggedTelemetry = recentTelemetry.Last();

            var trainSwitchingBeacons = newestLoggedTelemetry.BeaconID != oldestLoggedTelemetry.BeaconID;
            var mostRecentBeaconNotNewBeacon = newestLoggedTelemetry.BeaconID != context.Telemetry.BeaconID;

            // Discard if the train is switching beacons and trying to return to a previous beacon.
            return trainSwitchingBeacons && mostRecentBeaconNotNewBeacon;
        }
    }
}