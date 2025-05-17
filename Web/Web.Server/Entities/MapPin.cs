using System.ComponentModel.DataAnnotations;

namespace Web.Server.Entities
{
    public class MapPin : EntityBase, IEquatable<MapPin?>
    {
        [Key]
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
            return Equals(obj as MapPin);
        }

        public bool Equals(MapPin? other)
        {
            return other is not null &&
                   CreatedAt == other.CreatedAt &&
                   AddressID == other.AddressID &&
                   Direction == other.Direction &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Moving == other.Moving &&
                   Source == other.Source;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CreatedAt, AddressID, Direction, Latitude, Longitude, Moving, Source);
        }

        public static bool operator ==(MapPin? left, MapPin? right)
        {
            return EqualityComparer<MapPin>.Default.Equals(left, right);
        }

        public static bool operator !=(MapPin? left, MapPin? right)
        {
            return !(left == right);
        }
    }
}
