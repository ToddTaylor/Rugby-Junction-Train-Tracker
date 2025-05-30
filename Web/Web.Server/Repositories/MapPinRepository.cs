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

        public async Task<MapPin?> GetByIdAsync(int addressID)
        {
            return await _context.MapPins
                .Where(mp => mp.Addresses.Any(a => a.AddressID == addressID))
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
                    .ToListAsync();
            }
            else
            {
                return await _context.MapPins
                    .OrderByDescending(mp => mp.LastUpdate)
                    .Include(mp => mp.Addresses)
                    .ToListAsync();
            }
        }

        public async Task<MapPin> UpsertAsync(MapPin mapPin)
        {
            // Find existing map pin by matching address ID(s).
            var mapPinBAddressIds = mapPin.Addresses
                .Select(a => a.AddressID)
                .ToList();

            var existingMapPin = _context.MapPins
                .Where(pin => pin.Addresses.Any(a => mapPinBAddressIds.Contains(a.AddressID)))
                .FirstOrDefault();

            if (existingMapPin == null)
            {
                mapPin.Addresses = mapPin.Addresses;
                mapPin.BeaconID = mapPin.BeaconID;
                mapPin.CreatedAt = _timeProvider.UtcNow;
                mapPin.RailroadID = mapPin.RailroadID;
                mapPin.Direction = mapPin.Direction;
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
    }
}
