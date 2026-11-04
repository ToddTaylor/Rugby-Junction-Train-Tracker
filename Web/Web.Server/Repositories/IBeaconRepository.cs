using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IBeaconRepository
    {
        Task<Beacon> AddAsync(Beacon beacon);
        Task<IEnumerable<Beacon>> GetAllAsync();
        Task<Beacon?> GetByIdAsync(int id);
        Task<Beacon> UpdateAsync(Beacon beacon);

        /// <summary>
        /// Sets how many SDR#, SoftEOT and SoftDPU instances the beacon PC should run. Returns null when the beacon does not exist.
        /// </summary>
        Task<Beacon?> UpdateExpectedProgramCountsAsync(int id, int? sdrSharpCount, int? softEotCount, int? softDpuCount);
        Task<bool> DeleteAsync(int id);
    }
}
