using Web.Server.Entities;

namespace Web.Server.Services
{
    /// <summary>
    /// Resolves neighbor relationships between beacons using an online-only, same-railroad neighbor graph
    /// based on lat/lon distance and 8-direction sector classification (N/NE/E/SE/S/SW/W/NW).
    /// </summary>
    public interface IBeaconNeighborResolver
    {
        /// <summary>
        /// Returns the direct neighbors of <paramref name="beaconId"/> on the specified railroad.
        /// Direct neighbors are the nearest online beacon per 8-direction sector (N, NE, E, SE, S, SW, W, NW)
        /// among online, same-railroad beacon railroads.
        /// </summary>
        IReadOnlyList<BeaconRailroad> GetDirectNeighbors(
            int beaconId,
            int railroadId,
            IEnumerable<BeaconRailroad> allBeaconRailroads,
            DateTime now);

        /// <summary>
        /// Checks whether <paramref name="toBeaconId"/> is reachable from <paramref name="fromBeaconId"/>
        /// within <paramref name="maxHops"/> hops on the online-only, same-railroad neighbor graph.
        /// </summary>
        bool IsReachable(
            int fromBeaconId,
            int toBeaconId,
            int railroadId,
            IEnumerable<BeaconRailroad> allBeaconRailroads,
            DateTime now,
            int maxHops = 2);
    }
}
