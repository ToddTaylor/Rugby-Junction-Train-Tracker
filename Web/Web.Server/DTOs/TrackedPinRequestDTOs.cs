using System.ComponentModel.DataAnnotations;

namespace Web.Server.DTOs
{
    public static class TrackedPinLimits
    {
        /// <summary>
        /// Longest train symbol a user may give a tracked pin; the web client enforces the same limit.
        /// </summary>
        public const int MaxSymbolLength = 10;
    }

    public class AddTrackedPinRequestDTO
    {
        public int MapPinId { get; set; }
        public int? BeaconID { get; set; }
        public int? SubdivisionID { get; set; }
        public string? BeaconName { get; set; }
        [MaxLength(TrackedPinLimits.MaxSymbolLength)]
        public string? Symbol { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    public class UpdateTrackedPinSymbolRequestDTO
    {
        [MaxLength(TrackedPinLimits.MaxSymbolLength)]
        public string? Symbol { get; set; }
    }

    public class UpdateTrackedPinLocationRequestDTO
    {
        public int? BeaconID { get; set; }
        public int? SubdivisionID { get; set; }
        public string? BeaconName { get; set; }
    }

    public class AddTrackedPinByShareCodeRequestDTO
    {
        public string ShareCode { get; set; } = string.Empty;
    }
}
