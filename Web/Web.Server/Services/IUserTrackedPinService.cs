using Web.Server.DTOs;

namespace Web.Server.Services
{
    public interface IUserTrackedPinService
    {
        Task<IEnumerable<UserTrackedPinDTO>> GetByUserIdAsync(int userId);
        Task<UserTrackedPinDTO> AddAsync(int userId, int mapPinId, int? beaconId, int? subdivisionId, string? beaconName, string? symbol, string color);
        Task UpdateSymbolAsync(int userId, int mapPinId, string? symbol);
        Task UpdateLocationAsync(int userId, int mapPinId, int? beaconId, int? subdivisionId, string? beaconName);
        Task DeleteAsync(int userId, int mapPinId);
    }
}
