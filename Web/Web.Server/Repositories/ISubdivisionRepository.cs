using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface ISubdivisionRepository
    {
        Task<Subdivision> AddAsync(Subdivision subdivision);
        Task<IEnumerable<Subdivision>> GetAllAsync();
        Task<Subdivision?> GetByIdAsync(int id);
        Task<Subdivision> UpdateAsync(Subdivision subdivision);
        Task<bool> DeleteAsync(int id);
    }
}
