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
        public const int TIME_WINDOW_MINUTES = 15;

        private readonly ITelemetryRepository _telemetryRepository;

        public EotHotAntiPingPongRule(ITelemetryRepository telemetryRepository)
        {
            _telemetryRepository = telemetryRepository;
        }

        public async Task<bool> ShouldDiscardAsync(TelemetryRuleContext context)
        {
            if (context.Telemetry.Source != SourceEnum.EOT && context.Telemetry.Source != SourceEnum.HOT)
            {
                return false;
            }

            var minutesAgo = context.Telemetry.CreatedAt.AddMinutes(-TIME_WINDOW_MINUTES);

            // Check to see if there is any recent telemetry for this address within the time window.
            var recentTelemetry = await _telemetryRepository
                .GetRecentsWithinTimeOffsetAsync(context.Telemetry.AddressID, context.RailroadId, minutesAgo);

            var atLeastTwoRecentTelemetry = recentTelemetry.Any() && recentTelemetry.Count >= 2;

            if (!atLeastTwoRecentTelemetry)
            {
                return false;
            }

            var firstNewestLoggedTelemetry = recentTelemetry[0];

            var trainNotSwitchingBeacons = context.Telemetry.BeaconID == firstNewestLoggedTelemetry.BeaconID;

            if (trainNotSwitchingBeacons)
            {
                return false;
            }

            var secondNewestLoggedTelemetry = recentTelemetry[1];

            var trainPingPongingBackToBeacon = context.Telemetry.BeaconID == secondNewestLoggedTelemetry.BeaconID;

            // Discard if the train is switching beacons and trying to return to a previous beacon.
            return trainPingPongingBackToBeacon;
        }
    }
}