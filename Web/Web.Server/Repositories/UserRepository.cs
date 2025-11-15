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

        public async Task<User> AddAsync(User owner)
        {
            owner.CreatedAt = _timeProvider.UtcNow;
            owner.LastUpdate = owner.CreatedAt;

            _context.Users.Add(owner);
            await _context.SaveChangesAsync();
            return owner;
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

        public async Task<User> UpdateAsync(User owner)
        {
            var existingOwner = await _context.Users.FindAsync(owner.ID);
            if (existingOwner == null)
            {
                throw new KeyNotFoundException("Owner not found.");
            }

            existingOwner.FirstName = owner.FirstName;
            existingOwner.LastName = owner.LastName;
            existingOwner.Email = owner.Email;
            existingOwner.LastUpdate = _timeProvider.UtcNow;

            await _context.SaveChangesAsync();
            return existingOwner;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var owner = await _context.Users.FindAsync(id);
            if (owner == null)
            {
                return false;
            }

            _context.Users.Remove(owner);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
