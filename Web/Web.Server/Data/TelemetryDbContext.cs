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
        public DbSet<MapPinHistory> MapPinHistories { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Subdivision> Subdivisions { get; set; }
        public DbSet<Railroad> Railroads { get; set; }
        public DbSet<Telemetry> Telemetries { get; set; }
        public DbSet<UserTrackedPin> UserTrackedPins { get; set; }

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
                    .Property(br => br.Direction)
                    .HasConversion(new EnumToStringConverter<Direction>());

            // Composite keys
            modelBuilder.Entity<BeaconRailroad>()
                .HasKey(br => new { br.BeaconID, br.SubdivisionID });

            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            // Relationships
            modelBuilder.Entity<BeaconRailroad>()
                .HasOne(br => br.Beacon)
                .WithMany(b => b.BeaconRailroads)
                .HasForeignKey(br => br.BeaconID);

            modelBuilder.Entity<BeaconRailroad>()
                .HasOne(br => br.Subdivision)
                .WithMany(r => r.BeaconRailroads)
                .HasForeignKey(br => br.SubdivisionID);

            modelBuilder.Entity<Subdivision>()
                .HasOne(s => s.Railroad)
                .WithMany(r => r.Subdivisions)
                .HasForeignKey(s => s.RailroadID)
                .IsRequired();

            modelBuilder.Entity<MapPin>()
                .HasOne(mp => mp.BeaconRailroad)
                .WithMany() // <--- this is the key to preventing uniqueness
                .HasForeignKey(mp => new { mp.BeaconID, mp.SubdivisionId })
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Address>()
                .HasOne(a => a.MapPin)
                .WithMany(mp => mp.Addresses)
                .HasForeignKey(a => a.MapPinID);

            modelBuilder.Entity<Telemetry>()
                .HasOne(t => t.Beacon)
                .WithMany(b => b.Telemetries)
                .HasForeignKey(t => t.BeaconID);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            modelBuilder.Entity<UserTrackedPin>()
                .HasOne(utp => utp.User)
                .WithMany()
                .HasForeignKey(utp => utp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserTrackedPin>()
                .HasOne(utp => utp.MapPin)
                .WithMany()
                .HasForeignKey(utp => utp.MapPinId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
