namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Represents a rule that determines whether telemetry should be discarded.
    /// </summary>
    public interface ITelemetryRule
    {
        /// <summary>
        /// Evaluates whether the telemetry should be discarded.
        /// </summary>
        /// <param name="context">The context containing telemetry and related data.</param>
        /// <returns>True if telemetry should be discarded, otherwise false.</returns>
        Task<bool> ShouldDiscardAsync(TelemetryRuleContext context);
    }
}