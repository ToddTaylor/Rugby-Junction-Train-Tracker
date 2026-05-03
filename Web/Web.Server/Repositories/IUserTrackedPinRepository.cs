using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IUserTrackedPinRepository
    {
        Task<UserTrackedPin?> GetByIdAsync(int id);
        Task<IEnumerable<UserTrackedPin>> GetByUserIdAsync(int userId);
        Task<UserTrackedPin?> GetByUserAndMapPinAsync(int userId, int mapPinId);
        Task<IEnumerable<UserTrackedPin>> GetByMapPinIdAsync(int mapPinId);
        Task<IEnumerable<UserTrackedPin>> GetByShareCodeAsync(string shareCode);
        Task<UserTrackedPin> AddAsync(UserTrackedPin trackedPin);
        Task UpdateAsync(UserTrackedPin trackedPin);
        Task DeleteAsync(int id);
        Task DeleteByUserAndMapPinAsync(int userId, int mapPinId);
        Task<IEnumerable<UserTrackedPin>> GetExpiredAsync(DateTime nowUtc);
        Task DeleteExpiredAsync();
        Task UpdateMapPinIdAsync(int oldMapPinId, int newMapPinId);
    }
}
