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
    }
}
