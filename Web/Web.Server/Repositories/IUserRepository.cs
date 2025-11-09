using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IUserRepository
    {
        Task<User> AddAsync(User owner);
        Task<IEnumerable<User>> GetAllAsync();
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<User> UpdateAsync(User owner);
        Task<bool> DeleteAsync(int id);
    }
}
