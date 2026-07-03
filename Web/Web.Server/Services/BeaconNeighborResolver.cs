using Web.Server.Entities;

namespace Web.Server.Services
{
    /// <summary>
    /// Resolves neighbor relationships between beacons on the same railroad using an online-only
    /// neighbor graph. Direct neighbors are determined by nearest-per-sector (8 directions: N/NE/E/SE/S/SW/W/NW)
    /// using Haversine distance and compass bearing.
    /// </summary>
    public class BeaconNeighborResolver : IBeaconNeighborResolver
    {
        private const double OnlineThresholdMinutes = 15.0;
        private const double EarthRadiusKm = 6371.0;

        /// <inheritdoc/>
        public IReadOnlyList<BeaconRailroad> GetDirectNeighbors(
            int beaconId,
            int railroadId,
            IEnumerable<BeaconRailroad> allBeaconRailroads,
            DateTime now)
        {
            var candidates = GetOnlineSameRailroadCandidates(railroadId, allBeaconRailroads, now);
            var source = candidates.FirstOrDefault(br => br.BeaconID == beaconId);
            if (source == null)
                return Array.Empty<BeaconRailroad>();

            // For each of 8 sectors, track the nearest candidate (excluding the source beacon itself)
            var sectored = new Dictionary<int, (BeaconRailroad Beacon, double DistanceKm)>();

            foreach (var candidate in candidates)
            {
                if (candidate.BeaconID == beaconId)
                    continue;

                var bearing = CalculateBearing(source.Latitude, source.Longitude, candidate.Latitude, candidate.Longitude);
                var sector = GetSector(bearing);
                var distance = HaversineKm(source.Latitude, source.Longitude, candidate.Latitude, candidate.Longitude);

                if (!sectored.TryGetValue(sector, out var existing) || distance < existing.DistanceKm)
                {
                    sectored[sector] = (candidate, distance);
                }
            }

            return sectored.Values.Select(v => v.Beacon).ToList();
        }

        /// <inheritdoc/>
        public bool IsReachable(
            int fromBeaconId,
            int toBeaconId,
            int railroadId,
            IEnumerable<BeaconRailroad> allBeaconRailroads,
            DateTime now,
            int maxHops = 2)
        {
            if (fromBeaconId == toBeaconId)
                return true;

            var beaconRailroadList = allBeaconRailroads as IList<BeaconRailroad> ?? allBeaconRailroads.ToList();

            var visited = new HashSet<int> { fromBeaconId };
            var frontier = new HashSet<int> { fromBeaconId };

            for (int hop = 0; hop < maxHops; hop++)
            {
                var nextFrontier = new HashSet<int>();

                foreach (var currentBeaconId in frontier)
                {
                    var neighbors = GetDirectNeighbors(currentBeaconId, railroadId, beaconRailroadList, now);
                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor.BeaconID == toBeaconId)
                            return true;

                        if (!visited.Contains(neighbor.BeaconID))
                        {
                            nextFrontier.Add(neighbor.BeaconID);
                            visited.Add(neighbor.BeaconID);
                        }
                    }
                }

                frontier = nextFrontier;
                if (frontier.Count == 0)
                    break;
            }

            return false;
        }

        private static IList<BeaconRailroad> GetOnlineSameRailroadCandidates(
            int railroadId,
            IEnumerable<BeaconRailroad> allBeaconRailroads,
            DateTime now)
        {
            var threshold = now.AddMinutes(-OnlineThresholdMinutes);
            return allBeaconRailroads
                .Where(br =>
                    br.Subdivision?.RailroadID == railroadId &&
                    br.LastUpdate != default &&
                    br.LastUpdate >= threshold)
                .ToList();
        }

        /// <summary>
        /// Computes the Haversine great-circle distance in kilometres between two lat/lon points.
        /// </summary>
        private static double HaversineKm(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
        {
            var dLat = ToRad(lat2Deg - lat1Deg);
            var dLon = ToRad(lon2Deg - lon1Deg);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(ToRad(lat1Deg)) * Math.Cos(ToRad(lat2Deg))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusKm * c;
        }

        /// <summary>
        /// Computes the initial compass bearing (0–360°, clockwise from North) from point 1 to point 2.
        /// </summary>
        private static double CalculateBearing(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
        {
            var dLon = ToRad(lon2Deg - lon1Deg);
            var y = Math.Sin(dLon) * Math.Cos(ToRad(lat2Deg));
            var x = Math.Cos(ToRad(lat1Deg)) * Math.Sin(ToRad(lat2Deg))
                  - Math.Sin(ToRad(lat1Deg)) * Math.Cos(ToRad(lat2Deg)) * Math.Cos(dLon);
            var bearingDeg = Math.Atan2(y, x) * 180.0 / Math.PI;
            return (bearingDeg + 360) % 360;
        }

        /// <summary>
        /// Maps a bearing in degrees to an 8-direction sector index (0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW).
        /// Each sector spans 45°.  N is centred at 0° (337.5–22.5°).
        /// </summary>
        private static int GetSector(double bearingDeg)
        {
            return (int)Math.Floor((bearingDeg + 22.5) / 45) % 8;
        }

        private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
    }
}
