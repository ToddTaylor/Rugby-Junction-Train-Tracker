using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class MapPin : EntityBase, IEquatable<MapPin?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        public required int AddressID { get; set; }

        public int BeaconID { get; set; }

        public int RailroadID { get; set; }

        public BeaconRailroad? BeaconRailroad { get; set; }

        public string? Direction { get; set; }

        public bool? Moving { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU, HBD
        /// </summary>
        public required string Source { get; set; }

        public ICollection<Telemetry>? Telemetries { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MapPin);
        }

        public bool Equals(MapPin? other)
        {
            return other is not null &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   ID == other.ID &&
                   AddressID == other.AddressID &&
                   BeaconID == other.BeaconID &&
                   RailroadID == other.RailroadID &&
                   EqualityComparer<BeaconRailroad?>.Default.Equals(BeaconRailroad, other.BeaconRailroad) &&
                   Direction == other.Direction &&
                   Moving == other.Moving &&
                   Source == other.Source &&
                   EqualityComparer<ICollection<Telemetry>?>.Default.Equals(Telemetries, other.Telemetries);
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            hash.Add(ID);
            hash.Add(AddressID);
            hash.Add(BeaconID);
            hash.Add(RailroadID);
            hash.Add(BeaconRailroad);
            hash.Add(Direction);
            hash.Add(Moving);
            hash.Add(Source);
            hash.Add(Telemetries);
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
