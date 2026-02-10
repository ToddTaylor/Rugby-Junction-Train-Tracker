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
        /// Get MapPin by AddressID and optional TrainID. If TrainID is null, only an HOT/EOT match is performed (AddressID only).
        /// The addresses collection of the returned MapPin may contain addresses not queried for as all
        /// related addresses are returned.
        /// </summary>
        /// <param name="addressID">The HOT/EOT ID or the DPU ADDR value.</param>
        /// <param name="trainID">The DPU train ID.</param>
        /// <returns>MapPin containing matching in addresses collection or null if not found.</returns>
        public async Task<MapPin?> GetByAddressIdAsync(int addressID, int? trainID)
        {
            var mapPin = await _context.MapPins
                .Where(mp => mp.Addresses.Any(a =>
                    a.AddressID == addressID &&
                    ((trainID == null && a.DpuTrainID == null) ||
                     (trainID != null && a.DpuTrainID == trainID))
                ))
                .OrderByDescending(mp => mp.LastUpdate)
                .Include(mp => mp.Addresses)
                .Include(mp => mp.BeaconRailroad)
                .Include(mp => mp.BeaconRailroad.Subdivision)
                .Include(mp => mp.BeaconRailroad.Subdivision.Railroad)
                .FirstOrDefaultAsync();

            return mapPin;
        }

        public async Task<MapPin?> GetByTrainIdAsync(int trainID)
        {
            return await _context.MapPins
                .Where(mp => mp.Addresses.Any(a => a.DpuTrainID == trainID))
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
                mapPin.CreatedAt = _timeProvider.UtcNow;
                mapPin.LastUpdate = _timeProvider.UtcNow;

                mapPin.BeaconRailroad = null;

                _context.MapPins.Add(mapPin);
            }
            else
            {
                // Keep the existing CreatedAt to track how long pin has been at this beacon
                // Only update LastUpdate and other properties
                existingMapPin.BeaconID = mapPin.BeaconID;
                existingMapPin.SubdivisionId = mapPin.SubdivisionId;
                existingMapPin.Direction = mapPin.Direction;
                existingMapPin.Moving = mapPin.Moving;
                existingMapPin.IsLocal = mapPin.IsLocal;

                // No created at update.
                existingMapPin.LastUpdate = _timeProvider.UtcNow;

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
