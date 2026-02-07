namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Defines contract for evaluating map pins against a collection of rules.
    /// </summary>
    public interface IMapPinRuleEngine
    {
        /// <summary>
        /// Evaluates all rules and returns a result with discard status and reason.
        /// </summary>
        Task<MapPinRuleResult> ShouldDiscardAsync(MapPinRuleContext context);
    }
}
