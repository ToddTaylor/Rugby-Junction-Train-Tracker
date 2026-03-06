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
        /// Milepost value for the current ("to") beacon.
        /// </summary>
        public double ToMilepost { get; init; }

        /// <summary>
        /// Milepost value for the prior ("from") beacon.
        /// </summary>
        public double FromMilepost { get; init; }
    }
}