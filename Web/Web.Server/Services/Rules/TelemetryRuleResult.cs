public class TelemetryRuleResult
{
    public bool ShouldDiscard { get; set; }
    public string? Reason { get; set; }

    public static TelemetryRuleResult NotDiscarded() => new() { ShouldDiscard = false };
    public static TelemetryRuleResult Discarded(string reason) => new() { ShouldDiscard = true, Reason = reason };
}