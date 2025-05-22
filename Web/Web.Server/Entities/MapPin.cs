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

        public required double Milepost { get; set; }

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
                   LastUpdate == other.LastUpdate &&
                   AddressID == other.AddressID &&
                   Direction == other.Direction &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Milepost == other.Milepost &&
                   Moving == other.Moving &&
                   Source == other.Source;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            hash.Add(AddressID);
            hash.Add(Direction);
            hash.Add(Latitude);
            hash.Add(Longitude);
            hash.Add(Milepost);
            hash.Add(Moving);
            hash.Add(Source);
            return hash.ToHashCode();
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
