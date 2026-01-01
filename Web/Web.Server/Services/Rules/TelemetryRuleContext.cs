using Web.Server.Entities;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Context object containing all data needed for telemetry rule evaluation.
    /// </summary>
    public class TelemetryRuleContext
    {
        public required Telemetry Telemetry { get; init; }
        public required ICollection<BeaconRailroad> RailroadBeacons { get; init; }
        public required int RailroadId { get; init; }
    }
}