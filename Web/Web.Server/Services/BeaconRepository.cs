using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;

namespace Web.Server.Services
{
    public class BeaconRepository : IBeaconRepository
    {
        private readonly TelemetryDbContext _context;

        public BeaconRepository(TelemetryDbContext context)
        {
            _context = context;
        }

        public async Task<Beacon> AddAsync(Beacon beacon)
        {
            _context.Beacons.Add(beacon);
            await _context.SaveChangesAsync();
            return beacon;
        }

        public async Task<IEnumerable<Beacon>> GetAllAsync()
        {
            return await _context.Beacons.Include(b => b.Owner).Include(b => b.Railroads).ToListAsync();
        }

        public async Task<Beacon?> GetByIdAsync(int id)
        {
            return await _context.Beacons.Include(b => b.Owner).Include(b => b.Railroads).FirstOrDefaultAsync(b => b.ID == id);
        }

        public async Task<Beacon> UpdateAsync(Beacon beacon)
        {
            var existingBeacon = await _context.Beacons.FindAsync(beacon.ID);
            if (existingBeacon == null)
            {
                throw new KeyNotFoundException("Beacon not found.");
            }

            existingBeacon.Latitude = beacon.Latitude;
            existingBeacon.Longitude = beacon.Longitude;
            existingBeacon.Timestamp = beacon.Timestamp;
            existingBeacon.Owner = beacon.Owner;
            existingBeacon.Railroads = beacon.Railroads;

            await _context.SaveChangesAsync();
            return existingBeacon;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var beacon = await _context.Beacons.FindAsync(id);
            if (beacon == null)
            {
                return false;
            }

            _context.Beacons.Remove(beacon);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
