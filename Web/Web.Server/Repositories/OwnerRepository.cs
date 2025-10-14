using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class OwnerRepository : IOwnerRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public OwnerRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<Owner> AddAsync(Owner owner)
        {
            owner.CreatedAt = _timeProvider.UtcNow;
            owner.LastUpdate = owner.CreatedAt;

            _context.Owners.Add(owner);
            await _context.SaveChangesAsync();
            return owner;
        }

        public async Task<IEnumerable<Owner>> GetAllAsync()
        {
            return await _context.Owners
                .Include(o => o.Beacons)
                .ThenInclude(o => o.BeaconRailroads)
                .ThenInclude(o => o.Subdivision)
                .OrderByDescending(o => o.LastUpdate)
                .ToListAsync();
        }

        public async Task<Owner?> GetByIdAsync(int id)
        {
            return await _context.Owners
                .Include(o => o.Beacons)
                .ThenInclude(o => o.BeaconRailroads)
                .ThenInclude(o => o.Subdivision)
                .FirstOrDefaultAsync(o => o.ID == id);
        }

        public async Task<Owner> UpdateAsync(Owner owner)
        {
            var existingOwner = await _context.Owners.FindAsync(owner.ID);
            if (existingOwner == null)
            {
                throw new KeyNotFoundException("Owner not found.");
            }

            existingOwner.FirstName = owner.FirstName;
            existingOwner.LastName = owner.LastName;
            existingOwner.Email = owner.Email;
            existingOwner.City = owner.City;
            existingOwner.State = owner.State;
            existingOwner.LastUpdate = _timeProvider.UtcNow;

            await _context.SaveChangesAsync();
            return existingOwner;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var owner = await _context.Owners.FindAsync(id);
            if (owner == null)
            {
                return false;
            }

            _context.Owners.Remove(owner);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
