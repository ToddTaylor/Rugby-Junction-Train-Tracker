using Web.Server.DTOs;

namespace Web.Server.Services
{
    public interface IUserTrackedPinService
    {
        Task<UserTrackedPinDTO?> GetByIdAsync(int id);
        Task<IEnumerable<UserTrackedPinDTO>> GetByUserIdAsync(int userId);
        Task<UserTrackedPinDTO?> GetByUserAndMapPinAsync(int userId, int mapPinId);
        Task<UserTrackedPinDTO> AddAsync(int userId, int mapPinId, int? beaconId, string? beaconName, string? symbol, string color);
        Task UpdateSymbolAsync(int userId, int mapPinId, string? symbol);
        Task UpdateLocationAsync(int userId, int mapPinId, int? beaconId, string? beaconName);
        Task DeleteAsync(int userId, int mapPinId);
        Task CleanupExpiredAsync();
    }
}
