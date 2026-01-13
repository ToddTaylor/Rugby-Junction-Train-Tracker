using Web.Server.Repositories;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Rule: Discard telemetry if the previous subdivision does not have trackage rights
    /// to the current subdivision when changing railroads.
    /// </summary>
    public class TrackageRightsRule : ITelemetryRule
    {
        private const string DISCARD_REASON = "Trackage Rights";

        private readonly ITelemetryRepository _telemetryRepository;
        private readonly ISubdivisionTrackageRightRepository _trackageRightRepository;

        public TrackageRightsRule(
            ITelemetryRepository telemetryRepository,
            ISubdivisionTrackageRightRepository trackageRightRepository)
        {
            _telemetryRepository = telemetryRepository;
            _trackageRightRepository = trackageRightRepository;
        }

        public async Task<TelemetryRuleResult> ShouldDiscardAsync(TelemetryRuleContext context)
        {
            // Determine the current subdivision from the beacon's railroad mapping
            var currentSubdivision = context.RailroadBeacons
                .Select(br => br.Subdivision)
                .FirstOrDefault(sub => sub != null);

            if (currentSubdivision == null)
            {
                // No current subdivision to compare.
                return TelemetryRuleResult.NotDiscarded();
            }

            // Get the most recent prior telemetry for this address (non-discarded)
            var previousTelemetry = await _telemetryRepository.GetMostRecentByAddressAsync(context.Telemetry.AddressID);
            if (previousTelemetry == null)
            {
                // No prior telemetry to compare.
                return TelemetryRuleResult.NotDiscarded();
            }

            var previousSubdivision = previousTelemetry.Beacon?.BeaconRailroads?
                .Select(br => br.Subdivision)
                .FirstOrDefault(sub => sub != null);

            if (previousSubdivision == null)
            {
                // No prior subdivision to compare.
                return TelemetryRuleResult.NotDiscarded();
            }

            if (previousSubdivision.RailroadID == currentSubdivision.RailroadID)
            {
                // Same railroad is always allowed.
                return TelemetryRuleResult.NotDiscarded();
            }

            // Check if previous subdivision has rights to the current subdivision
            var trackageRights = await _trackageRightRepository.GetByFromSubdivisionAsync(previousSubdivision.ID);

            if (trackageRights == null)
            {
                // No rights found, allow by default.
                return TelemetryRuleResult.NotDiscarded();
            }

            var hasRights = trackageRights.Any(tr => tr.ToSubdivisionID == currentSubdivision.ID);

            if (hasRights)
            {
                return TelemetryRuleResult.NotDiscarded();
            }
            else
            {
                return TelemetryRuleResult.Discarded(DISCARD_REASON);
            }
        }
    }
}
