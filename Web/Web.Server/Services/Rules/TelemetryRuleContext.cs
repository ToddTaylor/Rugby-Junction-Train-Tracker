using Web.Server.Entities;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Context object containing all data needed for telemetry rule evaluation.
    /// </summary>
    public class TelemetryRuleContext
    {
        public required Telemetry Telemetry { get; init; }
        public required int RailroadId { get; init; }

        /// <summary>
        /// Resolved beacon railroad (beacon + subdivision) for the current ("to") reading.
        /// Carries the subdivision identity, not just a milepost, because mileposts are only
        /// comparable within a single subdivision (see issue #80).
        /// </summary>
        public BeaconRailroad? ToBeaconRailroad { get; init; }

        /// <summary>
        /// Resolved beacon railroad (beacon + subdivision) for the prior ("from") reading,
        /// i.e. the subdivision the existing map pin was on.
        /// </summary>
        public BeaconRailroad? FromBeaconRailroad { get; init; }
    }
}
