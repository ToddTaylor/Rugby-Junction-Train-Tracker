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

        /// <summary>
        /// Non-discarded telemetry applied to one map pin since the given time, newest first.
        /// </summary>
        Task<List<Telemetry>> GetRecentsForMapPinAsync(int mapPinId, DateTime sinceUtc);

        /// <summary>
        /// Telemetry for an address discarded with exactly the given reason since the given time, newest first.
        /// </summary>
        Task<List<Telemetry>> GetRecentsDiscardedForAddressAsync(int addressId, string discardReason, DateTime sinceUtc);

        /// <summary>
        /// Records the map pin a telemetry row was applied to.
        /// </summary>
        Task SetMapPinAsync(int telemetryId, int mapPinId);

        /// <summary>
        /// Moves every telemetry row from one map pin to another, used when map pins are merged.
        /// </summary>
        Task ReassignMapPinAsync(int fromMapPinId, int toMapPinId);
    }
}
