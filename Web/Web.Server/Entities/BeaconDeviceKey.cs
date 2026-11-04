using System.ComponentModel.DataAnnotations;

namespace Web.Server.Entities
{
    /// <summary>
    /// The secret a beacon PC sends in the X-Beacon-Key header. Only its SHA-256 hash is stored. Once a beacon has a
    /// key, health checks without it are refused, and remote commands are only handed to a PC that presents it.
    /// </summary>
    public class BeaconDeviceKey : EntityBase
    {
        [Key]
        public int BeaconID { get; set; }

        public Beacon? Beacon { get; set; }

        /// <summary>
        /// Lowercase hex SHA-256 of the key.
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string KeyHash { get; set; } = string.Empty;

        [MaxLength(200)]
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Last time a request for this beacon was refused because the key was missing or wrong.
        /// </summary>
        public DateTime? LastRejectedAt { get; set; }
    }
}
