using Microsoft.EntityFrameworkCore;
using Web.Server.Models;

namespace Web.Server.Data
{
    public class TelemetryDbContext : DbContext
    {
        public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options)
            : base(options)
        {
        }

        public DbSet<Telemetry> Telemetries { get; set; }
    }
}
