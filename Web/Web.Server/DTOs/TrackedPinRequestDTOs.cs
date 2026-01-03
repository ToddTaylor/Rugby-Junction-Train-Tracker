namespace Web.Server.DTOs
{
    public class AddTrackedPinRequestDTO
    {
        public int MapPinId { get; set; }
        public int? BeaconID { get; set; }
        public string? BeaconName { get; set; }
        public string? Symbol { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    public class UpdateTrackedPinSymbolRequestDTO
    {
        public string? Symbol { get; set; }
    }

    public class UpdateTrackedPinLocationRequestDTO
    {
        public int? BeaconID { get; set; }
        public string? BeaconName { get; set; }
    }
}
