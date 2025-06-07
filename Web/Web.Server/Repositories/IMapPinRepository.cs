using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IMapPinRepository
    {
        Task<MapPin?> GetByAddressIdAsync(int addressID);

        Task<MapPin?> GetByTimeThreshold(int beaconID, int railroadID, int minutesThreshold);

        Task<IEnumerable<MapPin>> GetAllAsync(int? minutes);

        Task<MapPin> UpsertAsync(MapPin mapPin);
    }
}
