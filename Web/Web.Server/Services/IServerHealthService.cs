using Web.Server.DTOs;
using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IServerHealthService
    {
        /// <summary>
        /// Stores the diagnostics report a beacon PC sent with its health check.
        /// </summary>
        Task RecordDiagnosticsAsync(int beaconId, BeaconDiagnostics diagnostics);

        /// <summary>
        /// Server Health rows the user may see: every beacon for Admins, and beacons on their own
        /// subdivisions for Custodians. Other users get an empty list.
        /// </summary>
        Task<IReadOnlyList<ServerHealthDTO>> GetServerHealthAsync(User user);

        /// <summary>
        /// True for Admins, and for Custodians of a subdivision the beacon serves.
        /// </summary>
        Task<bool> CanManageBeaconAsync(User user, int beaconId);

        /// <summary>
        /// Returns false when the beacon does not exist.
        /// </summary>
        Task<bool> UpdateExpectedProgramsAsync(int beaconId, UpdateServerExpectationsDTO expectations);

        /// <summary>
        /// Stops status-change pop-ups for the beacon until the mute ends. Returns false when the beacon does not exist.
        /// </summary>
        Task<bool> MuteAlertsAsync(int beaconId, MuteServerAlertsDTO mute, User mutedBy);

        /// <summary>
        /// Returns false when the beacon does not exist.
        /// </summary>
        Task<bool> UnmuteAlertsAsync(int beaconId);

        /// <summary>
        /// Evaluates every beacon, saves its alert state, and returns the status changes support users should be told
        /// about, each with the users who may see that beacon. Called once a minute by ServerHealthMonitorService.
        /// </summary>
        Task<IReadOnlyList<ServerStatusChange>> DetectStatusChangesAsync();
    }

    /// <summary>
    /// A status change to announce, and the IDs of the Admins and Custodians who should receive it.
    /// </summary>
    public record ServerStatusChange(ServerStatusChangedDTO Notification, IReadOnlyList<int> RecipientUserIds);
}
