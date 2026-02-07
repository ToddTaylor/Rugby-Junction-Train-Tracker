namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Represents a rule that determines whether a map pin should be discarded based on beacon railroad pairs.
    /// </summary>
    public interface IMapPinRule
    {
        /// <summary>
        /// Evaluates whether the map pin should be discarded based on the current and previous beacon railroads.
        /// </summary>
        /// <param name="context">The context containing the to and from beacon railroads and related data.</param>
        /// <returns>A MapPinRuleResult indicating whether the map pin should be discarded.</returns>
        Task<MapPinRuleResult> ShouldDiscardAsync(MapPinRuleContext context);
    }
}
