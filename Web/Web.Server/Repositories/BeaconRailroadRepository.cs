using Microsoft.EntityFrameworkCore;
using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public class BeaconRailroadRepository : IBeaconRailroadRepository
    {
        private readonly Data.TelemetryDbContext _context;

        public BeaconRailroadRepository(Data.TelemetryDbContext context)
        {
            _context = context;
        }

        public async Task<BeaconRailroad> AddAsync(BeaconRailroad beaconRailroad)
        {
            _context.BeaconRailroads.Add(beaconRailroad);
            await _context.SaveChangesAsync();
            return beaconRailroad;
        }

        public async Task<IEnumerable<BeaconRailroad>> GetAllAsync()
        {
            return await _context.BeaconRailroads
                .Include(br => br.Beacon)
                .Include(br => br.Railroad)
                .ToListAsync();
        }

        public async Task<BeaconRailroad?> GetByIdAsync(int beaconId, int railroadId)
        {
            return await _context.BeaconRailroads
                .Include(br => br.Beacon)
                .Include(br => br.Railroad)
                .FirstOrDefaultAsync(br => br.BeaconID == beaconId && br.RailroadID == railroadId);
        }

        public async Task<BeaconRailroad> UpdateAsync(BeaconRailroad beaconRailroad)
        {
            var existing = await _context.BeaconRailroads
                .FirstOrDefaultAsync(br => br.BeaconID == beaconRailroad.BeaconID && br.RailroadID == beaconRailroad.RailroadID);

            if (existing == null)
            {
                throw new KeyNotFoundException("BeaconRailroad not found.");
            }

            existing.Latitude = beaconRailroad.Latitude;
            existing.Longitude = beaconRailroad.Longitude;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int beaconId, int railroadId)
        {
            var beaconRailroad = await _context.BeaconRailroads
                .FirstOrDefaultAsync(br => br.BeaconID == beaconId && br.RailroadID == railroadId);

            if (beaconRailroad == null)
            {
                return false;
            }

            _context.BeaconRailroads.Remove(beaconRailroad);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
