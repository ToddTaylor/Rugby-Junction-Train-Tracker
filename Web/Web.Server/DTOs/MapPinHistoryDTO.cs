using System.Text.Json;

namespace Web.Server.DTOs
{
    public class MapPinHistoryDTO
    {
        public int ID { get; set; }
        public int? OriginalMapPinID { get; set; }
        public string? ShareCode { get; set; }
        public int BeaconID { get; set; }
        public string? BeaconName { get; set; }
        public int SubdivisionID { get; set; }
        public string? Subdivision { get; set; }
        public string? Railroad { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Milepost { get; set; }
        public string? Direction { get; set; }
        public bool? Moving { get; set; }
        public bool IsLocal { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string LastUpdate { get; set; } = string.Empty;
        
        // Deserialized addresses from JSON
        public List<AddressSnapshotDTO>? Addresses { get; set; }

            /// <summary>
            /// Indicates whether this train has at least one DPU address.
            /// Visible to all users, even when address details are hidden from Viewers.
            /// </summary>
            public bool HasDpu { get; set; }

            /// <summary>
            /// Distinct address source types (for example HOT/EOT/DPU), visible to all users.
            /// </summary>
            public List<string> AddressSourceTypes { get; set; } = [];
    }

    public class AddressSnapshotDTO
    {
        public int AddressID { get; set; }
        public string Source { get; set; } = string.Empty;
        public int? DpuTrainID { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string LastUpdate { get; set; } = string.Empty;
    }
}
