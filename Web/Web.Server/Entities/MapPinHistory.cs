using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    /// <summary>
    /// Historical record of map pins at beacons for tracking train passage history.
    /// Each record represents a train's passage through a beacon at a specific time.
    /// </summary>
    public class MapPinHistory : EntityBase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        public int BeaconID { get; set; }

        [Required]
        public int SubdivisionId { get; set; }

        /// <summary>
        /// The RailroadID of the subdivision where this map pin was originally created.
        /// This is set once at creation and never updated.
        /// </summary>
        public int? CreatedRailroadID { get; set; }

        [NotMapped]
        public BeaconRailroad? BeaconRailroad { get; set; }

        public string? Direction { get; set; }

        public bool? Moving { get; set; }

        /// <summary>
        /// Indicates if this map pin represents a local train.
        /// </summary>
        public bool IsLocal { get; set; } = false;

        /// <summary>
        /// Snapshot of addresses at the time this history record was created.
        /// Stored as JSON string.
        /// </summary>
        public string AddressesJson { get; set; } = string.Empty;

        /// <summary>
        /// The original MapPin ID that this history record was created from.
        /// </summary>
        public int? OriginalMapPinID { get; set; }
    }
}
