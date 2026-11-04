using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IBeaconServerStateRepository
    {
        Task<IEnumerable<BeaconServerState>> GetAllAsync();

        /// <summary>
        /// Saves the announced and pending status, adding a row if the beacon has none. Mute fields are left as they are.
        /// </summary>
        Task UpdateStatusAsync(BeaconServerState state);

        /// <summary>
        /// Mutes alerts for the beacon. A null mutedUntil mutes them until they are turned back on.
        /// </summary>
        Task SetMuteAsync(int beaconId, DateTime? mutedUntil, string? note, string mutedBy);

        Task ClearMuteAsync(int beaconId);
    }
}
