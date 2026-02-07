using Web.Server.Repositories;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Rule: Discard map pin if the from subdivision does not have trackage rights
    /// to the to subdivision when changing railroads.
    /// </summary>
    public class TrackageRightsRule : IMapPinRule
    {
        public const string DISCARD_REASON = "Trackage Rights";

        private readonly ISubdivisionTrackageRightRepository _trackageRightRepository;

        public TrackageRightsRule(ISubdivisionTrackageRightRepository trackageRightRepository)
        {
            _trackageRightRepository = trackageRightRepository;
        }

        public async Task<MapPinRuleResult> ShouldDiscardAsync(MapPinRuleContext context)
        {
            // Get subdivisions from the provided beacon railroads
            var fromSubdivision = context.FromBeaconRailroad?.Subdivision;
            var toSubdivision = context.ToBeaconRailroad?.Subdivision;

            if (fromSubdivision == null || toSubdivision == null)
            {
                // Cannot compare without both subdivisions.
                return MapPinRuleResult.NotDiscarded();
            }

            if (fromSubdivision.RailroadID == toSubdivision.RailroadID)
            {
                // Same railroad is always allowed.
                return MapPinRuleResult.NotDiscarded();
            }

            // Check if from subdivision has rights to the to subdivision
            var trackageRights = await _trackageRightRepository.GetByFromSubdivisionAsync(fromSubdivision.ID);

            if (trackageRights == null || !trackageRights.Any())
            {
                // No rights found, discard.
                return MapPinRuleResult.Discarded(DISCARD_REASON);
            }

            var hasRights = trackageRights.Any(tr => tr.ToSubdivisionID == toSubdivision.ID);

            return hasRights 
                ? MapPinRuleResult.NotDiscarded()
                : MapPinRuleResult.Discarded(DISCARD_REASON);
        }
    }
}
