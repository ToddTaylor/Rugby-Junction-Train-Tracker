using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IMapPinRepository
    {
        Task<MapPin?> GetByAddressIdAsync(int addressID);

        Task<MapPin?> GetByTimeThreshold(int beaconID, int railroadID, int minutesThreshold);

        Task<MapPin?> GetByTrainIdAsync(int dpuTrainID, int minutesThreshold);

        Task<MapPin?> GetByIdAsync(int id);

        Task<IEnumerable<MapPin>> GetAllAsync(int? minutes);

        Task<IEnumerable<MapPin>> GetLatestAsync();

        Task<IEnumerable<MapPin>> GetAllByBeaconAsync(int beaconID, int subdivisionID, int? minutesThreshold = null);

        Task<MapPin> UpsertAsync(MapPin mapPin, DateTime telemetryTimestamp);

        Task<bool> DeleteAsync(int id);
    }
}
