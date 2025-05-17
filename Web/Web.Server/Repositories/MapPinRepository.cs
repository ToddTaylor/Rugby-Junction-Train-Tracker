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

        public async Task<MapPin> UpsertAsync(MapPin mapPin)
        {
            var existingMapPin = await _context.MapPins.FindAsync(mapPin.AddressID);

            if (existingMapPin == null)
            {
                _context.MapPins.Add(mapPin);
            }
            else
            {
                existingMapPin.Direction = mapPin.Direction;
                existingMapPin.Latitude = mapPin.Latitude;
                existingMapPin.Longitude = mapPin.Longitude;
                existingMapPin.Moving = mapPin.Moving;
                existingMapPin.Source = mapPin.Source;
                existingMapPin.CreatedAt = _timeProvider.UtcNow;

                _context.MapPins.Update(existingMapPin);

                mapPin = existingMapPin;
            }

            await _context.SaveChangesAsync();

            return mapPin;
        }

        public async Task<IEnumerable<MapPin>> GetAllAsync(int? minutes)
        {
            if (minutes.HasValue)
            {
                return await _context.MapPins
                    .Where(mp => mp.CreatedAt >= _timeProvider.UtcNow.AddMinutes(-minutes.Value)).ToListAsync();
            }
            else
            {
                return await _context.MapPins.ToListAsync();
            }
        }
    }
}
