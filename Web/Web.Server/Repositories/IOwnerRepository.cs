using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IOwnerRepository
    {
        Task<Owner> AddAsync(Owner owner);
        Task<IEnumerable<Owner>> GetAllAsync();
        Task<Owner?> GetByIdAsync(int id);
        Task<Owner> UpdateAsync(Owner owner);
        Task<bool> DeleteAsync(int id);
    }
}
