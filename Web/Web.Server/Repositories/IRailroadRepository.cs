using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IRailroadRepository
    {
        Task<Railroad> AddAsync(Railroad railroad);
        Task<IEnumerable<Railroad>> GetAllAsync();
        Task<Railroad?> GetByIdAsync(int id);
        Task<Railroad> UpdateAsync(Railroad railroad);
        Task<bool> DeleteAsync(int id);
    }
}
