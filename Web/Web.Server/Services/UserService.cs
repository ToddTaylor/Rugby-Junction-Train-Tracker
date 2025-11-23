using Web.Server.Entities;
using Web.Server.Repositories;
using Web.Server.DTOs;
using Microsoft.EntityFrameworkCore;
using Web.Server.Data;

namespace Web.Server.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly TelemetryDbContext _context;

        public UserService(IUserRepository userRepository, TelemetryDbContext context)
        {
            _userRepository = userRepository;
            _context = context;
        }

        public async Task<IEnumerable<User>> GetUsersAsync()
        {
            return await _userRepository.GetAllAsync();
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _userRepository.GetByEmailAsync(email);
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _userRepository.GetByIdAsync(id);
        }

        public async Task<User> CreateUserAsync(User owner)
        {
            // Roles should be assigned before saving
            if (owner.UserRoles != null && owner.UserRoles.Count > 0)
            {
                foreach (var userRole in owner.UserRoles)
                {
                    var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == userRole.Role.RoleName);
                    if (role != null)
                    {
                        userRole.RoleId = role.RoleId;
                        userRole.Role = role;
                    }
                }
            }
            return await _userRepository.AddAsync(owner);
        }

        public async Task<User> UpdateUserAsync(int id, User owner)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                throw new KeyNotFoundException();

            // Update basic properties
            user.FirstName = owner.FirstName;
            user.LastName = owner.LastName;
            user.Email = owner.Email;
            user.IsActive = owner.IsActive;

            // Assign new roles (only key properties)
            user.UserRoles = owner.UserRoles ?? new List<UserRole>();

            return await _userRepository.UpdateAsync(user);
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            return await _userRepository.DeleteAsync(id);
        }

        public async Task<Role?> GetRoleByNameAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return null;

            return await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleName.ToLower() == roleName.ToLower());
        }
    }
}

