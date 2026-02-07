using Moq;
using Web.Server.Entities;
using Web.Server.Repositories;
using Web.Server.Services.Rules;

namespace Web.ServerTests.Services.Rules
{
    [TestClass]
    public class TrackageRightsRuleTests
    {
        private Mock<ISubdivisionTrackageRightRepository> _trackageRightRepositoryMock;
        private TrackageRightsRule _rule;

        [TestInitialize]
        public void Setup()
        {
            _trackageRightRepositoryMock = new Mock<ISubdivisionTrackageRightRepository>();
            _rule = new TrackageRightsRule(_trackageRightRepositoryMock.Object);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNoFromSubdivision()
        {
            // Arrange
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = null },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision }
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNoToSubdivision()
        {
            // Arrange
            var fromSubdivision = new Subdivision { ID = 2, RailroadID = 2 };
            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = null }
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenSameRailroad()
        {
            // Arrange
            var fromSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var toSubdivision = new Subdivision { ID = 2, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision }
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Same railroad always allowed
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenNoTrackageRightsFound()
        {
            // Arrange
            var fromSubdivision = new Subdivision { ID = 2, RailroadID = 2 };
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision }
            };

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync((List<SubdivisionTrackageRight>?)null);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - No rights found, should discard
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenTrackageRightsExist()
        {
            // Arrange
            var fromSubdivision = new Subdivision { ID = 2, RailroadID = 2 };
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision }
            };

            var trackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 1
                }
            };

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Has rights, so not discarded
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenNoTrackageRightsToSubdivision()
        {
            // Arrange
            var fromSubdivision = new Subdivision { ID = 2, RailroadID = 2 };
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision }
            };

            var trackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 3 // Different subdivision, not the target one
                }
            };

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - No rights to target subdivision, should discard
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenMultipleTrackageRightsIncludeToSubdivision()
        {
            // Arrange
            var fromSubdivision = new Subdivision { ID = 2, RailroadID = 2 };
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision }
            };

            var trackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 3
                },
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 1 // Has rights to target subdivision
                },
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 4
                }
            };

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Has rights to target subdivision among multiple, so not discarded
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenMultipleTrackageRightsExcludeToSubdivision()
        {
            // Arrange
            var fromSubdivision = new Subdivision { ID = 2, RailroadID = 2 };
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision }
            };

            var trackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 3
                },
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 4
                },
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 5
                }
            };

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - No rights to target subdivision, should discard
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenEmptyTrackageRightsList()
        {
            // Arrange
            var fromSubdivision = new Subdivision { ID = 2, RailroadID = 2 };
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision }
            };

            var trackageRights = new List<SubdivisionTrackageRight>();

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Empty list means no rights, should discard
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenCreatedRailroadIDMatchesToSubdivisionAndNoTrackageRights()
        {
            // Arrange - Train returning to its home railroad (CreatedRailroadID matches toSubdivision's RailroadID)
            var fromSubdivision = new Subdivision { ID = 3, RailroadID = 2 };
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision },
                CreatedRailroadID = 1 // Home railroad matches toSubdivision's RailroadID
            };

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(3))
                .ReturnsAsync((List<SubdivisionTrackageRight>?)null);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train returning to home railroad, should NOT be discarded even without trackage rights
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenCreatedRailroadIDMatchesToSubdivisionAndNoRightsToTarget()
        {
            // Arrange - Train returning to its home railroad but with trackage rights that don't include target
            var fromSubdivision = new Subdivision { ID = 3, RailroadID = 2 };
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision },
                CreatedRailroadID = 1 // Home railroad matches toSubdivision's RailroadID
            };

            var trackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 3,
                    ToSubdivisionID = 2 // Not the target subdivision
                }
            };

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(3))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Train returning to home railroad, should NOT be discarded even without specific rights
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenCreatedRailroadIDDoesNotMatchAndNoRights()
        {
            // Arrange - Train NOT returning to home railroad, no trackage rights
            var fromSubdivision = new Subdivision { ID = 3, RailroadID = 2 };
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision },
                CreatedRailroadID = 3 // Home railroad does NOT match toSubdivision's RailroadID
            };

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(3))
                .ReturnsAsync((List<SubdivisionTrackageRight>?)null);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Not home railroad and no rights, should be discarded
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenCreatedRailroadIDDoesNotMatchAndNoRightsToTarget()
        {
            // Arrange - Train NOT returning to home railroad, trackage rights don't include target
            var fromSubdivision = new Subdivision { ID = 3, RailroadID = 2 };
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision },
                CreatedRailroadID = 3 // Home railroad does NOT match toSubdivision's RailroadID
            };

            var trackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 3,
                    ToSubdivisionID = 4 // Not the target subdivision
                }
            };

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(3))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Not home railroad and doesn't have rights, should be discarded
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenCreatedRailroadIDIsNullAndTrackageRightsExist()
        {
            // Arrange - CreatedRailroadID is null (legacy data), but trackage rights exist
            var fromSubdivision = new Subdivision { ID = 2, RailroadID = 2 };
            var toSubdivision = new Subdivision { ID = 1, RailroadID = 1 };

            var context = new MapPinRuleContext
            {
                FromBeaconRailroad = new BeaconRailroad { Subdivision = fromSubdivision },
                ToBeaconRailroad = new BeaconRailroad { Subdivision = toSubdivision },
                CreatedRailroadID = null // No home railroad set (legacy data)
            };

            var trackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 1 // Has rights to target
                }
            };

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Has trackage rights, should not be discarded (CreatedRailroadID null is ignored)
            Assert.IsFalse(result.ShouldDiscard);
        }
    }
}
