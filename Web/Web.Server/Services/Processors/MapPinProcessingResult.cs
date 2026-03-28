using Web.Server.Entities;

namespace Web.Server.Services.Processors
{
    /// <summary>
    /// Result of processing telemetry through a map pin processor.
    /// If DiscardReason is null, the map pin should be upserted.
    /// If DiscardReason is non-null, the telemetry should be marked discarded.
    /// </summary>
    public class MapPinProcessingResult
    {
        /// <summary>
        /// The map pin candidate to upsert, or null if creation/update failed.
        /// </summary>
        public MapPin? MapPin { get; init; }

        /// <summary>
        /// Whether this is a newly created map pin (true) or an existing one (false).
        /// </summary>
        public bool IsNewMapPin { get; init; }

        /// <summary>
        /// Reason to discard telemetry, or null if telemetry should be kept and processed.
        /// </summary>
        public string? DiscardReason { get; init; }
    }
}
