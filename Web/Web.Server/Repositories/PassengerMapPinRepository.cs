using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class PassengerMapPinRepository : IPassengerMapPinRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public PassengerMapPinRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<IEnumerable<PassengerMapPin>> GetAllAsync()
        {
            return await _context.PassengerMapPins
                .OrderBy(p => p.TrainNum)
                .ThenByDescending(p => p.LastUpdate)
                .ToListAsync();
        }

        public async Task<PassengerMapPin?> GetByTrainIdAsync(string trainId)
        {
            return await _context.PassengerMapPins.FirstOrDefaultAsync(p => p.TrainId == trainId);
        }

        public async Task<PassengerMapPin?> GetByTrainNumberAsync(string trainNum)
        {
            return await _context.PassengerMapPins
                .Where(p => p.TrainNum == trainNum)
                .OrderByDescending(p => p.LastUpdate)
                .FirstOrDefaultAsync();
        }

        public async Task<PassengerMapPin> UpsertAsync(PassengerMapPin passengerMapPin)
        {
            var existing = await GetByTrainIdAsync(passengerMapPin.TrainId);
            if (existing == null)
            {
                passengerMapPin.CreatedAt = _timeProvider.UtcNow;
                passengerMapPin.LastUpdate = _timeProvider.UtcNow;
                _context.PassengerMapPins.Add(passengerMapPin);
                await _context.SaveChangesAsync();
                return passengerMapPin;
            }

            existing.Provider = passengerMapPin.Provider;
            existing.RouteName = passengerMapPin.RouteName;
            existing.TrainNum = passengerMapPin.TrainNum;
            existing.Heading = passengerMapPin.Heading;
            existing.Latitude = passengerMapPin.Latitude;
            existing.Longitude = passengerMapPin.Longitude;
            existing.Velocity = passengerMapPin.Velocity;
            existing.UpdatedAt = passengerMapPin.UpdatedAt;
            existing.IsStale = passengerMapPin.IsStale;
            existing.LastUpdate = _timeProvider.UtcNow;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> MarkStaleByTrainNumberAsync(string trainNum)
        {
            var entity = await GetByTrainNumberAsync(trainNum);
            if (entity == null)
            {
                return false;
            }

            entity.IsStale = true;
            entity.LastUpdate = _timeProvider.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteByTrainIdAsync(string trainId)
        {
            var entity = await GetByTrainIdAsync(trainId);
            if (entity == null)
            {
                return false;
            }

            _context.PassengerMapPins.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteByTrainNumberAsync(string trainNum)
        {
            var entities = await _context.PassengerMapPins
                .Where(p => p.TrainNum == trainNum)
                .ToListAsync();

            if (entities.Count == 0)
            {
                return false;
            }

            _context.PassengerMapPins.RemoveRange(entities);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}