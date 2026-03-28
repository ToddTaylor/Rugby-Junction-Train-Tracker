using Web.Server.Entities;

namespace Web.Server.Services.Processors
{
    /// <summary>
    /// Processes telemetry for a specific source type and returns a map pin candidate with status.
    /// </summary>
    public interface IMapPinProcessor
    {
        /// <summary>
        /// Sources handled by this processor (string constants from SourceEnum).
        /// </summary>
        string[] SupportedSources { get; }

        /// <summary>
        /// Process telemetry and return a map pin result.
        /// </summary>
        Task<MapPinProcessingResult> ProcessAsync(Telemetry telemetry);
    }
}
