using Moq;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Repositories;
using Web.Server.Services.Rules;

namespace Web.ServerTests.Services.Rules
{
    [TestClass]
    public class EotHotAntiPingPongRuleTests
    {
        private Mock<ITelemetryRepository> _telemetryRepositoryMock;
        private EotHotAntiPingPongRule _rule;

        [TestInitialize]
        public void Setup()
        {
            _telemetryRepositoryMock = new Mock<ITelemetryRepository>();
            _rule = new EotHotAntiPingPongRule(_telemetryRepositoryMock.Object);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenSourceIsNotEotOrHot()
        {
            // Arrange
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { Source = SourceEnum.DPU, AddressID = 123 },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenSourceIsEotButNoRecentTelemetry()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.EOT,
                    AddressID = 123,
                    BeaconID = 2,
                    CreatedAt = currentTime
                },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsWithinTimeOffsetAsync(123, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Telemetry>());

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenSourceIsHotButNoRecentTelemetry()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.HOT,
                    AddressID = 456,
                    BeaconID = 2,
                    CreatedAt = currentTime
                },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsWithinTimeOffsetAsync(456, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Telemetry>());

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenOnlyOneRecentTelemetry()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.EOT,
                    AddressID = 123,
                    BeaconID = 2,
                    CreatedAt = currentTime
                },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 2, CreatedAt = currentTime.AddMinutes(-3) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsWithinTimeOffsetAsync(123, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenTrainNotSwitchingBeacons()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.EOT,
                    AddressID = 123,
                    BeaconID = 2,
                    CreatedAt = currentTime
                },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 2, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 2, CreatedAt = currentTime.AddMinutes(-4) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsWithinTimeOffsetAsync(123, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenMostRecentBeaconIsNewBeacon()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.EOT,
                    AddressID = 123,
                    BeaconID = 2,
                    CreatedAt = currentTime
                },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 2, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 1, CreatedAt = currentTime.AddMinutes(-4) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsWithinTimeOffsetAsync(123, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train is moving forward legitimately
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenPingPongDetectedForEot()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.EOT,
                    AddressID = 123,
                    BeaconID = 1,
                    CreatedAt = currentTime
                },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 2, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 1, CreatedAt = currentTime.AddMinutes(-4) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsWithinTimeOffsetAsync(123, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train switched to beacon 2, now trying to return to beacon 1
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenPingPongDetectedForHot()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.HOT,
                    AddressID = 456,
                    BeaconID = 3,
                    CreatedAt = currentTime
                },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 4, CreatedAt = currentTime.AddMinutes(-2) },
                new Telemetry { BeaconID = 3, CreatedAt = currentTime.AddMinutes(-4) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsWithinTimeOffsetAsync(456, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train switched to beacon 4, now trying to return to beacon 3
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenMultipleRecentTelemetryAndPingPong()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.EOT,
                    AddressID = 789,
                    BeaconID = 1,
                    CreatedAt = currentTime
                },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 2, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 1, CreatedAt = currentTime.AddMinutes(-2) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsWithinTimeOffsetAsync(789, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train is at beacon 2 most recently, trying to return to beacon 1
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenMultipleRecentTelemetryAndNoPingPong()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry =
                new Telemetry { BeaconID = 1, AddressID = 32981, Source = SourceEnum.HOT, CreatedAt = currentTime },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 2, AddressID = 32981, Source = SourceEnum.EOT, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 2, AddressID = 32981, Source = SourceEnum.EOT, CreatedAt = currentTime.AddMinutes(-2) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsWithinTimeOffsetAsync(32981, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenOneRecentAndOneOldTelemetryAndNoPingPong()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry =
                new Telemetry { BeaconID = 1, AddressID = 32981, Source = SourceEnum.HOT, CreatedAt = currentTime },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 2, AddressID = 32981, Source = SourceEnum.EOT, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 3, AddressID = 32981, Source = SourceEnum.EOT, CreatedAt = currentTime.AddMinutes(-360) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsWithinTimeOffsetAsync(32981, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result);
        }
    }
}