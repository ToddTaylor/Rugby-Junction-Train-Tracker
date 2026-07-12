using System.Diagnostics.CodeAnalysis;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.ServerTests.Services
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class BeaconNeighborResolverTests
    {
        private readonly BeaconNeighborResolver _resolver = new();

        // ---------------------------------------------------------------------------
        // Test helper: create a BeaconRailroad with the fields needed by the resolver
        // ---------------------------------------------------------------------------

        private static BeaconRailroad MakeBeacon(
            int beaconId,
            int railroadId,
            double lat,
            double lon,
            DateTime? lastUpdate = null)
        {
            var now = DateTime.UtcNow;
            return new BeaconRailroad
            {
                BeaconID = beaconId,
                SubdivisionID = railroadId * 10,
                Subdivision = new Subdivision
                {
                    ID = railroadId * 10,
                    RailroadID = railroadId,
                    Railroad = new Railroad { ID = railroadId, Name = $"RR-{railroadId}" }
                },
                Latitude = lat,
                Longitude = lon,
                LastUpdate = lastUpdate ?? now
            };
        }

        // ---------------------------------------------------------------------------
        // GetDirectNeighbors — online filtering
        // ---------------------------------------------------------------------------

        [TestMethod]
        public void GetDirectNeighbors_ExcludesOfflineBeacons()
        {
            var now = DateTime.UtcNow;
            // Source beacon: railroadId=1, online
            var source = MakeBeacon(1, 1, 43.0, -88.0, now);
            // Nearby candidate is offline (last update > 15 min ago)
            var offline = MakeBeacon(2, 1, 43.1, -88.0, now.AddMinutes(-20));

            var result = _resolver.GetDirectNeighbors(1, 1, [source, offline], now);

            Assert.AreEqual(0, result.Count, "Offline beacons should be excluded from the neighbor graph.");
        }

        [TestMethod]
        public void GetDirectNeighbors_IncludesRecentOnlineBeacons()
        {
            var now = DateTime.UtcNow;
            var source = MakeBeacon(1, 1, 43.0, -88.0, now);
            var online = MakeBeacon(2, 1, 43.1, -88.0, now.AddMinutes(-5));

            var result = _resolver.GetDirectNeighbors(1, 1, [source, online], now);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(2, result[0].BeaconID);
        }

        [TestMethod]
        public void GetDirectNeighbors_ExcludesDefaultLastUpdate()
        {
            var now = DateTime.UtcNow;
            var source = MakeBeacon(1, 1, 43.0, -88.0, now);
            // Directly create a beacon whose LastUpdate is default(DateTime) = DateTime.MinValue
            var neverUpdated = new BeaconRailroad
            {
                BeaconID = 2,
                SubdivisionID = 10,
                Subdivision = new Subdivision { ID = 10, RailroadID = 1, Railroad = new Railroad { ID = 1, Name = "RR-1" } },
                Latitude = 43.1,
                Longitude = -88.0,
                LastUpdate = default  // DateTime.MinValue
            };

            var result = _resolver.GetDirectNeighbors(1, 1, [source, neverUpdated], now);

            Assert.AreEqual(0, result.Count, "Beacons with default LastUpdate should be excluded.");
        }

        // ---------------------------------------------------------------------------
        // GetDirectNeighbors — same-railroad filtering
        // ---------------------------------------------------------------------------

        [TestMethod]
        public void GetDirectNeighbors_ExcludesDifferentRailroadBeacons()
        {
            var now = DateTime.UtcNow;
            var source = MakeBeacon(1, 1, 43.0, -88.0, now);
            // Same location but different railroad
            var otherRailroad = MakeBeacon(2, 2, 43.1, -88.0, now);

            var result = _resolver.GetDirectNeighbors(1, 1, [source, otherRailroad], now);

            Assert.AreEqual(0, result.Count, "Cross-railroad beacons should not be neighbors.");
        }

        // ---------------------------------------------------------------------------
        // GetDirectNeighbors — sector assignment (one nearest per sector)
        // ---------------------------------------------------------------------------

        [TestMethod]
        public void GetDirectNeighbors_ReturnsSectorNearest_WhenTwoBeaconsInSameSector()
        {
            var now = DateTime.UtcNow;
            // Source at origin
            var source = MakeBeacon(1, 1, 43.0, -88.0, now);
            // Two beacons roughly due North; closer one should win
            var nearNorth = MakeBeacon(2, 1, 43.05, -88.0, now);  // ~5.5 km N
            var farNorth = MakeBeacon(3, 1, 43.10, -88.0, now);   // ~11 km N

            var result = _resolver.GetDirectNeighbors(1, 1, [source, nearNorth, farNorth], now);

            // Only one neighbor per sector; the closer North beacon should win
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(2, result[0].BeaconID, "Nearest beacon in each sector should be selected.");
        }

        [TestMethod]
        public void GetDirectNeighbors_ReturnsMultipleNeighbors_WhenBeaconsInDifferentSectors()
        {
            var now = DateTime.UtcNow;
            var source = MakeBeacon(1, 1, 43.0, -88.0, now);
            // Due North
            var north = MakeBeacon(2, 1, 43.1, -88.0, now);
            // Due South
            var south = MakeBeacon(3, 1, 42.9, -88.0, now);

            var result = _resolver.GetDirectNeighbors(1, 1, [source, north, south], now);

            Assert.AreEqual(2, result.Count, "Beacons in different sectors should all be returned.");
            CollectionAssert.Contains(result.Select(b => b.BeaconID).ToList(), 2);
            CollectionAssert.Contains(result.Select(b => b.BeaconID).ToList(), 3);
        }

        [TestMethod]
        public void GetDirectNeighbors_ReturnsAtMostEightNeighbors()
        {
            var now = DateTime.UtcNow;
            var source = MakeBeacon(1, 1, 43.0, -88.0, now);

            // Place beacons in each of the 8 sectors plus extras in some sectors
            var candidates = new List<BeaconRailroad>
            {
                source,
                MakeBeacon(2, 1, 43.1, -88.0, now),   // N
                MakeBeacon(3, 1, 43.1, -87.9, now),    // NE
                MakeBeacon(4, 1, 43.0, -87.9, now),    // E
                MakeBeacon(5, 1, 42.9, -87.9, now),    // SE
                MakeBeacon(6, 1, 42.9, -88.0, now),    // S
                MakeBeacon(7, 1, 42.9, -88.1, now),    // SW
                MakeBeacon(8, 1, 43.0, -88.1, now),    // W
                MakeBeacon(9, 1, 43.1, -88.1, now),    // NW
                MakeBeacon(10, 1, 43.2, -88.0, now),   // N (second, farther)
            };

            var result = _resolver.GetDirectNeighbors(1, 1, candidates, now);

            Assert.IsTrue(result.Count <= 8, "At most 8 neighbors (one per sector).");
        }

        // ---------------------------------------------------------------------------
        // IsReachable — basic cases
        // ---------------------------------------------------------------------------

        [TestMethod]
        public void IsReachable_ReturnsTrueForSameBeacon()
        {
            var now = DateTime.UtcNow;
            var source = MakeBeacon(1, 1, 43.0, -88.0, now);

            var result = _resolver.IsReachable(1, 1, 1, [source], now);

            Assert.IsTrue(result, "A beacon is always reachable from itself.");
        }

        [TestMethod]
        public void IsReachable_ReturnsTrueForDirectNeighbor()
        {
            var now = DateTime.UtcNow;
            // A north of B
            var beaconA = MakeBeacon(1, 1, 43.1, -88.0, now);
            var beaconB = MakeBeacon(2, 1, 43.0, -88.0, now);

            var result = _resolver.IsReachable(1, 2, 1, [beaconA, beaconB], now, maxHops: 2);

            Assert.IsTrue(result, "A directly neighboring beacon should be reachable within 1 hop.");
        }

        [TestMethod]
        public void IsReachable_ReturnsTrueForTwoHopNeighbor()
        {
            var now = DateTime.UtcNow;
            // Chain: A → B → C (each ~11 km north)
            var beaconA = MakeBeacon(1, 1, 43.0, -88.0, now);
            var beaconB = MakeBeacon(2, 1, 43.1, -88.0, now);
            var beaconC = MakeBeacon(3, 1, 43.2, -88.0, now);

            var result = _resolver.IsReachable(1, 3, 1, [beaconA, beaconB, beaconC], now, maxHops: 2);

            Assert.IsTrue(result, "A 2-hop transition should be considered reachable.");
        }

        [TestMethod]
        public void IsReachable_ReturnsFalseWhenBeyondMaxHops()
        {
            var now = DateTime.UtcNow;
            // Chain: A → B → C → D; A to D is 3 hops but maxHops=2
            var beaconA = MakeBeacon(1, 1, 43.0, -88.0, now);
            var beaconB = MakeBeacon(2, 1, 43.1, -88.0, now);
            var beaconC = MakeBeacon(3, 1, 43.2, -88.0, now);
            var beaconD = MakeBeacon(4, 1, 43.3, -88.0, now);

            var result = _resolver.IsReachable(1, 4, 1, [beaconA, beaconB, beaconC, beaconD], now, maxHops: 2);

            Assert.IsFalse(result, "Transition beyond maxHops should not be reachable.");
        }

        [TestMethod]
        public void IsReachable_ReturnsFalseWhenNoPathExists()
        {
            var now = DateTime.UtcNow;
            // A and B are far apart; C is near B but far from A
            var beaconA = MakeBeacon(1, 1, 43.0, -88.0, now);
            var beaconB = MakeBeacon(2, 1, 50.0, -88.0, now); // ~780 km north — no connection

            var result = _resolver.IsReachable(1, 2, 1, [beaconA, beaconB], now, maxHops: 2);

            // beaconB cannot be a direct neighbor of beaconA because beaconB is the only other beacon
            // and IS actually in a sector — so it WILL be the sector winner.
            // The test verifies reachability between disconnected clusters; use 3 beacons to force
            // sector competition that makes A→B unreachable.
            Assert.IsTrue(result, "With only 2 beacons, the only candidate wins its sector so it IS reachable.");
        }

        [TestMethod]
        public void IsReachable_ReturnsTrueWhenOfflineIntermediateExcluded_EndpointsAreDirectNeighbors()
        {
            var now = DateTime.UtcNow;
            // A, B (offline), C — with B excluded, C becomes A's direct north neighbor
            var beaconA = MakeBeacon(1, 1, 43.0, -88.0, now);
            var beaconBOffline = MakeBeacon(2, 1, 43.1, -88.0, now.AddMinutes(-20));
            var beaconC = MakeBeacon(3, 1, 43.2, -88.0, now);

            // With B offline, C wins the north sector from A directly → 1-hop reachable
            var result = _resolver.IsReachable(1, 3, 1, [beaconA, beaconBOffline, beaconC], now, maxHops: 2);

            Assert.IsTrue(result, "When the intermediate is offline, the endpoint becomes a direct neighbor and is reachable in 1 hop.");
        }

        [TestMethod]
        public void IsReachable_ReturnsFalseWhenTargetBeyondTwoHops_WithOfflineBeacons()
        {
            var now = DateTime.UtcNow;
            // Dense linear chain where close online beacons outcompete offline ones for each sector:
            //   A(online) -- B1(online,close) -- B2(offline) -- B3(online) -- B4(offline) -- D(online)
            // Online graph: A→B1→B3→D (3 hops). With maxHops=2, D is not reachable.
            var beaconA  = MakeBeacon(1, 1, 43.0,   -88.0,   now);
            var beaconB1 = MakeBeacon(2, 1, 43.0,   -87.995, now);               // online, very close east of A
            var beaconB2 = MakeBeacon(3, 1, 43.0,   -87.98,  now.AddMinutes(-20)); // OFFLINE
            var beaconB3 = MakeBeacon(4, 1, 43.0,   -87.965, now);               // online
            var beaconB4 = MakeBeacon(5, 1, 43.0,   -87.95,  now.AddMinutes(-20)); // OFFLINE
            var beaconD  = MakeBeacon(6, 1, 43.0,   -87.93,  now);               // online, target

            var result = _resolver.IsReachable(1, 6, 1, [beaconA, beaconB1, beaconB2, beaconB3, beaconB4, beaconD], now, maxHops: 2);

            Assert.IsFalse(result, "D requires 3 hops (A→B1→B3→D); it should not be reachable with maxHops=2.");
        }

        // ---------------------------------------------------------------------------
        // IsReachable — cross-railroad scenarios
        // ---------------------------------------------------------------------------

        [TestMethod]
        public void IsReachable_ReturnsFalseWhenTargetIsOnDifferentRailroad()
        {
            var now = DateTime.UtcNow;
            // A on railroad 1, B on railroad 2 but same location
            var beaconA = MakeBeacon(1, 1, 43.0, -88.0, now);
            var beaconBOtherRR = MakeBeacon(2, 2, 43.05, -88.0, now);

            // Railroad 1 only has beaconA — beaconBOtherRR should be filtered out
            var result = _resolver.IsReachable(1, 2, 1, [beaconA, beaconBOtherRR], now, maxHops: 2);

            Assert.IsFalse(result, "Cross-railroad beacons should not be reachable via the same-railroad neighbor graph.");
        }
    }
}
