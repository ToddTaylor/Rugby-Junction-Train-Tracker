
namespace Web.Server.DTOs
{
    public class MapPinDTO : IEquatable<MapPinDTO?>
    {
        public required int AddressID { get; set; }

        public required string Direction { get; set; }

        public required double Latitude { get; set; }

        public required double Longitude { get; set; }

        public bool? Moving { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU, HBD
        /// </summary>
        public required string Source { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MapPinDTO);
        }

        public bool Equals(MapPinDTO? other)
        {
            return other is not null &&
                   AddressID == other.AddressID &&
                   Direction == other.Direction &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Moving == other.Moving &&
                   Source == other.Source;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AddressID, Direction, Latitude, Longitude, Moving, Source);
        }

        public static bool operator ==(MapPinDTO? left, MapPinDTO? right)
        {
            return EqualityComparer<MapPinDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(MapPinDTO? left, MapPinDTO? right)
        {
            return !(left == right);
        }
    }
}
