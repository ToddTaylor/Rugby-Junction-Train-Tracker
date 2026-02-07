namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Evaluates map pins against a collection of rules to determine if they should be discarded.
    /// </summary>
    public class MapPinRuleEngine : IMapPinRuleEngine
    {
        private readonly IEnumerable<IMapPinRule> _rules;

        public MapPinRuleEngine(IEnumerable<IMapPinRule> rules)
        {
            _rules = rules;
        }

        /// <summary>
        /// Evaluates all rules and returns a result with discard status and reason.
        /// </summary>
        public async Task<MapPinRuleResult> ShouldDiscardAsync(MapPinRuleContext context)
        {
            foreach (var rule in _rules)
            {
                var result = await rule.ShouldDiscardAsync(context);
                if (result.ShouldDiscard)
                {
                    return result;
                }
            }
            return MapPinRuleResult.NotDiscarded();
        }
    }
}
