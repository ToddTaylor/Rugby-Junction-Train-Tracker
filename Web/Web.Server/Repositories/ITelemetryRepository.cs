using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface ITelemetryRepository
    {
        Task<Telemetry> AddAsync(Telemetry telemetry);
        Task<IEnumerable<Telemetry>> GetAllAsync();
        Task<Telemetry?> GetByIdAsync(int id);
        Task<Telemetry> UpdateAsync(Telemetry telemetry);
        Task<bool> DeleteAsync(int id);
        Task<Telemetry?> GetRecentWithinTimeOffsetAsync(int trainId, int beaconId, int railroadId, DateTime sinceUtc);
        Task<List<Telemetry>> GetRecentsWithinTimeOffsetAsync(int addressId, int railroadId, DateTime sinceUtc);
        Task<List<Telemetry>> GetRecentsForTrainWithinTimeOffsetAsync(int trainId, int railroadId, DateTime sinceUtc);
        Task<Telemetry?> GetMostRecentByAddressAsync(int addressId);
    }
}
