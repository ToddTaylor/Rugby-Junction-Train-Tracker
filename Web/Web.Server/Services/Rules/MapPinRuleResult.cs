namespace Web.Server.Services.Rules
{
    /// <summary>
    /// Represents the result of a map pin rule evaluation.
    /// </summary>
    public class MapPinRuleResult
    {
        public bool ShouldDiscard { get; set; }
        public string? Reason { get; set; }

        public static MapPinRuleResult NotDiscarded() => new() { ShouldDiscard = false };
        public static MapPinRuleResult Discarded(string reason) => new() { ShouldDiscard = true, Reason = reason };
    }
}
