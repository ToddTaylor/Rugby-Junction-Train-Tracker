using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;

namespace Web.Server.Services
{
    public class OwnerService : IOwnerService
    {
        private readonly TelemetryDbContext _dbContext;

        public OwnerService(TelemetryDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<Owner>> GetOwnersAsync()
        {
            return await _dbContext.Owners.ToListAsync();
        }

        public async Task<Owner?> GetOwnerByIdAsync(int id)
        {
            return await _dbContext.Owners.FindAsync(id);
        }

        public async Task<Owner> CreateOwnerAsync(Owner owner)
        {
            _dbContext.Owners.Add(owner);
            await _dbContext.SaveChangesAsync();
            return owner;
        }

        public async Task<Owner> UpdateOwnerAsync(int id, Owner owner)
        {
            var existingOwner = await _dbContext.Owners.FindAsync(id);
            if (existingOwner == null)
            {
                throw new KeyNotFoundException("Owner not found.");
            }

            existingOwner.FirstName = owner.FirstName;
            existingOwner.LastName = owner.LastName;
            existingOwner.Email = owner.Email;
            existingOwner.City = owner.City;
            existingOwner.State = owner.State;

            await _dbContext.SaveChangesAsync();
            return existingOwner;
        }

        public async Task<bool> DeleteOwnerAsync(int id)
        {
            var owner = await _dbContext.Owners.FindAsync(id);
            if (owner == null)
            {
                return false;
            }

            _dbContext.Owners.Remove(owner);
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
