using Web.Server.Entities;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Context object containing all data needed for map pin rule evaluation.
    /// </summary>
    public class MapPinRuleContext
    {
        public required BeaconRailroad FromBeaconRailroad { get; init; }
        public required BeaconRailroad ToBeaconRailroad { get; init; }
    }
}
