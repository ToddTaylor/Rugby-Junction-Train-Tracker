namespace Web.Server.DTOs
{
    public class SubdivisionDTO
    {
        public int ID { get; set; }

        public bool DpuCapable { get; set; }

        public required string Name { get; set; }

        public string? LocalTrainAddressIDs { get; set; }

        public int RailroadID { get; set; }

        public required string Railroad { get; set; }

        public required DateTime CreatedAt { get; set; }

        public required DateTime LastUpdate { get; set; }

    }
}
