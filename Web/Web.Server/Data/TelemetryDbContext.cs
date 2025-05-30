using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Reflection;
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

        public DbSet<Address> Addresses { get; set; }
        public DbSet<Beacon> Beacons { get; set; }
        public DbSet<BeaconRailroad> BeaconRailroads { get; set; }
        public DbSet<MapPin> MapPins { get; set; }
        public DbSet<Owner> Owners { get; set; }
        public DbSet<Railroad> Railroads { get; set; }
        public DbSet<Telemetry> Telemetries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Ensure all DateTime properties named "CreatedAt" or "LastUpdate" are stored in UTC ISO 8601 format
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.ClrType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.PropertyType == typeof(DateTime) && (p.Name == "CreatedAt" || p.Name == "LastUpdate"));

                foreach (var prop in properties)
                {
                    modelBuilder.Entity(entityType.Name)
                        .Property(prop.Name)
                        .HasConversion(Converters.UtcDateTimeConverter);
                }
            }

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

            modelBuilder.Entity<MapPin>()
                .HasOne(mp => mp.BeaconRailroad)
                .WithMany() // <--- this is the key to preventing uniqueness
                .HasForeignKey(mp => new { mp.BeaconID, mp.RailroadID })
                .OnDelete(DeleteBehavior.Restrict); // or whatever behavior you want

            modelBuilder.Entity<Address>()
                .HasOne(a => a.MapPin)
                .WithMany(mp => mp.Addresses)
                .HasForeignKey(a => a.MapPinID);

            modelBuilder.Entity<Telemetry>()
                .HasOne(t => t.Beacon)
                .WithMany(b => b.Telemetries)
                .HasForeignKey(t => t.BeaconID);

            modelBuilder.Entity<Telemetry>()
                .HasOne(t => t.MapPin)
                .WithMany(b => b.Telemetries)
                .HasForeignKey(t => t.MapPinID);

        }
    }
}
