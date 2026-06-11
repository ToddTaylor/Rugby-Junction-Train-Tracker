using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class AmtrakPollingConfigurationRepository : IAmtrakPollingConfigurationRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public AmtrakPollingConfigurationRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<AmtrakPollingConfiguration?> GetAsync()
        {
            return await _context.AmtrakPollingConfigurations
                .OrderBy(c => c.ID)
                .FirstOrDefaultAsync();
        }

        public async Task<AmtrakPollingConfiguration> UpsertAsync(int pollIntervalMinutes)
        {
            var existing = await GetAsync();
            if (existing == null)
            {
                existing = new AmtrakPollingConfiguration
                {
                    PollIntervalMinutes = pollIntervalMinutes,
                    CreatedAt = _timeProvider.UtcNow,
                    LastUpdate = _timeProvider.UtcNow
                };

                _context.AmtrakPollingConfigurations.Add(existing);
            }
            else
            {
                existing.PollIntervalMinutes = pollIntervalMinutes;
                existing.LastUpdate = _timeProvider.UtcNow;
            }

            await _context.SaveChangesAsync();
            return existing;
        }
    }
}