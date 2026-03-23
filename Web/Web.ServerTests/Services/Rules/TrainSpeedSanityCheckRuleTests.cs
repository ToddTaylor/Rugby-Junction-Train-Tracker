using Moq;
using Web.Server.Entities;
using Web.Server.Repositories;
using Web.Server.Services.Rules;

namespace Web.ServerTests.Services.Rules
{
    [TestClass]
    public class TrainSpeedSanityCheckRuleTests
    {
        private TrainSpeedSanityCheckRule _rule = null!;
        private Mock<ITelemetryRepository> _mockTelemetryRepository = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockTelemetryRepository = new Mock<ITelemetryRepository>();
            _rule = new TrainSpeedSanityCheckRule(_mockTelemetryRepository.Object);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenLessThanTwoTelemetryEntries()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;

            var subdivision = new Subdivision { RailroadID = 1 };
            var beaconRailroad = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 10.0,
                Subdivision = subdivision
            };

            var beacon = new Beacon
            {
                ID = 1,
                BeaconRailroads = new List<BeaconRailroad> { beaconRailroad }
            };

            var telemetry = new Telemetry
            {
                BeaconID = 1,
                AddressID = 100,
                CreatedAt = currentTime,
                Beacon = beacon
            };

            var context = new TelemetryRuleContext
            {
                Telemetry = telemetry,
                RailroadId = 1,
                ToMilepost = beaconRailroad.Milepost,
                FromMilepost = double.NaN // No prior beacon
            };

            // Only one telemetry entry
            _mockTelemetryRepository
                .Setup(r => r.GetRecentsWithinTimeOffsetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Telemetry> { telemetry });

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenRealisticSpeed()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-60); // 1 hour earlier

            var subdivision = new Subdivision { RailroadID = 1 };

            var currentBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 11.0,
                Subdivision = subdivision
            };

            var priorBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 10.0,
                Subdivision = subdivision
            };

            var currentBeacon = new Beacon
            {
                ID = 2,
                BeaconRailroads = new List<BeaconRailroad> { currentBeaconRailroad }
            };

            var priorBeacon = new Beacon
            {
                ID = 1,
                BeaconRailroads = new List<BeaconRailroad> { priorBeaconRailroad }
            };

            var currentTelemetry = new Telemetry
            {
                BeaconID = 2,
                AddressID = 100,
                CreatedAt = currentTime,
                Beacon = currentBeacon
            };

            var priorTelemetry = new Telemetry
            {
                BeaconID = 1,
                AddressID = 100,
                CreatedAt = previousTime,
                Beacon = priorBeacon
            };

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = 1,
                ToMilepost = currentBeaconRailroad.Milepost,
                FromMilepost = priorBeaconRailroad.Milepost
            };

            _mockTelemetryRepository
                .Setup(r => r.GetRecentsWithinTimeOffsetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Telemetry> { currentTelemetry, priorTelemetry });

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - 1 mile in 60 minutes, adjusted 1 - 4 - 0.75 = negative (0) = 0 mph
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenSpeedExceeds60Mph()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-20); // 20 minutes earlier

            var subdivision = new Subdivision { RailroadID = 1 };

            var currentBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 70.0,
                Subdivision = subdivision
            };

            var priorBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 10.0,
                Subdivision = subdivision
            };

            var currentBeacon = new Beacon
            {
                ID = 2,
                BeaconRailroads = new List<BeaconRailroad> { currentBeaconRailroad }
            };

            var priorBeacon = new Beacon
            {
                ID = 1,
                BeaconRailroads = new List<BeaconRailroad> { priorBeaconRailroad }
            };

            var currentTelemetry = new Telemetry
            {
                BeaconID = 2,
                AddressID = 100,
                CreatedAt = currentTime,
                Beacon = currentBeacon
            };

            var priorTelemetry = new Telemetry
            {
                BeaconID = 1,
                AddressID = 100,
                CreatedAt = previousTime,
                Beacon = priorBeacon
            };

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = 1,
                ToMilepost = currentBeaconRailroad.Milepost,
                FromMilepost = priorBeaconRailroad.Milepost
            };

            _mockTelemetryRepository
                .Setup(r => r.GetRecentsWithinTimeOffsetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Telemetry> { currentTelemetry, priorTelemetry });

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - 60 miles in 20 minutes, adjusted: 60 - 4 - 0.75 = 55.25 miles
            // Speed: 55.25 / (20/60) = 165.75 mph, exceeds 60 mph threshold
            Assert.IsTrue(result.ShouldDiscard);
            Assert.Contains(TrainSpeedSanityCheckRule.DISCARD_REASON, result.Reason);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNegativeTimeDifference()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var futureTime = currentTime.AddMinutes(10);

            var subdivision = new Subdivision { RailroadID = 1 };

            var currentBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 70.0,
                Subdivision = subdivision
            };

            var priorBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 10.0,
                Subdivision = subdivision
            };

            var currentBeacon = new Beacon
            {
                ID = 2,
                BeaconRailroads = new List<BeaconRailroad> { currentBeaconRailroad }
            };

            var priorBeacon = new Beacon
            {
                ID = 1,
                BeaconRailroads = new List<BeaconRailroad> { priorBeaconRailroad }
            };

            var currentTelemetry = new Telemetry
            {
                BeaconID = 2,
                AddressID = 100,
                CreatedAt = currentTime,
                Beacon = currentBeacon
            };

            var priorTelemetry = new Telemetry
            {
                BeaconID = 1,
                AddressID = 100,
                CreatedAt = futureTime,
                Beacon = priorBeacon
            };

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = 1,
                ToMilepost = currentBeaconRailroad.Milepost,
                FromMilepost = priorBeaconRailroad.Milepost
            };

            _mockTelemetryRepository
                .Setup(r => r.GetRecentsWithinTimeOffsetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Telemetry> { currentTelemetry, priorTelemetry });

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Negative time difference
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenMissingBeaconRailroad()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-60);

            var subdivision = new Subdivision { RailroadID = 1 };

            var currentBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 11.0,
                Subdivision = subdivision
            };

            var currentBeacon = new Beacon
            {
                ID = 2,
                BeaconRailroads = new List<BeaconRailroad> { currentBeaconRailroad }
            };

            var priorBeacon = new Beacon
            {
                ID = 1,
                BeaconRailroads = new List<BeaconRailroad>() // No matching railroad
            };

            var currentTelemetry = new Telemetry
            {
                BeaconID = 2,
                AddressID = 100,
                CreatedAt = currentTime,
                Beacon = currentBeacon
            };

            var priorTelemetry = new Telemetry
            {
                BeaconID = 1,
                AddressID = 100,
                CreatedAt = previousTime,
                Beacon = priorBeacon
            };

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = 1,
                ToMilepost = currentBeaconRailroad.Milepost,
                FromMilepost = 0.0 // No valid milepost for prior beacon
            };

            _mockTelemetryRepository
                .Setup(r => r.GetRecentsWithinTimeOffsetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Telemetry> { currentTelemetry, priorTelemetry });

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_UsesTelemetryBeaconMileposts_WhenContextMilepostsAreWrong()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-6);

            var cnSubdivision = new Subdivision { RailroadID = 1 };
            var wsorSubdivision = new Subdivision { RailroadID = 2 };

            var sussexCn = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 108.6,
                Subdivision = cnSubdivision
            };

            var rugbyCn = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 117.2,
                Subdivision = cnSubdivision
            };

            var rugbyWsor = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 112.16,
                Subdivision = wsorSubdivision
            };

            var currentTelemetry = new Telemetry
            {
                BeaconID = 2,
                AddressID = 6709,
                CreatedAt = currentTime,
                Beacon = new Beacon
                {
                    ID = 2,
                    BeaconRailroads = [sussexCn]
                }
            };

            var priorTelemetry = new Telemetry
            {
                BeaconID = 1,
                AddressID = 6709,
                CreatedAt = previousTime,
                Beacon = new Beacon
                {
                    ID = 1,
                    BeaconRailroads = [rugbyWsor, rugbyCn]
                }
            };

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = 1,
                // Intentionally wrong values to simulate stale/wrong map pin context.
                ToMilepost = 112.16,
                FromMilepost = 0.0
            };

            _mockTelemetryRepository
                .Setup(r => r.GetRecentsWithinTimeOffsetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
                .ReturnsAsync([currentTelemetry, priorTelemetry]);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }
    }
}
