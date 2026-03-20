using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IMapPinHistoryService
    {
        Task<MapPinHistory?> GetHistoryByOriginalMapPinIdAsync(int mapPinId);
        Task<IEnumerable<MapPinHistory>> GetHistoryByBeaconIdAsync(int beaconId, int? subdivisionId = null, int? limit = null);
        Task<IEnumerable<MapPinHistory>> GetLatestPerBeaconAsync();
        Task CreateOrUpdateHistoryFromMapPin(MapPin mapPin, bool isNewMapPin);
            Task DeleteHistoryByOriginalMapPinIdAsync(int mapPinId);
    }
}
