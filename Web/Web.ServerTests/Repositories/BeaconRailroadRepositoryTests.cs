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
            // LastUpdate is the sole signal BeaconRailroadHealthService uses to determine
            // online/offline. If UpdateAsync bumped it on ordinary edits, saving something as
            // unrelated as an OfflineNote would spuriously mark an offline beacon "online" again.
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
                Milepost = 101.5,
                MultipleTracks = true,
                OfflineNote = "Downed power line near milepost 101"
            };

            await repository.UpdateAsync(update);

            var persisted = await context.BeaconRailroads.FirstAsync(br => br.BeaconID == 1 && br.SubdivisionID == 1);
            Assert.AreEqual(seededLastUpdate, persisted.LastUpdate, "LastUpdate must be untouched by ordinary metadata/note edits.");
        }

        [TestMethod]
        public async Task UpdateAsync_PersistsOfflineNote_WhenSet()
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
                OfflineNote = "Downed power line near milepost 101"
            };

            await repository.UpdateAsync(update);

            var persisted = await context.BeaconRailroads.FirstAsync(br => br.BeaconID == 1 && br.SubdivisionID == 1);
            Assert.AreEqual("Downed power line near milepost 101", persisted.OfflineNote);
        }

        [TestMethod]
        public async Task UpdateAsync_ClearsOfflineNote_WhenNull()
        {
            var now = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);
            var (context, timeProviderMock) = BuildContext(now);

            await SeedBeaconRailroadAsync(context, now, telemetryStaleHoursOverride: null, offlineNote: "Existing note");

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
                OfflineNote = null
            };

            await repository.UpdateAsync(update);

            var persisted = await context.BeaconRailroads.FirstAsync(br => br.BeaconID == 1 && br.SubdivisionID == 1);
            Assert.IsNull(persisted.OfflineNote);
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
        public async Task GetLatestTelemetryTimestampAsync_ReturnsMostRecent_ForMatchingBeaconAndSubdivision()
        {
            var now = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);
            var (context, timeProviderMock) = BuildContext(now);
            await SeedBeaconRailroadAsync(context, now, telemetryStaleHoursOverride: null);

            var older = now.AddHours(-2);
            var newest = now.AddMinutes(-5);
            context.MapPinHistories.Add(new MapPinHistory
            {
                BeaconID = 1,
                SubdivisionId = 1,
                AddressesJson = "[]",
                CreatedAt = older,
                LastUpdate = older
            });
            context.MapPinHistories.Add(new MapPinHistory
            {
                BeaconID = 1,
                SubdivisionId = 1,
                AddressesJson = "[]",
                CreatedAt = newest,
                LastUpdate = newest
            });
            // Different subdivision — must not be picked up for BeaconID=1/SubdivisionID=1.
            context.MapPinHistories.Add(new MapPinHistory
            {
                BeaconID = 1,
                SubdivisionId = 2,
                AddressesJson = "[]",
                CreatedAt = now,
                LastUpdate = now
            });
            await context.SaveChangesAsync();

            var repository = new BeaconRailroadRepository(context, timeProviderMock.Object);
            var result = await repository.GetLatestTelemetryTimestampAsync(1, 1);

            Assert.AreEqual(newest, result);
        }

        [TestMethod]
        public async Task GetLatestTelemetryTimestampAsync_ReturnsNull_WhenNoTelemetryRecorded()
        {
            var now = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);
            var (context, timeProviderMock) = BuildContext(now);
            await SeedBeaconRailroadAsync(context, now, telemetryStaleHoursOverride: null);

            var repository = new BeaconRailroadRepository(context, timeProviderMock.Object);
            var result = await repository.GetLatestTelemetryTimestampAsync(1, 1);

            Assert.IsNull(result);
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

        private static async Task SeedBeaconRailroadAsync(TelemetryDbContext context, DateTime now, int? telemetryStaleHoursOverride, string? offlineNote = null)
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
                OfflineNote = offlineNote,
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