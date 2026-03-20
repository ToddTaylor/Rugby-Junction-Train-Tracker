using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IMapPinHistoryRepository
    {
        Task<IEnumerable<MapPinHistory>> GetByBeaconIdAsync(int beaconId, int? subdivisionId = null, int? limit = null);
        Task<MapPinHistory?> GetByOriginalMapPinIdAsync(int mapPinId);
        Task<IEnumerable<MapPinHistory>> GetLatestPerBeaconAsync();
        Task<MapPinHistory> AddAsync(MapPinHistory mapPinHistory);
        Task<MapPinHistory> UpdateAsync(MapPinHistory mapPinHistory);
            Task DeleteByOriginalMapPinIdAsync(int mapPinId);
    }
}
