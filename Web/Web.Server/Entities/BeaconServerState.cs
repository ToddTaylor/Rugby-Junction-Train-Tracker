using System.ComponentModel.DataAnnotations;
using Web.Server.Enums;

namespace Web.Server.Entities
{
    /// <summary>
    /// Alert state for one beacon PC: the last status support users were told about, a status
    /// change still waiting to be confirmed, and whether alerts are muted. All times are UTC.
    /// </summary>
    public class BeaconServerState : EntityBase
    {
        [Key]
        public int BeaconID { get; set; }

        public Beacon? Beacon { get; set; }

        /// <summary>
        /// The status last announced to support users.
        /// </summary>
        public ServerStatus Status { get; set; }

        public DateTime StatusSince { get; set; }

        /// <summary>
        /// A different status seen since PendingSince that has not lasted long enough to announce.
        /// </summary>
        public ServerStatus? PendingStatus { get; set; }

        public DateTime? PendingSince { get; set; }

        /// <summary>
        /// Set while alerts are muted. Null means alerts are on.
        /// </summary>
        public DateTime? AlertsMutedAt { get; set; }

        /// <summary>
        /// When the mute ends. Null while muted means muted until someone turns alerts back on.
        /// </summary>
        public DateTime? AlertsMutedUntil { get; set; }

        [MaxLength(500)]
        public string? AlertsMuteNote { get; set; }

        /// <summary>
        /// Name of the user who muted the alerts.
        /// </summary>
        public string? AlertsMutedBy { get; set; }

        public bool IsMuted(DateTime utcNow) =>
            AlertsMutedAt.HasValue && (!AlertsMutedUntil.HasValue || AlertsMutedUntil.Value > utcNow);
    }
}
