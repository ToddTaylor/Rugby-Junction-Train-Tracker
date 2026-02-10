namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Evaluates telemetry against a collection of rules to determine if it should be discarded.
    /// </summary>
    public class TelemetryRuleEngine : ITelemetryRuleEngine
    {
        private readonly IEnumerable<ITelemetryRule> _rules;

        public TelemetryRuleEngine(IEnumerable<ITelemetryRule> rules)
        {
            _rules = rules;
        }

        /// <summary>
        /// Evaluates all rules and returns a result with discard status and reason.
        /// </summary>
        public async Task<TelemetryRuleResult> ShouldDiscardAsync(TelemetryRuleContext context)
        {
            foreach (var rule in _rules)
            {
                var result = await rule.ShouldDiscardAsync(context);
                if (result.ShouldDiscard)
                {
                    return result;
                }
            }
            return TelemetryRuleResult.NotDiscarded();
        }
    }
}