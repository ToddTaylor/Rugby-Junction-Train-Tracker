using Web.Server.Entities;

namespace Web.Server.Services.Rules
{
    /// <summary>
    /// How a distance between two beacon railroads was measured.
    /// </summary>
    internal enum DistanceBasis
    {
        /// <summary>Linear subtraction of two mileposts on the same subdivision.</summary>
        Milepost,

        /// <summary>Great-circle distance between two sets of coordinates.</summary>
        Straightline
    }

    /// <summary>
    /// A distance in miles along with how it was measured.
    /// </summary>
    internal readonly record struct MeasuredDistance(double Miles, DistanceBasis Basis);

    internal static class TrainSpeedSanityMath
    {
        // Number is doubled for HOT and EOT both reaching to the beacon.
        public const double TrainRadioRangeMiles = 3.0 * 2;

        public const double TrainLengthMiles = 1;

        public static double GetAdjustedDistanceMiles(double rawDistanceMiles)
        {
            var adjustedDistance = rawDistanceMiles - TrainRadioRangeMiles - TrainLengthMiles;
            return adjustedDistance < 0 ? 0 : adjustedDistance;
        }

        public static double? TryGetSpeedMph(double distanceMiles, DateTime fromUtc, DateTime toUtc)
        {
            var hours = (toUtc - fromUtc).TotalHours;
            if (hours <= 0)
            {
                return null;
            }

            return distanceMiles / hours;
        }

        /// <summary>
        /// Distance between two beacon railroads. Linear milepost subtraction is valid ONLY when both
        /// are on the SAME subdivision. Mileposts on different subdivisions are independent numbering
        /// schemes even within one railroad: Junction City is MP 260 on the CN Superior Subdivision and
        /// a different milepost entirely on the CN Valley Subdivision, so subtracting across the two
        /// produces a meaningless distance (see issue #80).
        ///
        /// When the subdivisions differ, falls back to great-circle distance between the two sets of
        /// coordinates. That is a lower bound on actual track distance (track curves and detours), so
        /// it can only under-estimate speed, which biases toward keeping telemetry rather than
        /// discarding it - the correct error direction for a sanity check.
        ///
        /// Returns null when neither basis is available.
        /// </summary>
        public static MeasuredDistance? TryGetDistanceMiles(BeaconRailroad from, BeaconRailroad to)
        {
            if (from.SubdivisionID == to.SubdivisionID)
            {
                return new MeasuredDistance(Math.Abs(to.Milepost - from.Milepost), DistanceBasis.Milepost);
            }

            if (!HasUsableCoordinates(from) || !HasUsableCoordinates(to))
            {
                return null;
            }

            var miles = (double)DistanceService.GetDistanceBetweenInMiles(
                new GeoCoordinate(from.Latitude, from.Longitude),
                new GeoCoordinate(to.Latitude, to.Longitude));

            return new MeasuredDistance(miles, DistanceBasis.Straightline);
        }

        /// <summary>
        /// Latitude and Longitude are required non-nullable doubles, so an unpopulated beacon railroad
        /// reads as 0,0 rather than null. Treat exact zero as missing: a stray 0,0 row would otherwise
        /// compute a ~5,500 mile distance from Wisconsin and discard everything.
        /// </summary>
        private static bool HasUsableCoordinates(BeaconRailroad beaconRailroad)
        {
            return beaconRailroad.Latitude != 0
                && beaconRailroad.Longitude != 0
                && !double.IsNaN(beaconRailroad.Latitude)
                && !double.IsNaN(beaconRailroad.Longitude);
        }
    }
}
