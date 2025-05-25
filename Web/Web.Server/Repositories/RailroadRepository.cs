using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class RailroadRepository : IRailroadRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public RailroadRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<Railroad> AddAsync(Railroad railroad)
        {
            railroad.CreatedAt = _timeProvider.UtcNow;
            railroad.LastUpdate = railroad.CreatedAt;

            _context.Railroads.Add(railroad);
            await _context.SaveChangesAsync();
            return railroad;
        }

        public async Task<IEnumerable<Railroad>> GetAllAsync()
        {
            return await _context.Railroads
                .OrderByDescending(r => r.LastUpdate)
                .ToListAsync();
        }

        public async Task<Railroad?> GetByIdAsync(int id)
        {
            return await _context.Railroads.FirstOrDefaultAsync(r => r.ID == id);
        }

        public async Task<Railroad> UpdateAsync(Railroad railroad)
        {
            var existingRailroad = await _context.Railroads.FindAsync(railroad.ID);
            if (existingRailroad == null)
            {
                throw new KeyNotFoundException("Railroad not found.");
            }

            existingRailroad.Name = railroad.Name;
            existingRailroad.Subdivision = railroad.Subdivision;
            existingRailroad.LastUpdate = _timeProvider.UtcNow;

            await _context.SaveChangesAsync();
            return existingRailroad;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var railroad = await _context.Railroads.FindAsync(id);
            if (railroad == null)
            {
                return false;
            }

            _context.Railroads.Remove(railroad);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
