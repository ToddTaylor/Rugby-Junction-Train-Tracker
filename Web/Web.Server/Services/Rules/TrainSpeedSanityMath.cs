namespace Web.Server.Services.Rules
{
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
    }
}