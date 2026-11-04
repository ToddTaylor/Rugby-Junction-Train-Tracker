using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Web.Server.Enums;

namespace Web.Server.Entities
{
    /// <summary>
    /// A reboot or restart-apps request for a beacon PC, and the audit record of who asked for it and what happened.
    /// All times are UTC.
    /// </summary>
    public class BeaconCommand : EntityBase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        public int BeaconID { get; set; }

        public Beacon? Beacon { get; set; }

        public BeaconCommandType Type { get; set; }

        public int? RequestedByUserId { get; set; }

        [MaxLength(200)]
        public string RequestedBy { get; set; } = string.Empty;

        public DateTime RequestedAt { get; set; }

        /// <summary>
        /// The PC must start the command before this time, or it is not carried out.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// First time the command was handed to the PC with a health check response.
        /// </summary>
        public DateTime? DeliveredAt { get; set; }

        /// <summary>
        /// When the PC reported that it accepted the command and started it.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the command ended: succeeded, failed or rejected by the PC.
        /// </summary>
        public DateTime? FinishedAt { get; set; }

        /// <summary>
        /// Succeeded, Failed or Rejected once FinishedAt is set.
        /// </summary>
        public BeaconCommandStatus? Outcome { get; set; }

        [MaxLength(500)]
        public string? ResultMessage { get; set; }

        public DateTime? CancelledAt { get; set; }

        [MaxLength(200)]
        public string? CancelledBy { get; set; }

        public BeaconCommandStatus GetStatus(DateTime utcNow)
        {
            if (Outcome.HasValue)
            {
                return Outcome.Value;
            }

            if (CancelledAt.HasValue)
            {
                return BeaconCommandStatus.Cancelled;
            }

            if (StartedAt.HasValue)
            {
                return BeaconCommandStatus.Running;
            }

            return ExpiresAt > utcNow ? BeaconCommandStatus.Pending : BeaconCommandStatus.Expired;
        }
    }
}
