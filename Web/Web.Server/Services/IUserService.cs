using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetUsersAsync();
        Task<User?> GetUserByIdAsync(int id);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User> CreateUserAsync(User owner);
        Task<User> UpdateUserAsync(int id, User owner);
        Task<bool> DeleteUserAsync(int id);
    }
}
