
namespace Web.Server.Models
{
    public struct GeoCoordinate : IEquatable<GeoCoordinate>
    {
        private readonly double latitude;
        private readonly double longitude;

        public double Latitude { get { return latitude; } }
        public double Longitude { get { return longitude; } }

        public GeoCoordinate(double latitude, double longitude)
        {
            this.latitude = latitude;
            this.longitude = longitude;
        }

        public override string ToString()
        {
            return string.Format("{0},{1}", Latitude, Longitude);
        }

        public override bool Equals(object? obj)
        {
            return obj is GeoCoordinate coordinate && Equals(coordinate);
        }

        public bool Equals(GeoCoordinate other)
        {
            return latitude == other.latitude &&
                   longitude == other.longitude &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(latitude, longitude, Latitude, Longitude);
        }

        public static bool operator ==(GeoCoordinate left, GeoCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GeoCoordinate left, GeoCoordinate right)
        {
            return !(left == right);
        }
    }
}
