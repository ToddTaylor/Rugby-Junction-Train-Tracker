using Web.Server.Entities;

namespace Web.Server.Services
{
    public class DistanceService
    {
        public static decimal GetDistanceBetweenInMiles(GeoCoordinate from, GeoCoordinate to)
        {
            // Haversine formula using double for math, return in miles as decimal.
            const double EarthRadiusMiles = 3958.7613d; // mean Earth radius in miles

            double deg2rad = Math.PI / 180.0d;

            double lat1 = from.Latitude * deg2rad;
            double lon1 = from.Longitude * deg2rad;
            double lat2 = to.Latitude * deg2rad;
            double lon2 = to.Longitude * deg2rad;

            double dLat = lat2 - lat1;
            double dLon = lon2 - lon1;

            double a = Math.Sin(dLat / 2.0d) * Math.Sin(dLat / 2.0d) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dLon / 2.0d) * Math.Sin(dLon / 2.0d);

            double c = 2.0d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0d - a));

            double distanceMiles = EarthRadiusMiles * c;

            return (decimal)distanceMiles;
        }
    }
}
