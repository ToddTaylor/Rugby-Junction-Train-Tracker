using Microsoft.EntityFrameworkCore;
using Moq;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.ServerTests.Repositories
{
    [TestClass]
    public class BeaconRailroadRepositoryTests
    {
        [TestMethod]
        public async Task UpdateAsync_PersistsTelemetryStaleHoursOverride_WhenSet()
        {
            var now = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);
            var (context, timeProviderMock) = BuildContext(now);

            await SeedBeaconRailroadAsync(context, now, telemetryStaleHoursOverride: null);

            var repository = new BeaconRailroadRepository(context, timeProviderMock.Object);
            var update = new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = 1,
                Direction = Direction.NorthSouth,
                Latitude = 43.3,
                Longitude = -88.2,
                Milepost = 101.5,
                MultipleTracks = true,
                TelemetryStaleHoursOverride = 1
            };

            await repository.UpdateAsync(update);

            var persisted = await context.BeaconRailroads.FirstAsync(br => br.BeaconID == 1 && br.SubdivisionID == 1);
            Assert.AreEqual(1, persisted.TelemetryStaleHoursOverride);
        }

        [TestMethod]
        public async Task UpdateAsync_DoesNotBumpLastUpdate()
        {
            // LastUpdate records health check-ins, which Server Health uses to decide whether a
            // beacon PC is offline. If UpdateAsync bumped it on ordinary edits, saving a metadata
            // change would spuriously mark an offline beacon "online" again.
            var seededLastUpdate = new DateTime(2026, 7, 17, 11, 0, 0, DateTimeKind.Utc); // 1 hour before the update call
            var updateCallTime = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);
            var (context, timeProviderMock) = BuildContext(updateCallTime);

            await SeedBeaconRailroadAsync(context, seededLastUpdate, telemetryStaleHoursOverride: null);

            var repository = new BeaconRailroadRepository(context, timeProviderMock.Object);
            var update = new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = 1,
                Direction = Direction.NorthSouth,
                Latitude = 43.3,
                Longitude = -88.2,
                Milepost = 102.0,
                MultipleTracks = true
            };

            await repository.UpdateAsync(update);

            var persisted = await context.BeaconRailroads.FirstAsync(br => br.BeaconID == 1 && br.SubdivisionID == 1);
            Assert.AreEqual(seededLastUpdate, persisted.LastUpdate, "LastUpdate must be untouched by ordinary metadata edits.");
        }

        [TestMethod]
        public async Task UpdateAsync_ClearsTelemetryStaleHoursOverride_WhenNull()
        {
            var now = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);
            var (context, timeProviderMock) = BuildContext(now);

            await SeedBeaconRailroadAsync(context, now, telemetryStaleHoursOverride: 6);

            var repository = new BeaconRailroadRepository(context, timeProviderMock.Object);
            var update = new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = 1,
                Direction = Direction.NorthSouth,
                Latitude = 43.3,
                Longitude = -88.2,
                Milepost = 101.5,
                MultipleTracks = true,
                TelemetryStaleHoursOverride = null
            };

            await repository.UpdateAsync(update);

            var persisted = await context.BeaconRailroads.FirstAsync(br => br.BeaconID == 1 && br.SubdivisionID == 1);
            Assert.IsNull(persisted.TelemetryStaleHoursOverride);
        }

        [TestMethod]
        public async Task TouchBeaconHealthAsync_StampsEverySubdivisionOfTheBeacon()
        {
            var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
            var (context, timeProviderMock) = BuildContext(now);

            var stale = now.AddHours(-2);
            await SeedJunctionBeaconAsync(context, stale);

            var repository = new BeaconRailroadRepository(context, timeProviderMock.Object);

            await repository.TouchBeaconHealthAsync(beaconId: 17, timestampUtc: now);

            var rows = await context.BeaconRailroads
                .Where(br => br.BeaconID == 17)
                .ToListAsync();

            Assert.HasCount(2, rows);
            Assert.IsTrue(rows.All(br => br.LastUpdate == now),
                "Both subdivisions of one physical beacon must share a health timestamp.");
        }

        [TestMethod]
        public async Task TouchBeaconHealthAsync_LeavesOtherBeaconsAlone()
        {
            var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
            var (context, timeProviderMock) = BuildContext(now);

            var stale = now.AddHours(-2);
            await SeedJunctionBeaconAsync(context, stale);

            var repository = new BeaconRailroadRepository(context, timeProviderMock.Object);

            await repository.TouchBeaconHealthAsync(beaconId: 17, timestampUtc: now);

            var otherBeaconRow = await context.BeaconRailroads.FirstAsync(br => br.BeaconID == 18);
            Assert.AreEqual(stale, otherBeaconRow.LastUpdate);
        }

        /// <summary>
        /// Seeds Junction City, where the CN Superior and CN Valley subdivisions cross, plus a
        /// second beacon elsewhere on the same railroad.
        /// </summary>
        private static async Task SeedJunctionBeaconAsync(TelemetryDbContext context, DateTime timestamp)
        {
            var railroad = new Railroad { ID = 1, Name = "CN", Subdivisions = [], CreatedAt = timestamp, LastUpdate = timestamp };

            var superior = new Subdivision
            {
                ID = 4,
                RailroadID = 1,
                Railroad = railroad,
                Name = "Superior",
                CreatedAt = timestamp,
                LastUpdate = timestamp
            };

            var valley = new Subdivision
            {
                ID = 10,
                RailroadID = 1,
                Railroad = railroad,
                Name = "Valley",
                CreatedAt = timestamp,
                LastUpdate = timestamp
            };

            var owner = new User
            {
                ID = 1,
                FirstName = "Test",
                LastName = "Owner",
                Email = "owner@example.com",
                IsActive = true,
                CreatedAt = timestamp,
                LastUpdate = timestamp
            };

            var junctionCity = new Beacon { ID = 17, OwnerID = 1, Owner = owner, Name = "Junction City", CreatedAt = timestamp, LastUpdate = timestamp };
            var mosinee = new Beacon { ID = 18, OwnerID = 1, Owner = owner, Name = "Mosinee", CreatedAt = timestamp, LastUpdate = timestamp };

            BeaconRailroad Row(Beacon beacon, Subdivision subdivision, double milepost) => new()
            {
                BeaconID = beacon.ID,
                SubdivisionID = subdivision.ID,
                Beacon = beacon,
                Subdivision = subdivision,
                Direction = Direction.All,
                Latitude = 44.589494,
                Longitude = -89.761417,
                Milepost = milepost,
                CreatedAt = timestamp,
                LastUpdate = timestamp
            };

            context.Users.Add(owner);
            context.Railroads.Add(railroad);
            context.Subdivisions.AddRange(superior, valley);
            context.Beacons.AddRange(junctionCity, mosinee);
            context.BeaconRailroads.AddRange(
                Row(junctionCity, superior, 260.0),
                Row(junctionCity, valley, 63.2),
                Row(mosinee, valley, 78.5));

            await context.SaveChangesAsync();
        }

        private static (TelemetryDbContext Context, Mock<ITimeProvider> TimeProviderMock) BuildContext(DateTime now)
        {
            var options = new DbContextOptionsBuilder<TelemetryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var timeProviderMock = new Mock<ITimeProvider>();
            timeProviderMock.Setup(tp => tp.UtcNow).Returns(now);

            var context = new TelemetryDbContext(options);
            return (context, timeProviderMock);
        }

        private static async Task SeedBeaconRailroadAsync(TelemetryDbContext context, DateTime now, int? telemetryStaleHoursOverride)
        {
            var owner = new User
            {
                ID = 1,
                FirstName = "Test",
                LastName = "Owner",
                Email = "owner@example.com",
                IsActive = true,
                CreatedAt = now,
                LastUpdate = now
            };

            var railroad = new Railroad
            {
                ID = 1,
                Name = "CN",
                Subdivisions = [],
                CreatedAt = now,
                LastUpdate = now
            };

            var subdivision = new Subdivision
            {
                ID = 1,
                RailroadID = 1,
                Railroad = railroad,
                Name = "Neenah",
                DpuCapable = true,
                CreatedAt = now,
                LastUpdate = now
            };

            var beacon = new Beacon
            {
                ID = 1,
                OwnerID = 1,
                Owner = owner,
                Name = "Beacon A",
                CreatedAt = now,
                LastUpdate = now
            };

            var beaconRailroad = new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = 1,
                Beacon = beacon,
                Subdivision = subdivision,
                Direction = Direction.All,
                Latitude = 43.1,
                Longitude = -88.1,
                Milepost = 100.0,
                MultipleTracks = false,
                TelemetryStaleHoursOverride = telemetryStaleHoursOverride,
                CreatedAt = now,
                LastUpdate = now
            };

            context.Users.Add(owner);
            context.Railroads.Add(railroad);
            context.Subdivisions.Add(subdivision);
            context.Beacons.Add(beacon);
            context.BeaconRailroads.Add(beaconRailroad);

            await context.SaveChangesAsync();
        }
    }
}