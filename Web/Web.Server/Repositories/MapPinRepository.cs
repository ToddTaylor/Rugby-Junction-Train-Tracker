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
                .FirstOrDefaultAsync(mp => mp.AddressID == addressID);
        }

        public async Task<IEnumerable<MapPin>> GetAllAsync(int? minutes)
        {
            if (minutes.HasValue)
            {
                return await _context.MapPins
                    .Where(mp => mp.LastUpdate >= _timeProvider.UtcNow.AddMinutes(-minutes.Value))
                    .OrderByDescending(mp => mp.LastUpdate)
                    .ToListAsync();
            }
            else
            {
                return await _context.MapPins.ToListAsync();
            }
        }

        public async Task<MapPin> UpsertAsync(MapPin mapPin)
        {
            var existingMapPin = _context.MapPins
                .Where(mp => mp.AddressID == mapPin.AddressID)
                .FirstOrDefault();

            if (existingMapPin == null)
            {
                mapPin.CreatedAt = _timeProvider.UtcNow;
                mapPin.BeaconRailroad = null;
                _context.MapPins.Add(mapPin);
            }
            else
            {
                existingMapPin.BeaconID = mapPin.BeaconID;
                existingMapPin.RailroadID = mapPin.RailroadID;
                existingMapPin.Direction = mapPin.Direction;
                existingMapPin.Moving = mapPin.Moving;
                existingMapPin.Source = mapPin.Source;
                existingMapPin.LastUpdate = _timeProvider.UtcNow;

                _context.MapPins.Update(existingMapPin);

                mapPin = existingMapPin;
            }

            await _context.SaveChangesAsync();

            return mapPin;
        }
    }
}
