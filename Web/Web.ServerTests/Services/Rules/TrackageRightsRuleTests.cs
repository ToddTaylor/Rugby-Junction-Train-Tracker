using Moq;
using Web.Server.Entities;
using Web.Server.Repositories;
using Web.Server.Services.Rules;

namespace Web.ServerTests.Services.Rules
{
    [TestClass]
    public class TrackageRightsRuleTests
    {
        private Mock<ITelemetryRepository> _telemetryRepositoryMock;
        private Mock<ISubdivisionTrackageRightRepository> _trackageRightRepositoryMock;
        private TrackageRightsRule _rule;

        [TestInitialize]
        public void Setup()
        {
            _telemetryRepositoryMock = new Mock<ITelemetryRepository>();
            _trackageRightRepositoryMock = new Mock<ISubdivisionTrackageRightRepository>();
            _rule = new TrackageRightsRule(
                _telemetryRepositoryMock.Object,
                _trackageRightRepositoryMock.Object);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNoCurrentSubdivision()
        {
            // Arrange
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>
                {
                    new BeaconRailroad { Subdivision = null }
                },
                RailroadId = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenRailroadBeaconsEmpty()
        {
            // Arrange
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>(),
                RailroadId = 1
            };

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNoPreviousTelemetry()
        {
            // Arrange
            var currentSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>
                {
                    new BeaconRailroad { Subdivision = currentSubdivision }
                },
                RailroadId = 1
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetMostRecentByAddressAsync(123))
                .ReturnsAsync((Telemetry?)null);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenPreviousTelemetryHasNoBeacon()
        {
            // Arrange
            var currentSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>
                {
                    new BeaconRailroad { Subdivision = currentSubdivision }
                },
                RailroadId = 1
            };

            var previousTelemetry = new Telemetry { AddressID = 123, Beacon = null };

            _telemetryRepositoryMock
                .Setup(s => s.GetMostRecentByAddressAsync(123))
                .ReturnsAsync(previousTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenPreviousBeaconHasNoSubdivision()
        {
            // Arrange
            var currentSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>
                {
                    new BeaconRailroad { Subdivision = currentSubdivision }
                },
                RailroadId = 1
            };

            var previousTelemetry = new Telemetry
            {
                AddressID = 123,
                Beacon = new Beacon
                {
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        new BeaconRailroad { Subdivision = null }
                    }
                }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetMostRecentByAddressAsync(123))
                .ReturnsAsync(previousTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenSameRailroad()
        {
            // Arrange
            var currentSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var previousSubdivision = new Subdivision { ID = 2, RailroadID = 1 };

            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>
                {
                    new BeaconRailroad { Subdivision = currentSubdivision }
                },
                RailroadId = 1
            };

            var previousTelemetry = new Telemetry
            {
                AddressID = 123,
                Beacon = new Beacon
                {
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        new BeaconRailroad { Subdivision = previousSubdivision }
                    }
                }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetMostRecentByAddressAsync(123))
                .ReturnsAsync(previousTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Same railroad always allowed
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNoTrackageRightsFound()
        {
            // Arrange
            var currentSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var previousSubdivision = new Subdivision { ID = 2, RailroadID = 2 };

            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>
                {
                    new BeaconRailroad { Subdivision = currentSubdivision }
                },
                RailroadId = 1
            };

            var previousTelemetry = new Telemetry
            {
                AddressID = 123,
                Beacon = new Beacon
                {
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        new BeaconRailroad { Subdivision = previousSubdivision }
                    }
                }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetMostRecentByAddressAsync(123))
                .ReturnsAsync(previousTelemetry);

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync((List<SubdivisionTrackageRight>?)null);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - No rights found, allow by default
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenTrackageRightsExist()
        {
            // Arrange
            var currentSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var previousSubdivision = new Subdivision { ID = 2, RailroadID = 2 };

            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>
                {
                    new BeaconRailroad { Subdivision = currentSubdivision }
                },
                RailroadId = 1
            };

            var previousTelemetry = new Telemetry
            {
                AddressID = 123,
                Beacon = new Beacon
                {
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        new BeaconRailroad { Subdivision = previousSubdivision }
                    }
                }
            };

            var trackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 1
                }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetMostRecentByAddressAsync(123))
                .ReturnsAsync(previousTelemetry);

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Has rights, so telemetry is allowed
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenNoTrackageRightsToCurrentSubdivision()
        {
            // Arrange
            var currentSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var previousSubdivision = new Subdivision { ID = 2, RailroadID = 2 };

            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>
                {
                    new BeaconRailroad { Subdivision = currentSubdivision }
                },
                RailroadId = 1
            };

            var previousTelemetry = new Telemetry
            {
                AddressID = 123,
                Beacon = new Beacon
                {
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        new BeaconRailroad { Subdivision = previousSubdivision }
                    }
                }
            };

            var trackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 3 // Different subdivision, not the current one
                }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetMostRecentByAddressAsync(123))
                .ReturnsAsync(previousTelemetry);

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - No rights to current subdivision, should discard
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenMultipleTrackageRightsIncludeCurrentSubdivision()
        {
            // Arrange
            var currentSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var previousSubdivision = new Subdivision { ID = 2, RailroadID = 2 };

            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>
                {
                    new BeaconRailroad { Subdivision = currentSubdivision }
                },
                RailroadId = 1
            };

            var previousTelemetry = new Telemetry
            {
                AddressID = 123,
                Beacon = new Beacon
                {
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        new BeaconRailroad { Subdivision = previousSubdivision }
                    }
                }
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
                    ToSubdivisionID = 1 // Has rights to current subdivision
                },
                new SubdivisionTrackageRight
                {
                    FromSubdivisionID = 2,
                    ToSubdivisionID = 4
                }
            };

            _telemetryRepositoryMock
                .Setup(s => s.GetMostRecentByAddressAsync(123))
                .ReturnsAsync(previousTelemetry);

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Has rights among multiple, so allowed
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenMultipleTrackageRightsExcludeCurrentSubdivision()
        {
            // Arrange
            var currentSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var previousSubdivision = new Subdivision { ID = 2, RailroadID = 2 };

            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>
                {
                    new BeaconRailroad { Subdivision = currentSubdivision }
                },
                RailroadId = 1
            };

            var previousTelemetry = new Telemetry
            {
                AddressID = 123,
                Beacon = new Beacon
                {
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        new BeaconRailroad { Subdivision = previousSubdivision }
                    }
                }
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

            _telemetryRepositoryMock
                .Setup(s => s.GetMostRecentByAddressAsync(123))
                .ReturnsAsync(previousTelemetry);

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - No rights to current subdivision among all rights, should discard
            Assert.IsTrue(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenEmptyTrackageRightsList()
        {
            // Arrange
            var currentSubdivision = new Subdivision { ID = 1, RailroadID = 1 };
            var previousSubdivision = new Subdivision { ID = 2, RailroadID = 2 };

            var context = new TelemetryRuleContext
            {
                Telemetry = new Telemetry { AddressID = 123, BeaconID = 1 },
                RailroadBeacons = new List<BeaconRailroad>
                {
                    new BeaconRailroad { Subdivision = currentSubdivision }
                },
                RailroadId = 1
            };

            var previousTelemetry = new Telemetry
            {
                AddressID = 123,
                Beacon = new Beacon
                {
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        new BeaconRailroad { Subdivision = previousSubdivision }
                    }
                }
            };

            var trackageRights = new List<SubdivisionTrackageRight>();

            _telemetryRepositoryMock
                .Setup(s => s.GetMostRecentByAddressAsync(123))
                .ReturnsAsync(previousTelemetry);

            _trackageRightRepositoryMock
                .Setup(s => s.GetByFromSubdivisionAsync(2))
                .ReturnsAsync(trackageRights);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - Empty list means no rights, but code treats this as discard (no rights found)
            Assert.IsTrue(result.ShouldDiscard);
        }
    }
}
