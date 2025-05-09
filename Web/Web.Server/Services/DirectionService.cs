using Web.Server.Entities;
using Web.Server.Enums;

namespace Web.Server.Services
{
    public class DirectionService
    {
        public static string GetDirection(GeoCoordinate from, GeoCoordinate to, Direction directionLimits)
        {
            double latDiff = to.Latitude - from.Latitude;
            double lonDiff = to.Longitude - from.Longitude;

            if (latDiff == 0 && lonDiff == 0)
            {
                return string.Empty;
            }

            switch (directionLimits)
            {
                case Direction.All:
                    return GetAllDirections(from, to);
                case Direction.NorthSouth:
                    return GetLatitudeDirections(from, to);
                case Direction.EastWest:
                    return GetLongitudeDirections(from, to);
                case Direction.NortheastSouthwest:
                    return GetDiagonalDirectionNEorSW(from, to);
                case Direction.NorthwestSoutheast:
                    return GetDiagonalDirectionNWorSE(from, to);
                default:
                    return string.Empty;
            }
        }

        private static string GetAllDirections(GeoCoordinate from, GeoCoordinate to)
        {
            string latDirection = "";
            string lonDirection = "";

            if (to.Latitude > from.Latitude)
                latDirection = "N";
            else if (to.Latitude < from.Latitude)
                latDirection = "S";

            if (to.Longitude > from.Longitude)
                lonDirection = "E";
            else if (to.Longitude < from.Longitude)
                lonDirection = "W";

            return latDirection + lonDirection;
        }

        private static string GetLatitudeDirections(GeoCoordinate from, GeoCoordinate to)
        {
            string latDirection = "";

            if (to.Latitude > from.Latitude)
                latDirection = "N";
            else if (to.Latitude < from.Latitude)
                latDirection = "S";

            return latDirection;
        }

        private static string GetLongitudeDirections(GeoCoordinate from, GeoCoordinate to)
        {
            string lonDirection = "";

            if (to.Longitude > from.Longitude)
                lonDirection = "E";
            else if (to.Longitude < from.Longitude)
                lonDirection = "W";

            return lonDirection;
        }

        private static string GetDiagonalDirectionNWorSE(GeoCoordinate from, GeoCoordinate to)
        {
            if (to.Latitude > from.Latitude && to.Longitude < from.Longitude)
                return "NW";

            if (to.Latitude < from.Latitude && to.Longitude > from.Longitude)
                return "SE";

            return string.Empty;
        }

        private static string GetDiagonalDirectionNEorSW(GeoCoordinate from, GeoCoordinate to)
        {
            if (to.Latitude > from.Latitude && to.Longitude > from.Longitude)
                return "NE";

            if (to.Latitude < from.Latitude && to.Longitude < from.Longitude)
                return "SW";

            return string.Empty;
        }

    }
}
