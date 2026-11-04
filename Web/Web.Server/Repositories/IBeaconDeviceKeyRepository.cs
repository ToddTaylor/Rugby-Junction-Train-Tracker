using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IBeaconDeviceKeyRepository
    {
        Task<BeaconDeviceKey?> GetAsync(int beaconId);

        Task<IEnumerable<BeaconDeviceKey>> GetAllAsync();

        /// <summary>
        /// Adds or replaces the beacon's key and clears its last-rejected time.
        /// </summary>
        Task SetAsync(int beaconId, string keyHash, string createdBy);

        /// <summary>
        /// Returns false when the beacon has no key.
        /// </summary>
        Task<bool> DeleteAsync(int beaconId);

        Task RecordRejectedAsync(int beaconId);
    }
}
