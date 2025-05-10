using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IBeaconRepository
    {
        Task<Beacon> AddAsync(Beacon beacon);
        Task<IEnumerable<Beacon>> GetAllAsync();
        Task<Beacon?> GetByIdAsync(int id);
        Task<Beacon> UpdateAsync(Beacon beacon);
        Task<bool> DeleteAsync(int id);
    }
}
