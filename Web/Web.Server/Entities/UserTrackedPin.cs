using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class UserTrackedPin : EntityBase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int MapPinId { get; set; }

        /// <summary>
        /// The beacon ID associated with this tracked pin at the time of tracking.
        /// </summary>
        public int? BeaconID { get; set; }

        /// <summary>
        /// The beacon name associated with this tracked pin at the time of tracking.
        /// </summary>
        public string? BeaconName { get; set; }

        /// <summary>
        /// Optional user-defined symbol for this tracked pin (max 10 characters, uppercase).
        /// </summary>
        public string? Symbol { get; set; }

        /// <summary>
        /// The color assigned to this tracked pin.
        /// </summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// When this tracked pin will expire and be automatically removed.
        /// </summary>
        public DateTime ExpiresUtc { get; set; }

        // Navigation
        public User? User { get; set; }
        public MapPin? MapPin { get; set; }
    }
}
