
namespace Web.Server.Services.Rules
{
    public interface ITelemetryRuleEngine
    {
        Task<TelemetryRuleResult> ShouldDiscardAsync(TelemetryRuleContext context);
    }
}