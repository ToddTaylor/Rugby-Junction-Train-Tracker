using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;

namespace Web.Server.Services
{
    public class BeaconService : IBeaconService
    {
        private readonly TelemetryDbContext _dbContext;

        public BeaconService(TelemetryDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<Beacon>> GetBeaconsAsync()
        {
            return await _dbContext.Beacons.Include(b => b.Owner).ToListAsync();
        }

        public async Task<Beacon?> GetBeaconByIdAsync(int id)
        {
            return await _dbContext.Beacons.Include(b => b.Owner).FirstOrDefaultAsync(b => b.ID == id);
        }

        public async Task<Beacon> CreateBeaconAsync(Beacon beacon)
        {
            _dbContext.Beacons.Add(beacon);
            await _dbContext.SaveChangesAsync();
            return beacon;
        }

        public async Task<Beacon> UpdateBeaconAsync(int id, Beacon beacon)
        {
            var existingBeacon = await _dbContext.Beacons.FindAsync(id);
            if (existingBeacon == null)
            {
                throw new KeyNotFoundException("Beacon not found.");
            }

            existingBeacon.Latitude = beacon.Latitude;
            existingBeacon.Longitude = beacon.Longitude;
            existingBeacon.Timestamp = beacon.Timestamp;
            existingBeacon.Owner = beacon.Owner;

            await _dbContext.SaveChangesAsync();
            return existingBeacon;
        }

        public async Task<bool> DeleteBeaconAsync(int id)
        {
            var beacon = await _dbContext.Beacons.FindAsync(id);
            if (beacon == null)
            {
                return false;
            }

            _dbContext.Beacons.Remove(beacon);
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
