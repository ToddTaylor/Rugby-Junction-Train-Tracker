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

        /// <summary>
        /// Get MapPin by AddressID only for HOT/EOT only. (DpuTrainID is null)
        /// The addresses collection of the returned MapPin may contain addresses not queried for as all
        /// related addresses are returned.
        /// </summary>
        /// <param name="addressID">The HOT/EOT ID or the DPU ADDR value.</param>
        /// <returns>MapPin containing matching in addresses collection or null if not found.</returns>
        public async Task<MapPin?> GetByAddressIdAsync(int addressID)
        {
            var mapPin = await _context.MapPins
                .Where(mp => mp.Addresses.Any(a => a.AddressID == addressID && a.DpuTrainID == null))
                .OrderByDescending(mp => mp.LastUpdate)
                .Include(mp => mp.Addresses)
                .Include(mp => mp.BeaconRailroad)
                .Include(mp => mp.BeaconRailroad.Subdivision)
                .Include(mp => mp.BeaconRailroad.Subdivision.Railroad)
                .FirstOrDefaultAsync();

            return mapPin;
        }

        public async Task<MapPin?> GetByAddressAndTrainIdAsync(int addressID, int dpuTrainID, int minutesThreshold)
        {
            return await _context.MapPins
                .Where(mp => mp.Addresses.Any(a => a.AddressID == addressID && a.DpuTrainID == dpuTrainID) &&
                    mp.LastUpdate >= _timeProvider.UtcNow.AddMinutes(-minutesThreshold))
                .OrderByDescending(mp => mp.LastUpdate)
                .Include(mp => mp.Addresses)
                .Include(mp => mp.BeaconRailroad)
                .Include(mp => mp.BeaconRailroad.Subdivision)
                .Include(mp => mp.BeaconRailroad.Subdivision.Railroad)
                .FirstOrDefaultAsync();
        }

        public async Task<MapPin?> GetByTrainIdAsync(int trainID, int minutesThreshold)
        {
            return await _context.MapPins
                .Where(mp => mp.Addresses.Any(a => a.DpuTrainID == trainID) &&
                    mp.LastUpdate >= _timeProvider.UtcNow.AddMinutes(-minutesThreshold))
                .OrderByDescending(mp => mp.LastUpdate)
                .Include(mp => mp.Addresses)
                .Include(mp => mp.BeaconRailroad)
                .Include(mp => mp.BeaconRailroad.Subdivision)
                .Include(mp => mp.BeaconRailroad.Subdivision.Railroad)
                .FirstOrDefaultAsync();
        }

        public async Task<MapPin?> GetByIdAsync(int id)
        {
            return await _context.MapPins
                .Include(mp => mp.Addresses)
                .Include(mp => mp.BeaconRailroad)
                .Include(mp => mp.BeaconRailroad.Subdivision)
                .Include(mp => mp.BeaconRailroad.Subdivision.Railroad)
                .FirstOrDefaultAsync(mp => mp.ID == id);
        }

        public async Task<IEnumerable<MapPin>> GetLatestAsync()
        {
            var mapPins = await _context.MapPins
                .Include(mp => mp.BeaconRailroad)
                    .ThenInclude(br => br.Subdivision)
                        .ThenInclude(sub => sub.Railroad)
                .GroupBy(mp => new { mp.BeaconID, mp.SubdivisionId })
                .Select(g => g.OrderByDescending(mp => mp.LastUpdate).First())
                .ToListAsync();

            return mapPins;
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

        public async Task<IEnumerable<MapPin>> GetAllByBeaconAsync(int beaconID, int subdivisionID, int? minutesThreshold = null)
        {
            var query = _context.MapPins
                .Where(mp => mp.BeaconID == beaconID && mp.SubdivisionId == subdivisionID);

            if (minutesThreshold.HasValue)
            {
                query = query.Where(mp => mp.LastUpdate >= _timeProvider.UtcNow.AddMinutes(-minutesThreshold.Value));
            }

            return await query
                .Include(mp => mp.Addresses)
                .Include(mp => mp.BeaconRailroad)
                .Include(mp => mp.BeaconRailroad.Subdivision)
                .Include(mp => mp.BeaconRailroad.Subdivision.Railroad)
                .ToListAsync();
        }

        public async Task<MapPin> UpsertAsync(MapPin mapPin, DateTime telemetryTimestamp)
        {
            // Find existing map pin by matching address ID(s) and subdivision.
            var mapPinBAddressIds = mapPin.Addresses
                .Select(a => a.AddressID)
                .ToList();

            var existingMapPin = _context.MapPins
                .Where(pin => pin.ID == mapPin.ID)
                .FirstOrDefault();

            if (existingMapPin == null)
            {
                mapPin.CreatedAt = telemetryTimestamp;
                mapPin.LastUpdate = telemetryTimestamp;

                mapPin.BeaconRailroad = null;

                _context.MapPins.Add(mapPin);
            }
            else
            {
                // Keep the existing CreatedAt to track how long pin has been at this beacon
                // Only update LastUpdate and other properties
                existingMapPin.BeaconID = mapPin.BeaconID;
                existingMapPin.SubdivisionId = mapPin.SubdivisionId;
                existingMapPin.CreatedRailroadID = mapPin.CreatedRailroadID;
                existingMapPin.Direction = mapPin.Direction;
                existingMapPin.Moving = mapPin.Moving;
                existingMapPin.IsLocal = mapPin.IsLocal;
                existingMapPin.BeaconRailroad = mapPin.BeaconRailroad;

                // No created at update.
                existingMapPin.LastUpdate = telemetryTimestamp;

                // Update addresses collection
                existingMapPin.Addresses = mapPin.Addresses;

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
