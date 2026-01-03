namespace Web.Server.DTOs
{
    public class UserTrackedPinDTO
    {
        public int ID { get; set; }
        public int UserId { get; set; }
        public int MapPinId { get; set; }
        public int? BeaconID { get; set; }
        public int? SubdivisionID { get; set; }
        public string? BeaconName { get; set; }
        public string? Symbol { get; set; }
        public string Color { get; set; } = string.Empty;
        public DateTime ExpiresUtc { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}
