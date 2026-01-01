namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Evaluates telemetry against a collection of rules to determine if it should be discarded.
    /// </summary>
    public class TelemetryRuleEngine
    {
        private readonly IEnumerable<ITelemetryRule> _rules;

        public TelemetryRuleEngine(IEnumerable<ITelemetryRule> rules)
        {
            _rules = rules;
        }

        /// <summary>
        /// Evaluates all rules and returns true if any rule determines the telemetry should be discarded.
        /// </summary>
        public async Task<bool> ShouldDiscardAsync(TelemetryRuleContext context)
        {
            foreach (var rule in _rules)
            {
                if (await rule.ShouldDiscardAsync(context))
                {
                    return true;
                }
            }

            return false;
        }
    }
}