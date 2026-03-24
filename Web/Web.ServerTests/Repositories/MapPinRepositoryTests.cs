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
    public class MapPinRepositoryTests
    {
        [TestMethod]
        public async Task GetByAddressAndTrainIdAsync_ReturnsLatestMatchingMapPinWithinThreshold()
        {
            var now = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc);
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
                Name = "Neenah",
                CreatedAt = now,
                LastUpdate = now
            };

            var beaconRailroad = new BeaconRailroad
            {
                BeaconID = beacon.ID,
                Beacon = beacon,
                SubdivisionID = subdivision.ID,
                Subdivision = subdivision,
                Direction = Direction.NorthSouth,
                Latitude = 44.185,
                Longitude = -88.462,
                Milepost = 160,
                MultipleTracks = false,
                CreatedAt = now,
                LastUpdate = now
            };

            context.Users.Add(owner);
            context.Railroads.Add(railroad);
            context.Subdivisions.Add(subdivision);
            context.Beacons.Add(beacon);
            context.BeaconRailroads.Add(beaconRailroad);

            var matchingMapPin = new MapPin
            {
                ID = 100,
                BeaconID = beacon.ID,
                SubdivisionId = subdivision.ID,
                CreatedRailroadID = railroad.ID,
                Direction = null,
                Moving = true,
                IsLocal = false,
                BeaconRailroad = beaconRailroad,
                CreatedAt = now.AddMinutes(-5),
                LastUpdate = now.AddMinutes(-5),
                Addresses =
                [
                    new Address
                    {
                        AddressID = 55501,
                        DpuTrainID = 808,
                        Source = SourceEnum.DPU,
                        CreatedAt = now.AddMinutes(-5),
                        LastUpdate = now.AddMinutes(-5)
                    }
                ]
            };

            var nonMatchingTrainMapPin = new MapPin
            {
                ID = 101,
                BeaconID = beacon.ID,
                SubdivisionId = subdivision.ID,
                CreatedRailroadID = railroad.ID,
                Direction = null,
                Moving = true,
                IsLocal = false,
                BeaconRailroad = beaconRailroad,
                CreatedAt = now.AddMinutes(-1),
                LastUpdate = now.AddMinutes(-1),
                Addresses =
                [
                    new Address
                    {
                        AddressID = 55501,
                        DpuTrainID = 809,
                        Source = SourceEnum.DPU,
                        CreatedAt = now.AddMinutes(-1),
                        LastUpdate = now.AddMinutes(-1)
                    }
                ]
            };

            var outsideThresholdMapPin = new MapPin
            {
                ID = 102,
                BeaconID = beacon.ID,
                SubdivisionId = subdivision.ID,
                CreatedRailroadID = railroad.ID,
                Direction = null,
                Moving = true,
                IsLocal = false,
                BeaconRailroad = beaconRailroad,
                CreatedAt = now.AddMinutes(-90),
                LastUpdate = now.AddMinutes(-90),
                Addresses =
                [
                    new Address
                    {
                        AddressID = 55501,
                        DpuTrainID = 808,
                        Source = SourceEnum.DPU,
                        CreatedAt = now.AddMinutes(-90),
                        LastUpdate = now.AddMinutes(-90)
                    }
                ]
            };

            context.MapPins.AddRange(matchingMapPin, nonMatchingTrainMapPin, outsideThresholdMapPin);
            await context.SaveChangesAsync();

            var repository = new MapPinRepository(context, timeProviderMock.Object);

            var result = await repository.GetByAddressAndTrainIdAsync(55501, 808, 60);

            Assert.IsNotNull(result);
            Assert.AreEqual(matchingMapPin.ID, result.ID);
            Assert.IsTrue(result.Addresses.Any(a => a.AddressID == 55501 && a.DpuTrainID == 808));
        }

        [TestMethod]
        public async Task UpsertAsync_ExistingMovedDpuPin_RefreshesBeaconRailroadAndDirection()
        {
            var now = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc);
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

            var fromBeacon = new Beacon
            {
                ID = 10,
                OwnerID = owner.ID,
                Owner = owner,
                Name = "Neenah",
                CreatedAt = now,
                LastUpdate = now
            };

            var toBeacon = new Beacon
            {
                ID = 11,
                OwnerID = owner.ID,
                Owner = owner,
                Name = "Oshkosh",
                CreatedAt = now,
                LastUpdate = now
            };

            var fromBeaconRailroad = new BeaconRailroad
            {
                BeaconID = fromBeacon.ID,
                Beacon = fromBeacon,
                SubdivisionID = subdivision.ID,
                Subdivision = subdivision,
                Direction = Direction.NorthSouth,
                Latitude = 44.185,
                Longitude = -88.462,
                Milepost = 160,
                MultipleTracks = false,
                CreatedAt = now,
                LastUpdate = now
            };

            var toBeaconRailroad = new BeaconRailroad
            {
                BeaconID = toBeacon.ID,
                Beacon = toBeacon,
                SubdivisionID = subdivision.ID,
                Subdivision = subdivision,
                Direction = Direction.NorthSouth,
                Latitude = 44.024,
                Longitude = -88.531,
                Milepost = 172,
                MultipleTracks = false,
                CreatedAt = now,
                LastUpdate = now
            };

            context.Users.Add(owner);
            context.Railroads.Add(railroad);
            context.Subdivisions.Add(subdivision);
            context.Beacons.AddRange(fromBeacon, toBeacon);
            context.BeaconRailroads.AddRange(fromBeaconRailroad, toBeaconRailroad);

            var existingMapPin = new MapPin
            {
                ID = 100,
                BeaconID = fromBeacon.ID,
                SubdivisionId = subdivision.ID,
                CreatedRailroadID = railroad.ID,
                Direction = null,
                Moving = true,
                IsLocal = false,
                BeaconRailroad = fromBeaconRailroad,
                CreatedAt = now.AddMinutes(-10),
                LastUpdate = now.AddMinutes(-10),
                Addresses =
                [
                    new Address
                    {
                        AddressID = 99930,
                        DpuTrainID = 101,
                        Source = SourceEnum.DPU,
                        CreatedAt = now.AddMinutes(-10),
                        LastUpdate = now.AddMinutes(-10)
                    }
                ]
            };

            context.MapPins.Add(existingMapPin);
            await context.SaveChangesAsync();

            var repository = new MapPinRepository(context, timeProviderMock.Object);

            var updatedMapPin = new MapPin
            {
                ID = existingMapPin.ID,
                BeaconID = toBeacon.ID,
                SubdivisionId = subdivision.ID,
                CreatedRailroadID = railroad.ID,
                Direction = "N",
                Moving = true,
                IsLocal = false,
                BeaconRailroad = toBeaconRailroad,
                CreatedAt = existingMapPin.CreatedAt,
                LastUpdate = now,
                Addresses =
                [
                    new Address
                    {
                        AddressID = 99930,
                        DpuTrainID = 101,
                        Source = SourceEnum.DPU,
                        CreatedAt = now.AddMinutes(-10),
                        LastUpdate = now
                    },
                    new Address
                    {
                        AddressID = 99931,
                        DpuTrainID = 101,
                        Source = SourceEnum.DPU,
                        CreatedAt = now,
                        LastUpdate = now
                    }
                ]
            };

            var result = await repository.UpsertAsync(updatedMapPin, now);
            var persisted = await repository.GetByIdAsync(existingMapPin.ID);

            Assert.IsNotNull(result.BeaconRailroad);
            Assert.AreEqual(toBeacon.ID, result.BeaconRailroad.BeaconID);
            Assert.AreEqual("N", result.Direction);

            Assert.IsNotNull(persisted);
            Assert.AreEqual(toBeacon.ID, persisted.BeaconID);
            Assert.AreEqual(subdivision.ID, persisted.SubdivisionId);
            Assert.AreEqual("N", persisted.Direction);
            Assert.IsNotNull(persisted.BeaconRailroad);
            Assert.AreEqual(toBeacon.ID, persisted.BeaconRailroad.BeaconID);
            Assert.AreEqual("Oshkosh", persisted.BeaconRailroad.Beacon?.Name);
        }
    }
}