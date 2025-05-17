using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Web.Server.Entities;
using Web.Server.Enums;

namespace Web.Server.Data
{
    public class TelemetryDbContext : DbContext
    {
        public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options)
            : base(options)
        {
        }

        public DbSet<Beacon> Beacons { get; set; }
        public DbSet<BeaconRailroad> BeaconRailroads { get; set; }
        public DbSet<MapPin> MapPins { get; set; }
        public DbSet<Owner> Owners { get; set; }
        public DbSet<Railroad> Railroads { get; set; }
        public DbSet<Telemetry> Telemetries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Converts the Direction enum to text in the database instead of an int
            modelBuilder.Entity<BeaconRailroad>()
                .Property(t => t.Direction)
                .HasConversion(new EnumToStringConverter<Direction>());

            modelBuilder.Entity<BeaconRailroad>()
                .HasKey(br => new { br.BeaconID, br.RailroadID }); // composite key

            modelBuilder.Entity<BeaconRailroad>()
                .HasOne(br => br.Beacon)
                .WithMany(b => b.BeaconRailroads)
                .HasForeignKey(br => br.BeaconID);

            modelBuilder.Entity<BeaconRailroad>()
                .HasOne(br => br.Railroad)
                .WithMany(r => r.BeaconRailroads)
                .HasForeignKey(br => br.RailroadID);
        }
    }
}
