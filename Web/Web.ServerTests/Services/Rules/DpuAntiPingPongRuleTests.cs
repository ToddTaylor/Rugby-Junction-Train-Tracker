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
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNoCurrentBeacon()
        {
            // Arrange
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry
                {
                    Source = SourceEnum.DPU,
                    TrainID = 66,
                    BeaconID = 2,
                    CreatedAt = DateTime.UtcNow
                },
                RailroadId = 1
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentWithinTimeOffsetAsync(66, 2, 1, It.IsAny<DateTime>()))
                .ReturnsAsync((Telemetry?)null);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNoPreviousBeacon()
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

            var currentBeacon = new Telemetry { BeaconID = 2, CreatedAt = currentTime.AddMinutes(-3) };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentWithinTimeOffsetAsync(66, 2, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(currentBeacon);

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentForOtherBeaconWithinTimeOffsetAsync(66, 2, 1, It.IsAny<DateTime>()))
                .ReturnsAsync((Telemetry?)null);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
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
                    BeaconID = 2,
                    CreatedAt = currentTime
                },
                RailroadId = 1
            };

            var currentBeacon = new Telemetry { BeaconID = 2, CreatedAt = currentTime.AddMinutes(-4) };
            var previousBeacon = new Telemetry { BeaconID = 1, CreatedAt = currentTime.AddMinutes(-2) };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentWithinTimeOffsetAsync(66, 2, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(currentBeacon);

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentForOtherBeaconWithinTimeOffsetAsync(66, 2, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(previousBeacon);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - previousBeacon is more recent than currentBeacon
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNoPingPong()
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

            var currentBeacon = new Telemetry { BeaconID = 1, CreatedAt = currentTime.AddMinutes(-2) };
            var previousBeacon = new Telemetry { BeaconID = 2, CreatedAt = currentTime.AddMinutes(-4) };

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentWithinTimeOffsetAsync(66, 1, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(currentBeacon);

            _telemetryRepositoryMock
                .Setup(s => s.GetRecentForOtherBeaconWithinTimeOffsetAsync(66, 1, 1, It.IsAny<DateTime>()))
                .ReturnsAsync(previousBeacon);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - currentBeacon is more recent, so legitimate move forward
            Assert.IsFalse(result.ShouldDiscard);
        }
    }
}
