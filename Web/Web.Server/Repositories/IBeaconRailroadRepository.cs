using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IBeaconRailroadRepository
    {
        Task<BeaconRailroad> AddAsync(BeaconRailroad beaconRailroad);
        Task<IEnumerable<BeaconRailroad>> GetAllAsync();
        Task<BeaconRailroad?> GetByIdAsync(int beaconId, int subdivisionId);
        Task<BeaconRailroad> UpdateAsync(BeaconRailroad beaconRailroad);
        Task<bool> DeleteAsync(int beaconId, int railroadId);
    }
}
