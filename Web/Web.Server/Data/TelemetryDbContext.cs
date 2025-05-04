using Microsoft.EntityFrameworkCore;
using Web.Server.Entities;

namespace Web.Server.Data
{
    public class TelemetryDbContext : DbContext
    {
        public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options)
            : base(options)
        {
        }

        public DbSet<Beacon> Beacons { get; set; }
        public DbSet<Owner> Owners { get; set; }
        public DbSet<Railroad> Railroads { get; set; }
        public DbSet<Telemetry> Telemetries { get; set; }
    }
}
