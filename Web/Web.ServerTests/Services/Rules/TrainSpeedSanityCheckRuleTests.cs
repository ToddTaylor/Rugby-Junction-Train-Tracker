using Moq;
using Web.Server.Entities;
using Web.Server.Repositories;
using Web.Server.Services.Rules;

namespace Web.ServerTests.Services.Rules
{
    [TestClass]
    public class TrainSpeedSanityCheckRuleTests
    {
        private const int CN_RAILROAD_ID = 1;
        private const int WSOR_RAILROAD_ID = 2;

        private const int SUPERIOR_SUBDIVISION_ID = 4;
        private const int VALLEY_SUBDIVISION_ID = 5;

        private TrainSpeedSanityCheckRule _rule = null!;
        private Mock<ITelemetryRepository> _mockTelemetryRepository = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockTelemetryRepository = new Mock<ITelemetryRepository>();
            _rule = new TrainSpeedSanityCheckRule(_mockTelemetryRepository.Object);
        }

        #region Fixtures

        private static Subdivision CnSuperior() => new()
        {
            ID = SUPERIOR_SUBDIVISION_ID,
            Name = "Superior",
            RailroadID = CN_RAILROAD_ID
        };

        private static Subdivision CnValley() => new()
        {
            ID = VALLEY_SUBDIVISION_ID,
            Name = "Valley",
            RailroadID = CN_RAILROAD_ID
        };

        /// <summary>
        /// Junction City is a true junction: the CN Superior Subdivision crosses the CN Valley
        /// Subdivision here, so the one beacon has two rows on the SAME railroad with wildly
        /// different mileposts. This is the shape that broke the rule in issue #80.
        /// </summary>
        private static BeaconRailroad JunctionCitySuperior() => new()
        {
            BeaconID = 10,
            SubdivisionID = SUPERIOR_SUBDIVISION_ID,
            Subdivision = CnSuperior(),
            Milepost = 260.0,
            Latitude = 44.589494,
            Longitude = -89.761417
        };

        private static BeaconRailroad JunctionCityValley() => new()
        {
            BeaconID = 10,
            SubdivisionID = VALLEY_SUBDIVISION_ID,
            Subdivision = CnValley(),
            Milepost = 90.4,
            Latitude = 44.589494,
            Longitude = -89.761417
        };

        private static BeaconRailroad MosineeValley() => new()
        {
            BeaconID = 11,
            SubdivisionID = VALLEY_SUBDIVISION_ID,
            Subdivision = CnValley(),
            Milepost = 78.5,
            Latitude = 44.804505,
            Longitude = -89.680821
        };

        private static Telemetry TelemetryAt(BeaconRailroad beaconRailroad, DateTime createdAt, params BeaconRailroad[] allRowsForBeacon)
        {
            var rows = allRowsForBeacon.Length > 0 ? allRowsForBeacon : [beaconRailroad];

            return new Telemetry
            {
                BeaconID = beaconRailroad.BeaconID,
                AddressID = 32700,
                CreatedAt = createdAt,
                Beacon = new Beacon
                {
                    ID = beaconRailroad.BeaconID,
                    BeaconRailroads = rows.ToList()
                }
            };
        }

        private void SetupRecents(params Telemetry[] telemetry)
        {
            _mockTelemetryRepository
                .Setup(r => r.GetRecentsWithinTimeOffsetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
                .ReturnsAsync(telemetry.ToList());
        }

        #endregion

        #region Issue #80 - junction / subdivision awareness

        /// <summary>
        /// Issue #80 repro. A CN train runs east on the Superior Subdivision, turns north onto the
        /// Valley Subdivision at Junction City, and is detected at Mosinee 30 minutes later.
        ///
        /// Junction City's Superior row is listed FIRST in the beacon's collection, so selecting by
        /// railroad alone picks MP 260 and computes |260 - 78.5| = 181.5 miles = 349 MPH. The correct
        /// comparison is Junction City's VALLEY milepost against Mosinee's Valley milepost.
        /// </summary>
        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenPriorBeaconIsJunctionOnSameSubdivision()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-30);

            var mosinee = MosineeValley();
            var junctionCitySuperior = JunctionCitySuperior();
            var junctionCityValley = JunctionCityValley();

            var currentTelemetry = TelemetryAt(mosinee, currentTime);

            // Superior deliberately first: an arbitrary FirstOrDefault would grab MP 260.
            var priorTelemetry = TelemetryAt(junctionCityValley, previousTime, junctionCitySuperior, junctionCityValley);

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = mosinee,
                FromBeaconRailroad = junctionCityValley
            };

            SetupRecents(currentTelemetry, priorTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - |90.4 - 78.5| = 11.9 miles, adjusted to 4.9, in 30 minutes = ~10 MPH.
            Assert.IsFalse(result.ShouldDiscard, result.Reason);
        }

        /// <summary>
        /// Same junction scenario, but without usable map pin context. The rule must still prefer the
        /// prior beacon's row on the SAME subdivision as the current reading.
        /// </summary>
        [TestMethod]
        public async Task ShouldDiscardAsync_PrefersSameSubdivisionRow_WhenContextFromBeaconRailroadIsMissing()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-30);

            var mosinee = MosineeValley();
            var currentTelemetry = TelemetryAt(mosinee, currentTime);
            var priorTelemetry = TelemetryAt(JunctionCityValley(), previousTime, JunctionCitySuperior(), JunctionCityValley());

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = mosinee,
                FromBeaconRailroad = null
            };

            SetupRecents(currentTelemetry, priorTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard, result.Reason);
        }

        /// <summary>
        /// Skipping the check across subdivisions would create a blind spot at junctions. A reading
        /// hundreds of straight-line miles away in minutes must still be discarded.
        /// </summary>
        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenCrossSubdivisionStraightlineSpeedIsImpossible()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-10);

            // Far-away beacon on a different subdivision (roughly 200 miles from Junction City).
            var farAway = new BeaconRailroad
            {
                BeaconID = 12,
                SubdivisionID = VALLEY_SUBDIVISION_ID,
                Subdivision = CnValley(),
                Milepost = 50.0,
                Latitude = 41.878113,
                Longitude = -87.629799
            };

            var junctionCitySuperior = JunctionCitySuperior();

            var currentTelemetry = TelemetryAt(farAway, currentTime);
            var priorTelemetry = TelemetryAt(junctionCitySuperior, previousTime);

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = farAway,
                FromBeaconRailroad = junctionCitySuperior
            };

            SetupRecents(currentTelemetry, priorTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsTrue(result.ShouldDiscard);
            Assert.Contains(TrainSpeedSanityCheckRule.DISCARD_REASON, result.Reason!);
            Assert.Contains("straightline", result.Reason!);
        }

        /// <summary>
        /// Two beacons on different subdivisions that genuinely sit near each other at a junction are
        /// within the radio-range adjustment, so the adjusted distance clamps to zero and the reading
        /// is kept. A train really can be near both.
        /// </summary>
        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenCrossSubdivisionBeaconsAreAdjacent()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-5);

            var nearby = new BeaconRailroad
            {
                BeaconID = 13,
                SubdivisionID = VALLEY_SUBDIVISION_ID,
                Subdivision = CnValley(),
                Milepost = 88.0,
                Latitude = 44.617,
                Longitude = -89.761
            };

            var junctionCitySuperior = JunctionCitySuperior();

            var currentTelemetry = TelemetryAt(nearby, currentTime);
            var priorTelemetry = TelemetryAt(junctionCitySuperior, previousTime);

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = nearby,
                FromBeaconRailroad = junctionCitySuperior
            };

            SetupRecents(currentTelemetry, priorTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - under 2 miles apart, so the 7 mile adjustment clamps to 0.
            Assert.IsFalse(result.ShouldDiscard, result.Reason);
        }

        /// <summary>
        /// The milepost path must still catch impossible speeds within a single subdivision.
        /// </summary>
        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenSameSubdivisionSpeedIsImpossible()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-20);

            var mosinee = MosineeValley();

            var farUpValley = new BeaconRailroad
            {
                BeaconID = 14,
                SubdivisionID = VALLEY_SUBDIVISION_ID,
                Subdivision = CnValley(),
                Milepost = 178.5,
                Latitude = 45.5,
                Longitude = -89.6
            };

            var currentTelemetry = TelemetryAt(farUpValley, currentTime);
            var priorTelemetry = TelemetryAt(mosinee, previousTime);

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = farUpValley,
                FromBeaconRailroad = mosinee
            };

            SetupRecents(currentTelemetry, priorTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - 100 miles, adjusted to 93, in 20 minutes = 279 MPH.
            Assert.IsTrue(result.ShouldDiscard);
            Assert.Contains("milepost", result.Reason!);
        }

        /// <summary>
        /// Never discard on unknown position: a cross-subdivision pair with no coordinates has no
        /// comparable basis at all.
        /// </summary>
        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenCrossSubdivisionCoordinatesAreMissing()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-10);

            var noCoordinates = new BeaconRailroad
            {
                BeaconID = 15,
                SubdivisionID = VALLEY_SUBDIVISION_ID,
                Subdivision = CnValley(),
                Milepost = 500.0,
                Latitude = 0,
                Longitude = 0
            };

            var junctionCitySuperior = JunctionCitySuperior();

            var currentTelemetry = TelemetryAt(noCoordinates, currentTime);
            var priorTelemetry = TelemetryAt(junctionCitySuperior, previousTime);

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = noCoordinates,
                FromBeaconRailroad = junctionCitySuperior
            };

            SetupRecents(currentTelemetry, priorTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard, result.Reason);
        }

        #endregion

        #region Existing behavior

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenLessThanTwoTelemetryEntries()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;

            var beaconRailroad = new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = SUPERIOR_SUBDIVISION_ID,
                Subdivision = CnSuperior(),
                Milepost = 10.0,
                Latitude = 44.1,
                Longitude = -89.1
            };

            var telemetry = TelemetryAt(beaconRailroad, currentTime);

            var context = new TelemetryRuleContext
            {
                Telemetry = telemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = beaconRailroad,
                FromBeaconRailroad = null // No prior beacon
            };

            // Only one telemetry entry
            SetupRecents(telemetry);

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

            var currentBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 2,
                SubdivisionID = SUPERIOR_SUBDIVISION_ID,
                Subdivision = CnSuperior(),
                Milepost = 11.0,
                Latitude = 44.2,
                Longitude = -89.2
            };

            var priorBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = SUPERIOR_SUBDIVISION_ID,
                Subdivision = CnSuperior(),
                Milepost = 10.0,
                Latitude = 44.1,
                Longitude = -89.1
            };

            var currentTelemetry = TelemetryAt(currentBeaconRailroad, currentTime);
            var priorTelemetry = TelemetryAt(priorBeaconRailroad, previousTime);

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = currentBeaconRailroad,
                FromBeaconRailroad = priorBeaconRailroad
            };

            SetupRecents(currentTelemetry, priorTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - 1 mile in 60 minutes, adjusted 1 - 6 - 1 = negative, clamped to 0 = 0 mph
            Assert.IsFalse(result.ShouldDiscard);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsTrue_WhenSpeedExceedsThreshold()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-20);

            var currentBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 2,
                SubdivisionID = SUPERIOR_SUBDIVISION_ID,
                Subdivision = CnSuperior(),
                Milepost = 70.0,
                Latitude = 44.9,
                Longitude = -89.9
            };

            var priorBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = SUPERIOR_SUBDIVISION_ID,
                Subdivision = CnSuperior(),
                Milepost = 10.0,
                Latitude = 44.1,
                Longitude = -89.1
            };

            var currentTelemetry = TelemetryAt(currentBeaconRailroad, currentTime);
            var priorTelemetry = TelemetryAt(priorBeaconRailroad, previousTime);

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = currentBeaconRailroad,
                FromBeaconRailroad = priorBeaconRailroad
            };

            SetupRecents(currentTelemetry, priorTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - 60 miles, adjusted to 53, in 20 minutes = 159 MPH
            Assert.IsTrue(result.ShouldDiscard);
            Assert.Contains(TrainSpeedSanityCheckRule.DISCARD_REASON, result.Reason!);
        }

        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenNegativeTimeDifference()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var futureTime = currentTime.AddMinutes(10);

            var currentBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 2,
                SubdivisionID = SUPERIOR_SUBDIVISION_ID,
                Subdivision = CnSuperior(),
                Milepost = 70.0,
                Latitude = 44.9,
                Longitude = -89.9
            };

            var priorBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = SUPERIOR_SUBDIVISION_ID,
                Subdivision = CnSuperior(),
                Milepost = 10.0,
                Latitude = 44.1,
                Longitude = -89.1
            };

            var currentTelemetry = TelemetryAt(currentBeaconRailroad, currentTime);

            // Prior telemetry is timestamped AFTER the current one.
            var priorTelemetry = TelemetryAt(priorBeaconRailroad, futureTime);

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = currentBeaconRailroad,
                FromBeaconRailroad = priorBeaconRailroad
            };

            SetupRecents(currentTelemetry, priorTelemetry);

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

            var currentBeaconRailroad = new BeaconRailroad
            {
                BeaconID = 2,
                SubdivisionID = SUPERIOR_SUBDIVISION_ID,
                Subdivision = CnSuperior(),
                Milepost = 11.0,
                Latitude = 44.2,
                Longitude = -89.2
            };

            var currentTelemetry = TelemetryAt(currentBeaconRailroad, currentTime);

            // Prior beacon has no beacon railroads at all.
            var priorTelemetry = new Telemetry
            {
                BeaconID = 1,
                AddressID = 32700,
                CreatedAt = previousTime,
                Beacon = new Beacon
                {
                    ID = 1,
                    BeaconRailroads = []
                }
            };

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = currentBeaconRailroad,
                FromBeaconRailroad = null
            };

            SetupRecents(currentTelemetry, priorTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert
            Assert.IsFalse(result.ShouldDiscard);
        }

        /// <summary>
        /// Rugby Junction is NOT a junction: it is one location where two DIFFERENT railroads run
        /// parallel. Selecting by railroad correctly disambiguates here, and must keep doing so.
        /// </summary>
        [TestMethod]
        public async Task ShouldDiscardAsync_ReturnsFalse_WhenPriorBeaconHasMultipleRailroads()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var previousTime = currentTime.AddMinutes(-6);

            var cnWaukesha = new Subdivision { ID = 1, Name = "Waukesha", RailroadID = CN_RAILROAD_ID };
            var wsorWatertown = new Subdivision { ID = 2, Name = "Watertown", RailroadID = WSOR_RAILROAD_ID };

            var sussexCn = new BeaconRailroad
            {
                BeaconID = 2,
                SubdivisionID = cnWaukesha.ID,
                Subdivision = cnWaukesha,
                Milepost = 108.6,
                Latitude = 43.159517,
                Longitude = -88.200492
            };

            var rugbyCn = new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = cnWaukesha.ID,
                Subdivision = cnWaukesha,
                Milepost = 117.2,
                Latitude = 43.280958,
                Longitude = -88.214682
            };

            var rugbyWsor = new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = wsorWatertown.ID,
                Subdivision = wsorWatertown,
                Milepost = 112.16,
                Latitude = 43.280958,
                Longitude = -88.213966
            };

            var currentTelemetry = TelemetryAt(sussexCn, currentTime);

            // WSOR row listed first; the CN row is the correct one for a CN train.
            var priorTelemetry = TelemetryAt(rugbyCn, previousTime, rugbyWsor, rugbyCn);

            var context = new TelemetryRuleContext
            {
                Telemetry = currentTelemetry,
                RailroadId = CN_RAILROAD_ID,
                ToBeaconRailroad = sussexCn,
                FromBeaconRailroad = null
            };

            SetupRecents(currentTelemetry, priorTelemetry);

            // Act
            var result = await _rule.ShouldDiscardAsync(context);

            // Assert - |117.2 - 108.6| = 8.6 miles, adjusted to 1.6, in 6 minutes = 16 MPH.
            Assert.IsFalse(result.ShouldDiscard, result.Reason);
        }

        #endregion
    }
}
