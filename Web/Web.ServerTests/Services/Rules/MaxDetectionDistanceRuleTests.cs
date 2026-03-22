using Web.Server.Entities;
using Web.Server.Services.Rules;

namespace Web.ServerTests.Services.Rules
{
    [TestClass]
    public class MaxDetectionDistanceRuleTests
    {
        private MaxDetectionDistanceRule _rule;

        [TestInitialize]
        public void Setup()
        {
            _rule = new MaxDetectionDistanceRule();
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenMaxDetectionDistanceNotSet()
        {
            // Arrange: To beacon has no max detection distance limit
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 100.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 130.0,
                Subdivision = new Subdivision { RailroadID = 1 },
                MaxDetectionDistanceMiles = null  // No limit
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenDistanceEqualsMax()
        {
            // Arrange: Distance equals max limit (should not discard)
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 100.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 125.0,
                Subdivision = new Subdivision { RailroadID = 1 },
                MaxDetectionDistanceMiles = 25.0  // Exactly at limit
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenDistanceExceedsMax()
        {
            // Arrange: Distance (30 mi) exceeds max limit (25 mi)
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 100.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 130.0,
                Subdivision = new Subdivision { RailroadID = 1 },
                MaxDetectionDistanceMiles = 25.0  // Exceeds by 5 mi
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsTrue(result.ShouldDiscard);
            Assert.IsTrue(result.Reason!.Contains("Max Detection Distance"));
            Assert.IsTrue(result.Reason!.Contains("30.0"));
            Assert.IsTrue(result.Reason!.Contains("25.0"));
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenDifferentRailroads()
        {
            // Arrange: Different railroads, even if distance exceeds limit
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 100.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 130.0,
                Subdivision = new Subdivision { RailroadID = 2 },  // Different railroad
                MaxDetectionDistanceMiles = 25.0  // Would be exceeded, but different railroad
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert: Different railroads are not considered "steals"
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenDistanceLessThanMax()
        {
            // Arrange: Distance (10 mi) is less than max limit (25 mi)
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 100.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 110.0,
                Subdivision = new Subdivision { RailroadID = 1 },
                MaxDetectionDistanceMiles = 25.0
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenToBeaconRailroadNull()
        {
            // Arrange: No to beacon railroad
            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Milepost = 100.0 },
                ToBeaconRailroad = null,
                CreatedRailroadID = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenReverseMilepostOrder()
        {
            // Arrange: Train moving south (mp 130 -> 100) exceeds limit
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 130.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 100.0,
                Subdivision = new Subdivision { RailroadID = 1 },
                MaxDetectionDistanceMiles = 25.0  // 30 mi distance exceeds this
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert: Abs() handles direction
            Assert.IsTrue(result.ShouldDiscard);
            Assert.IsTrue(result.Reason!.Contains("30.0"));
        }
    }
}
