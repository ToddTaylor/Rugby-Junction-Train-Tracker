using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public UserRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<User> AddAsync(User user)
        {
            user.CreatedAt = _timeProvider.UtcNow;
            user.LastUpdate = user.CreatedAt;

            // Ensure UserRoles are attached
            if (user.UserRoles != null)
            {
                foreach (var userRole in user.UserRoles)
                {
                    userRole.User = user;
                }
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users
                .Include(o => o.UserRoles)
                .ThenInclude(ur => ur.Role)
                .OrderByDescending(o => o.LastUpdate)
                .ToListAsync();
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            return user;
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users
                .Include(o => o.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(o => o.ID == id);
        }

        public async Task<User> UpdateAsync(User user)
        {
            var existingUser = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.ID == user.ID);
            if (existingUser == null)
            {
                throw new KeyNotFoundException("User not found.");
            }

            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.Email = user.Email;
            existingUser.IsActive = user.IsActive;
            existingUser.LastUpdate = _timeProvider.UtcNow;

            // Prepare new role IDs
            var newRoleIds = user.UserRoles?.Select(ur => ur.RoleId).ToHashSet() ?? new HashSet<int>();

            // Remove roles that are not in the new set
            var rolesToRemove = existingUser.UserRoles
                .Where(ur => !newRoleIds.Contains(ur.RoleId))
                .ToList();
            _context.UserRoles.RemoveRange(rolesToRemove);

            // Add new roles that don't exist yet
            var existingRoleIds = existingUser.UserRoles.Select(ur => ur.RoleId).ToHashSet();
            var rolesToAdd = newRoleIds.Except(existingRoleIds);
            foreach (var roleId in rolesToAdd)
            {
                _context.UserRoles.Add(new UserRole
                {
                    UserId = existingUser.ID,
                    RoleId = roleId,
                    AssignedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // Re-query the user with roles for a fresh tracked instance
            return await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.ID == user.ID);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return false;
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
