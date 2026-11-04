using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class BeaconCommandRepository : IBeaconCommandRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public BeaconCommandRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<BeaconCommand> AddAsync(BeaconCommand command)
        {
            var utcNow = _timeProvider.UtcNow;
            command.CreatedAt = utcNow;
            command.LastUpdate = utcNow;
            _context.BeaconCommands.Add(command);
            await _context.SaveChangesAsync();
            return command;
        }

        public async Task<BeaconCommand?> GetByIdAsync(int id)
        {
            return await _context.BeaconCommands.AsNoTracking().FirstOrDefaultAsync(c => c.ID == id);
        }

        public async Task<IReadOnlyList<BeaconCommand>> GetRequestedSinceAsync(DateTime since)
        {
            // RequestedAt is stored as UTC text, so the comparison is done after loading; the table stays small.
            var commands = await _context.BeaconCommands.AsNoTracking().ToListAsync();
            return commands
                .Where(c => c.RequestedAt >= since)
                .OrderByDescending(c => c.RequestedAt)
                .ThenByDescending(c => c.ID)
                .ToList();
        }

        public async Task<IReadOnlyList<BeaconCommand>> GetForBeaconSinceAsync(int beaconId, DateTime since)
        {
            var commands = await _context.BeaconCommands.AsNoTracking().Where(c => c.BeaconID == beaconId).ToListAsync();
            return commands
                .Where(c => c.RequestedAt >= since)
                .OrderByDescending(c => c.RequestedAt)
                .ThenByDescending(c => c.ID)
                .ToList();
        }

        public async Task UpdateAsync(BeaconCommand command)
        {
            var existing = await _context.BeaconCommands.FindAsync(command.ID)
                ?? throw new KeyNotFoundException($"Beacon command {command.ID} not found.");

            existing.DeliveredAt = command.DeliveredAt;
            existing.StartedAt = command.StartedAt;
            existing.FinishedAt = command.FinishedAt;
            existing.Outcome = command.Outcome;
            existing.ResultMessage = command.ResultMessage;
            existing.CancelledAt = command.CancelledAt;
            existing.CancelledBy = command.CancelledBy;
            existing.LastUpdate = _timeProvider.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
