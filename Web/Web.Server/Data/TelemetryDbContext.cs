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
        public DbSet<BeaconDiagnostics> BeaconDiagnostics { get; set; }
        public DbSet<BeaconServerState> BeaconServerStates { get; set; }
        public DbSet<MapPin> MapPins { get; set; }
        public DbSet<MapPinHistory> MapPinHistories { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Subdivision> Subdivisions { get; set; }
        public DbSet<Railroad> Railroads { get; set; }
        public DbSet<Telemetry> Telemetries { get; set; }
        public DbSet<UserTrackedPin> UserTrackedPins { get; set; }
        public DbSet<SubdivisionTrackageRight> SubdivisionTrackageRights { get; set; }
        public DbSet<AuthToken> AuthTokens { get; set; }
        public DbSet<AmtrakTrackedTrain> AmtrakTrackedTrains { get; set; }
        public DbSet<AmtrakPollingConfiguration> AmtrakPollingConfigurations { get; set; }
        public DbSet<PassengerMapPin> PassengerMapPins { get; set; }

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

            modelBuilder.Entity<User>()
                .Property(u => u.LastActive)
                .HasConversion(Converters.NullableUtcDateTimeConverter);

            modelBuilder.Entity<User>()
                .Property(u => u.LastLogin)
                .HasConversion(Converters.NullableUtcDateTimeConverter);

            modelBuilder.Entity<BeaconDiagnostics>()
                .HasOne(d => d.Beacon)
                .WithOne()
                .HasForeignKey<BeaconDiagnostics>(d => d.BeaconID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BeaconDiagnostics>()
                .Property(d => d.CollectedAt)
                .HasConversion(Converters.UtcDateTimeConverter);

            modelBuilder.Entity<BeaconDiagnostics>()
                .Property(d => d.LastBootAt)
                .HasConversion(Converters.NullableUtcDateTimeConverter);

            modelBuilder.Entity<BeaconDiagnostics>()
                .Property(d => d.LastTelemetrySentAt)
                .HasConversion(Converters.NullableUtcDateTimeConverter);

            // Program, log and startup task details are only ever read as a whole, so they are stored as JSON text.
            modelBuilder.Entity<BeaconDiagnostics>()
                .Property(d => d.Programs)
                .HasConversion(Converters.JsonListConverter<DiagnosticsProgramStatus>(), Converters.JsonListComparer<DiagnosticsProgramStatus>());

            modelBuilder.Entity<BeaconDiagnostics>()
                .Property(d => d.Logs)
                .HasConversion(Converters.JsonListConverter<DiagnosticsLogFileStatus>(), Converters.JsonListComparer<DiagnosticsLogFileStatus>());

            modelBuilder.Entity<BeaconDiagnostics>()
                .Property(d => d.StartupTasks)
                .HasConversion(Converters.JsonListConverter<DiagnosticsStartupTaskStatus>(), Converters.JsonListComparer<DiagnosticsStartupTaskStatus>());

            modelBuilder.Entity<BeaconServerState>()
                .HasOne(s => s.Beacon)
                .WithOne()
                .HasForeignKey<BeaconServerState>(s => s.BeaconID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BeaconServerState>()
                .Property(s => s.Status)
                .HasConversion(new EnumToStringConverter<ServerStatus>());

            modelBuilder.Entity<BeaconServerState>()
                .Property(s => s.PendingStatus)
                .HasConversion(new EnumToStringConverter<ServerStatus>());

            modelBuilder.Entity<BeaconServerState>()
                .Property(s => s.StatusSince)
                .HasConversion(Converters.UtcDateTimeConverter);

            foreach (var property in new[]
            {
                nameof(BeaconServerState.PendingSince),
                nameof(BeaconServerState.AlertsMutedAt),
                nameof(BeaconServerState.AlertsMutedUntil),
            })
            {
                modelBuilder.Entity<BeaconServerState>()
                    .Property<DateTime?>(property)
                    .HasConversion(Converters.NullableUtcDateTimeConverter);
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

            modelBuilder.Entity<MapPin>()
                .HasIndex(mp => mp.ShareCode)
                .IsUnique();

            modelBuilder.Entity<MapPinHistory>()
                .HasIndex(mph => mph.ShareCode);

            modelBuilder.Entity<AmtrakTrackedTrain>()
                .HasIndex(t => t.TrainNumber)
                .IsUnique();

            modelBuilder.Entity<PassengerMapPin>()
                .HasIndex(p => p.TrainId)
                .IsUnique();

            modelBuilder.Entity<PassengerMapPin>()
                .Property(p => p.UpdatedAt)
                .HasConversion(Converters.UtcDateTimeConverter);

            modelBuilder.Entity<Address>()
                .HasOne(a => a.MapPin)
                .WithMany(mp => mp.Addresses)
                .HasForeignKey(a => a.MapPinID);

            modelBuilder.Entity<Telemetry>()
                .HasOne(t => t.Beacon)
                .WithMany(b => b.Telemetries)
                .HasForeignKey(t => t.BeaconID);

            modelBuilder.Entity<Telemetry>()
                .HasIndex(t => new { t.MapPinID, t.CreatedAt });

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

            // Trackage Rights relationships
            modelBuilder.Entity<SubdivisionTrackageRight>()
                .HasOne(str => str.FromSubdivision)
                .WithMany(s => s.HasTrackageRights)
                .HasForeignKey(str => str.FromSubdivisionID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubdivisionTrackageRight>()
                .HasOne(str => str.ToSubdivision)
                .WithMany(s => s.GrantedTrackageRights)
                .HasForeignKey(str => str.ToSubdivisionID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
