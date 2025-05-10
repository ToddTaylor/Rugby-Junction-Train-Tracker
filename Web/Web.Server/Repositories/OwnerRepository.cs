using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public class OwnerRepository : IOwnerRepository
    {
        private readonly TelemetryDbContext _context;

        public OwnerRepository(TelemetryDbContext context)
        {
            _context = context;
        }

        public async Task<Owner> AddAsync(Owner owner)
        {
            _context.Owners.Add(owner);
            await _context.SaveChangesAsync();
            return owner;
        }

        public async Task<IEnumerable<Owner>> GetAllAsync()
        {
            return await _context.Owners
                .Include(o => o.Beacons)
                .ThenInclude(o => o.BeaconRailroads)
                .ThenInclude(o => o.Railroad)
                .ToListAsync();
        }

        public async Task<Owner?> GetByIdAsync(int id)
        {
            return await _context.Owners
                .Include(o => o.Beacons)
                .ThenInclude(o => o.BeaconRailroads)
                .ThenInclude(o => o.Railroad)
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
