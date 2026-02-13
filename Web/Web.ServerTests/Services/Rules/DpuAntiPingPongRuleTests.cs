using Moq;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Repositories;
using Web.Server.Services.Rules;

namespace Web.ServerTests.Services.Rules
{
    [TestClass]
    public class DpuAntiPingPongRuleTests
    {
        private Mock<ITelemetryRepository> _telemetryRepositoryMock;
        private DpuAntiPingPongRule _rule;

        [TestInitialize]
        public void Setup()
        {
            _telemetryRepositoryMock = new Mock<ITelemetryRepository>();
            _rule = new DpuAntiPingPongRule(_telemetryRepositoryMock.Object);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenSourceIsNotDpu()
        {
            // Arrange
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { Source = SourceEnum.HOT, TrainID = 66 },
                RailroadId = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNoTrainId()
        {
            // Arrange
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { Source = SourceEnum.DPU, TrainID = null },
                RailroadId = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNoRecentTelemetry()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.DPU,
                    TrainID = 66,
                    BeaconID = 2,
                    CreatedAt = currentTime
                },
                RailroadId = 1
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsForTrainWithinTimeOffsetAsync(66, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Telemetry>());

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
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
                    Source = SourceEnum.DPU,
                    TrainID = 66,
                    BeaconID = 2,
                    CreatedAt = currentTime
                },
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-3) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsForTrainWithinTimeOffsetAsync(66, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
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
                    Source = SourceEnum.DPU,
                    TrainID = 66,
                    BeaconID = 2,
                    CreatedAt = currentTime
                },
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-4) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsForTrainWithinTimeOffsetAsync(66, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
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
                    Source = SourceEnum.DPU,
                    TrainID = 66,
                    BeaconID = 2,
                    CreatedAt = currentTime
                },
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 1, TrainID = 66, CreatedAt = currentTime.AddMinutes(-4) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsForTrainWithinTimeOffsetAsync(66, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train is moving forward legitimately
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenPingPongDetected()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.DPU,
                    TrainID = 66,
                    BeaconID = 1,
                    CreatedAt = currentTime
                },
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 1, TrainID = 66, CreatedAt = currentTime.AddMinutes(0) }, // Most recent telemetry at beacon 1
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 1, TrainID = 66, CreatedAt = currentTime.AddMinutes(-4) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsForTrainWithinTimeOffsetAsync(66, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train switched to beacon 2, now trying to return to beacon 1
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenMultiplePingPongDetected()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.DPU,
                    TrainID = 66,
                    BeaconID = 3,
                    CreatedAt = currentTime
                },
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 3, TrainID = 66, CreatedAt = currentTime.AddMinutes(0) }, // Most recent telemetry at beacon 3
                new Telemetry { BeaconID = 4, TrainID = 66, CreatedAt = currentTime.AddMinutes(-2) },
                new Telemetry { BeaconID = 3, TrainID = 66, CreatedAt = currentTime.AddMinutes(-4) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsForTrainWithinTimeOffsetAsync(66, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train switched to beacon 4, now trying to return to beacon 3
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenMultipleRecentTelemetryAndNoPingPong()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.DPU,
                    TrainID = 66,
                    BeaconID = 1,
                    CreatedAt = currentTime
                },
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 1, TrainID = 66, CreatedAt = currentTime.AddMinutes(0) },
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-2) },
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-3) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsForTrainWithinTimeOffsetAsync(66, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train legitimately moved from beacon 2 to beacon 1
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenDifferentBeaconHitAfterLastBeacon()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.DPU,
                    TrainID = 66,
                    BeaconID = 1,
                    CreatedAt = currentTime
                },
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 1, TrainID = 66, CreatedAt = currentTime.AddMinutes(0) },
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 3, TrainID = 66, CreatedAt = currentTime.AddMinutes(-2) }, // Different beacon hit
                new Telemetry { BeaconID = 1, TrainID = 66, CreatedAt = currentTime.AddMinutes(-3) }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsForTrainWithinTimeOffsetAsync(66, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train visited beacon 3 between beacon 2 and beacon 1, so no ping-pong
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenTelemetryPingPongsToPreviousBeacon()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.DPU,
                    TrainID = 66,
                    BeaconID = 2,
                    CreatedAt = currentTime
                },
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(0) },
                new Telemetry { BeaconID = 1, TrainID = 66, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 1, TrainID = 66, CreatedAt = currentTime.AddMinutes(-2) },
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-3) } // Older telemetry at beacon 2
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsForTrainWithinTimeOffsetAsync(66, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train was at beacon 2, moved to beacon 1, now ping-ponging back to beacon 2
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenBeaconSequenceChanges()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.DPU,
                    TrainID = 66,
                    BeaconID = 1,
                    CreatedAt = currentTime
                },
                RailroadId = 1
            };

            var recentTelemetry = new List<Telemetry>
            {
                new Telemetry { BeaconID = 1, TrainID = 66, CreatedAt = currentTime.AddMinutes(0) },
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-1) },
                new Telemetry { BeaconID = 2, TrainID = 66, CreatedAt = currentTime.AddMinutes(-2) },
                new Telemetry { BeaconID = 3, TrainID = 66, CreatedAt = currentTime.AddMinutes(-4) } // Old beacon 3
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentsForTrainWithinTimeOffsetAsync(66, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(recentTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train sequence: 3 -> 2 -> 1, legitimate progression
            Assert.IsFalse(result.ShouldDiscard);
        }
    }
}
