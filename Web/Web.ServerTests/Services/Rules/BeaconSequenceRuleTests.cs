using Moq;
using Web.Server.Entities;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services.Rules;

namespace Web.ServerTests.Services.Rules
{
    [TestClass]
    public class BeaconSequenceRuleTests
    {
        private Mock<IBeaconRailroadRepository> _beaconRailroadRepositoryMock;
        private Mock<ITimeProvider> _timeProviderMock;
        private BeaconSequenceRule _rule;
        private DateTime _currentUtc;

        [TestInitialize]
        public void Setup()
        {
            _beaconRailroadRepositoryMock = new Mock<IBeaconRailroadRepository>();
            _timeProviderMock = new Mock<ITimeProvider>();
            
            // Default: current time is March 22, 2026, 12:00 UTC
            _currentUtc = new DateTime(2026, 3, 22, 12, 0, 0, DateTimeKind.Utc);
            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(_currentUtc);

            _rule = new BeaconSequenceRule(_beaconRailroadRepositoryMock.Object, _timeProviderMock.Object);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenGapBelowThreshold()
        {
            // Arrange: 10-mile gap between beacons, below 15-mile threshold
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
                Subdivision = new Subdivision { RailroadID = 1 }
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
            _beaconRailroadRepositoryMock.Verify(
                r => r.GetByRailroadBetweenMilepostsAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTime>()),
                Times.Never);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenGapAboveThresholdButNoIntermediates()
        {
            // Arrange: 20-mile gap, but no intermediate beacons exist
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 100.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 120.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            _beaconRailroadRepositoryMock
                .Setup(r => r.GetByRailroadBetweenMilepostsAsync(1, 100.0, 120.0, It.IsAny<DateTime>()))
                .ReturnsAsync(new List<BeaconRailroad>());

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
            _beaconRailroadRepositoryMock.Verify(
                r => r.GetByRailroadBetweenMilepostsAsync(1, 100.0, 120.0, It.IsAny<DateTime>()),
                Times.Once);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenGapAboveThresholdAndIntermediateExists()
        {
            // Arrange: 20-mile gap with one intermediate beacon
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 100.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 120.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            var intermediate = new BeaconRailroad
            {
                BeaconID = 3,
                Milepost = 110.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };

            _beaconRailroadRepositoryMock
                .Setup(r => r.GetByRailroadBetweenMilepostsAsync(1, 100.0, 120.0, It.IsAny<DateTime?>()))
                .ReturnsAsync(new List<BeaconRailroad> { intermediate });

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsTrue(result.ShouldDiscard);
            Assert.IsTrue(result.Reason!.Contains("Beacon Sequence Skip"));
            Assert.IsTrue(result.Reason!.Contains("20"));
            Assert.IsTrue(result.Reason!.Contains("1 skipped beacon(s)"));
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenMultipleIntermediatesExist()
        {
            // Arrange: FDL (157.26) -> NEENAH (184.8), with NFDL (160.53) and OSHKOSH (172.8) in between
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 157.26,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 184.8,
                Subdivision = new Subdivision { RailroadID = 1 }
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            var intermediates = new List<BeaconRailroad>
            {
                new BeaconRailroad { BeaconID = 3, Milepost = 160.53, Subdivision = new Subdivision { RailroadID = 1 } },
                new BeaconRailroad { BeaconID = 4, Milepost = 172.8, Subdivision = new Subdivision { RailroadID = 1 } }
            };

            _beaconRailroadRepositoryMock
                .Setup(r => r.GetByRailroadBetweenMilepostsAsync(1, 157.26, 184.8, It.IsAny<DateTime?>()))
                .ReturnsAsync(intermediates);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsTrue(result.ShouldDiscard);
            Assert.IsTrue(result.Reason!.Contains("Beacon Sequence Skip"));
            Assert.IsTrue(result.Reason!.Contains("28"));
            Assert.IsTrue(result.Reason!.Contains("2 skipped beacon(s)"));
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenCrossingSubdivisionsOnSameRailroad()
        {
            // Arrange: Gateway from FDL (157.26) to Neenah (184.8), same railroad
            // Intermediate beacons exist on Neenah side (160.53, 172.8)
            var waushineSubdivision = new Subdivision { ID = 1, RailroadID = 1, Name = "Waukesha" };
            var neenheSubdivision = new Subdivision { ID = 2, RailroadID = 1, Name = "Neenah" };

            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 157.26,
                Subdivision = waushineSubdivision
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 184.8,
                Subdivision = neenheSubdivision
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            var intermediates = new List<BeaconRailroad>
            {
                new BeaconRailroad { BeaconID = 3, Milepost = 160.53, Subdivision = neenheSubdivision },
                new BeaconRailroad { BeaconID = 4, Milepost = 172.8, Subdivision = neenheSubdivision }
            };

            _beaconRailroadRepositoryMock
                .Setup(r => r.GetByRailroadBetweenMilepostsAsync(1, 157.26, 184.8, It.IsAny<DateTime>()))
                .ReturnsAsync(intermediates);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert: Cross-subdivision on same railroad should still trigger the rule
            Assert.IsTrue(result.ShouldDiscard);
            Assert.IsTrue(result.Reason!.Contains("Beacon Sequence Skip"));
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenDifferentRailroads()
        {
            // Arrange: Beacon on Railroad 1 jumping to Beacon on Railroad 2 (different railroads)
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 100.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 120.0,
                Subdivision = new Subdivision { RailroadID = 2 }
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert: Different railroads should not trigger the rule
            Assert.IsFalse(result.ShouldDiscard);
            _beaconRailroadRepositoryMock.Verify(
                r => r.GetByRailroadBetweenMilepostsAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTime>()),
                Times.Never);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenReverseMilepostOrder()
        {
            // Arrange: Train moving south from mp 120 to mp 100, above threshold but no intermediates
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 120.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 100.0,
                Subdivision = new Subdivision { RailroadID = 1 }
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            _beaconRailroadRepositoryMock
                .Setup(r => r.GetByRailroadBetweenMilepostsAsync(1, 100.0, 120.0, It.IsAny<DateTime>()))
                .ReturnsAsync(new List<BeaconRailroad>());

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert: Should call with correct min/max regardless of direction
            Assert.IsFalse(result.ShouldDiscard);
            _beaconRailroadRepositoryMock.Verify(
                r => r.GetByRailroadBetweenMilepostsAsync(1, 100.0, 120.0, It.IsAny<DateTime>()),
                Times.Once);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenReverseMilepostWithIntermediates()
        {
            // Arrange: Train moving south from 184.8 to 157.26, with intermediates between
            var fromBr = new BeaconRailroad
            {
                BeaconID = 1,
                Milepost = 184.8,
                Subdivision = new Subdivision { RailroadID = 1 }
            };
            var toBr = new BeaconRailroad
            {
                BeaconID = 2,
                Milepost = 157.26,
                Subdivision = new Subdivision { RailroadID = 1 }
            };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = fromBr,
                ToBeaconRailroad = toBr,
                CreatedRailroadID = 1
            };

            var intermediates = new List<BeaconRailroad>
            {
                new BeaconRailroad { BeaconID = 3, Milepost = 160.53, Subdivision = new Subdivision { RailroadID = 1 } },
                new BeaconRailroad { BeaconID = 4, Milepost = 172.8, Subdivision = new Subdivision { RailroadID = 1 } }
            };

            _beaconRailroadRepositoryMock
                .Setup(r => r.GetByRailroadBetweenMilepostsAsync(1, 157.26, 184.8, It.IsAny<DateTime>()))
                .ReturnsAsync(intermediates);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert: Should discard regardless of direction
            Assert.IsTrue(result.ShouldDiscard);
            Assert.IsTrue(result.Reason!.Contains("28"));
        }
    }
}
