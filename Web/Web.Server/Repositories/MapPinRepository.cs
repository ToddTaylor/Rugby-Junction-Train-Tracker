using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class MapPinRepository : IMapPinRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public MapPinRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<MapPin?> GetByTimeThreshold(int beaconID, int railroadID, int minutesThreshold)
        {
            return await _context.MapPins
                .Where(mp => mp.BeaconID == beaconID &&
                             mp.SubdivisionId == railroadID &&
                             mp.LastUpdate >= _timeProvider.UtcNow.AddMinutes(-minutesThreshold))
                .OrderByDescending(mp => mp.LastUpdate)
                .Include(mp => mp.Addresses)
                .Include(mp => mp.BeaconRailroad)
                .Include(mp => mp.BeaconRailroad.Subdivision)
                .Include(mp => mp.BeaconRailroad.Subdivision.Railroad)
                .FirstOrDefaultAsync();
        }

        public async Task<MapPin?> GetByAddressIdAsync(int addressID)
        {
            return await _context.MapPins
                .Where(mp => mp.Addresses.Any(a => a.AddressID == addressID))
                .Include(mp => mp.Addresses)
                .Include(mp => mp.BeaconRailroad)
                .Include(mp => mp.BeaconRailroad.Subdivision)
                .Include(mp => mp.BeaconRailroad.Subdivision.Railroad)
                .FirstOrDefaultAsync();
        }

        public async Task<MapPin?> GetByTrainIdAsync(int dpuTrainID)
        {
            return await _context.MapPins
                .Where(mp => mp.DpuTrainID == dpuTrainID)
                .Include(mp => mp.Addresses)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<MapPin>> GetAllAsync(int? minutes)
        {
            if (minutes.HasValue)
            {
                return await _context.MapPins
                    .Where(mp => mp.LastUpdate >= _timeProvider.UtcNow.AddMinutes(-minutes.Value))
                    .OrderByDescending(mp => mp.LastUpdate)
                    .Include(mp => mp.Addresses)
                    .Include(mp => mp.BeaconRailroad)
                    .Include(mp => mp.BeaconRailroad.Beacon)
                    .Include(mp => mp.BeaconRailroad.Subdivision)
                    .Include(mp => mp.BeaconRailroad.Subdivision.Railroad)
                    .ToListAsync();
            }
            else
            {
                return await _context.MapPins
                    .OrderByDescending(mp => mp.LastUpdate)
                    .Include(mp => mp.Addresses)
                    .Include(mp => mp.BeaconRailroad)
                    .Include(mp => mp.BeaconRailroad.Beacon)
                    .Include(mp => mp.BeaconRailroad.Subdivision)
                    .Include(mp => mp.BeaconRailroad.Subdivision.Railroad)
                    .ToListAsync();
            }
        }

        public async Task<MapPin> UpsertAsync(MapPin mapPin)
        {
            // Find existing map pin by matching address ID(s) and subdivision.
            var mapPinBAddressIds = mapPin.Addresses
                .Select(a => a.AddressID)
                .ToList();

            var existingMapPin = _context.MapPins
                .Where(pin => pin.Addresses.Any(a => mapPinBAddressIds.Contains(a.AddressID)))
                .Where(pin => pin.SubdivisionId == mapPin.SubdivisionId)
                .FirstOrDefault();

            if (existingMapPin == null)
            {
                mapPin.Addresses = mapPin.Addresses;
                mapPin.BeaconID = mapPin.BeaconID;
                mapPin.BeaconName = mapPin.BeaconName;
                mapPin.CreatedAt = _timeProvider.UtcNow;
                mapPin.SubdivisionId = mapPin.SubdivisionId;
                mapPin.Direction = mapPin.Direction;
                mapPin.DpuTrainID = mapPin.DpuTrainID;
                mapPin.LastUpdate = _timeProvider.UtcNow;
                mapPin.BeaconRailroad = null;

                _context.MapPins.Add(mapPin);
            }
            else
            {
                existingMapPin.CreatedAt = _timeProvider.UtcNow;
                existingMapPin.LastUpdate = _timeProvider.UtcNow;

                _context.MapPins.Update(existingMapPin);

                mapPin = existingMapPin;
            }

            await _context.SaveChangesAsync();

            return mapPin;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var mapPin = await _context.MapPins.FindAsync(id);
            if (mapPin == null)
            {
                return false;
            }
            _context.MapPins.Remove(mapPin);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
