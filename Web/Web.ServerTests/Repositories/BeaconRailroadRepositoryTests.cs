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
        public async Task UpdateAsync_PersistsMaxDetectionDistanceMiles_WhenValueChanges()
        {
            var now = new DateTime(2026, 3, 22, 12, 0, 0, DateTimeKind.Utc);

            var options = new DbContextOptionsBuilder<TelemetryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var timeProviderMock = new Mock<ITimeProvider>();
            timeProviderMock.Setup(tp => tp.UtcNow).Returns(now);

            await using var context = new TelemetryDbContext(options);

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
                RailroadID = railroad.ID,
                Railroad = railroad,
                Name = "Neenah",
                DpuCapable = true,
                CreatedAt = now,
                LastUpdate = now
            };

            var beacon = new Beacon
            {
                ID = 10,
                OwnerID = owner.ID,
                Owner = owner,
                Name = "NEENAH",
                CreatedAt = now,
                LastUpdate = now
            };

            var existing = new BeaconRailroad
            {
                BeaconID = beacon.ID,
                Beacon = beacon,
                SubdivisionID = subdivision.ID,
                Subdivision = subdivision,
                Direction = Direction.NorthSouth,
                Latitude = 44.185,
                Longitude = -88.462,
                Milepost = 184.8,
                MaxDetectionDistanceMiles = null,
                MultipleTracks = false,
                CreatedAt = now,
                LastUpdate = now
            };

            context.Users.Add(owner);
            context.Railroads.Add(railroad);
            context.Subdivisions.Add(subdivision);
            context.Beacons.Add(beacon);
            context.BeaconRailroads.Add(existing);
            await context.SaveChangesAsync();

            var repository = new BeaconRailroadRepository(context, timeProviderMock.Object);

            var updateRequest = new BeaconRailroad
            {
                BeaconID = beacon.ID,
                SubdivisionID = subdivision.ID,
                Direction = Direction.NorthSouth,
                Latitude = 44.185,
                Longitude = -88.462,
                Milepost = 184.8,
                MaxDetectionDistanceMiles = 10.0,
                MultipleTracks = false
            };

            var updated = await repository.UpdateAsync(updateRequest);

            Assert.IsNotNull(updated);
            Assert.AreEqual(10.0, updated.MaxDetectionDistanceMiles);

            var persisted = await repository.GetByIdAsync(beacon.ID, subdivision.ID);
            Assert.IsNotNull(persisted);
            Assert.AreEqual(10.0, persisted.MaxDetectionDistanceMiles);
        }
    }
}
