using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IMapPinRepository
    {
        Task<MapPin?> GetByAddressIdAsync(int addressID, int? trainID);

        Task<MapPin?> GetByTimeThreshold(int beaconID, int railroadID, int minutesThreshold);

        Task<MapPin?> GetByTrainIdAsync(int dpuTrainID);

        Task<MapPin?> GetByIdAsync(int id);

        Task<IEnumerable<MapPin>> GetAllAsync(int? minutes);

        Task<IEnumerable<MapPin>> GetLatestAsync();

        Task<MapPin> UpsertAsync(MapPin mapPin);

        Task<bool> DeleteAsync(int id);
    }
}
