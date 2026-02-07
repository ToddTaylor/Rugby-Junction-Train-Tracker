using Web.Server.Entities;
using Web.Server.Services.Rules;

namespace Web.ServerTests.Services.Rules
{
    [TestClass]
    public class TrainSpeedSanityCheckRuleTests
    {
        private TrainSpeedSanityCheckRule _rule = null!;

        [TestInitialize]
        public void Setup()
        {
            _rule = new TrainSpeedSanityCheckRule();
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenTimeDifferenceIsZero()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;

            var fromBeacon = new BeaconRailroad
            {
                BeaconID = 1,
                Latitude = 43.0,
                Longitude = -88.0,
                LastUpdate = currentTime
            };

            var toBeacon = new BeaconRailroad
            {
                BeaconID = 2,
                Latitude = 43.1,
                Longitude = -88.1,
                LastUpdate = currentTime
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBeacon,
                ToBeaconRailroad = toBeacon
            };

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

            // Beacons approximately 1 mile apart
            var fromBeacon = new BeaconRailroad
            {
                BeaconID = 1,
                Latitude = 43.294944,
                Longitude = -88.253118,
                LastUpdate = previousTime
            };

            var toBeacon = new BeaconRailroad
            {
                BeaconID = 2,
                Latitude = 43.304944,
                Longitude = -88.243118,
                LastUpdate = currentTime
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBeacon,
                ToBeaconRailroad = toBeacon
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - ~1 mile in 1 hour = 1 mph, which is well below 35 mph
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenSpeedEquals35Mph()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-60); // 1 hour earlier

            // Beacons approximately 35 miles apart
            // Using rough coordinates for ~35 miles distance
            var fromBeacon = new BeaconRailroad
            {
                BeaconID = 1,
                Latitude = 43.0,
                Longitude = -88.0,
                LastUpdate = previousTime
            };

            var toBeacon = new BeaconRailroad
            {
                BeaconID = 2,
                Latitude = 43.5,
                Longitude = -88.0,
                LastUpdate = currentTime
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBeacon,
                ToBeaconRailroad = toBeacon
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - At the threshold, should not discard
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenSpeedExceeds35Mph()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-30); // 30 minutes earlier

            // Beacons approximately 35 miles apart
            var fromBeacon = new BeaconRailroad
            {
                BeaconID = 1,
                Latitude = 43.0,
                Longitude = -88.0,
                LastUpdate = previousTime
            };

            var toBeacon = new BeaconRailroad
            {
                BeaconID = 2,
                Latitude = 43.5,
                Longitude = -88.0,
                LastUpdate = currentTime
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBeacon,
                ToBeaconRailroad = toBeacon
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - ~35 miles in 30 minutes = 70 mph, which exceeds threshold
            Assert.IsTrue(result.ShouldDiscard);
            Assert.AreEqual("Train Speed Sanity Check", result.Reason);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenSpeedSignificantlyExceeds35Mph()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-10); // 10 minutes earlier

            // Beacons approximately 35 miles apart
            var fromBeacon = new BeaconRailroad
            {
                BeaconID = 1,
                Latitude = 43.0,
                Longitude = -88.0,
                LastUpdate = previousTime
            };

            var toBeacon = new BeaconRailroad
            {
                BeaconID = 2,
                Latitude = 43.5,
                Longitude = -88.0,
                LastUpdate = currentTime
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBeacon,
                ToBeaconRailroad = toBeacon
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - ~35 miles in 10 minutes = 210 mph, which is way over threshold
            Assert.IsTrue(result.ShouldDiscard);
            Assert.AreEqual("Train Speed Sanity Check", result.Reason);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenSameBeaconRepeated()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-10);

            var beacon = new BeaconRailroad
            {
                BeaconID = 1,
                Latitude = 43.0,
                Longitude = -88.0,
                LastUpdate = previousTime
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = beacon,
                ToBeaconRailroad = beacon
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Same beacon repeating, 0 distance in 10 minutes = 0 mph
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNegativeTimeDifference()
        {
            // Arrange - Edge case where current time is before previous time (shouldn't happen but test edge case)
            var currentTime = DateTime.UtcNow;
            var futureTime = currentTime.AddMinutes(10);

            var fromBeacon = new BeaconRailroad
            {
                BeaconID = 1,
                Latitude = 43.0,
                Longitude = -88.0,
                LastUpdate = futureTime // Future timestamp
            };

            var toBeacon = new BeaconRailroad
            {
                BeaconID = 2,
                Latitude = 43.5,
                Longitude = -88.0,
                LastUpdate = currentTime
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBeacon,
                ToBeaconRailroad = toBeacon
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenVeryCloseBeaconsWithinThreshold()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddSeconds(-30); // Only 30 seconds apart

            // Beacons very close (same location essentially)
            var fromBeacon = new BeaconRailroad
            {
                BeaconID = 1,
                Latitude = 43.294944,
                Longitude = -88.253118,
                LastUpdate = previousTime
            };

            var toBeacon = new BeaconRailroad
            {
                BeaconID = 2,
                Latitude = 43.294950, // Very close
                Longitude = -88.253120,
                LastUpdate = currentTime
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBeacon,
                ToBeaconRailroad = toBeacon
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Very small distance in short time
            Assert.IsFalse(result.ShouldDiscard);
        }
    }
}
