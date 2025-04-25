using Web.Server.Data;
using Web.Server.Models;

namespace Web.Server.Services
{
    public class TelemetryRepository : ITelemetryRepository
    {
        private readonly TelemetryDbContext _context;

        public TelemetryRepository(TelemetryDbContext context)
        {
            _context = context;
        }

        public async Task<Telemetry> AddAsync(Telemetry telemetry)
        {
            _context.Telemetries.Add(telemetry);
            await _context.SaveChangesAsync();
            return telemetry;
        }
    }
}
