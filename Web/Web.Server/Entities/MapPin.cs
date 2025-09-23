using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class MapPin : EntityBase, IEquatable<MapPin?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        public int BeaconID { get; set; }

        public int RailroadID { get; set; }

        public BeaconRailroad? BeaconRailroad { get; set; }

        public string? Direction { get; set; }

        public int? DpuTrainID { get; set; }

        public bool? Moving { get; set; }

        public ICollection<Address> Addresses { get; set; } = [];

        public ICollection<Telemetry> Telemetries { get; set; } = [];

        public MapPin Clone()
        {
            return (MapPin)this.MemberwiseClone();
        }

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
                   BeaconID == other.BeaconID &&
                   RailroadID == other.RailroadID &&
                   EqualityComparer<BeaconRailroad?>.Default.Equals(BeaconRailroad, other.BeaconRailroad) &&
                   Direction == other.Direction &&
                   DpuTrainID == other.DpuTrainID &&
                   Moving == other.Moving &&
                   Addresses.SequenceEqual(other.Addresses) &&      // Custom comparer that works.
                   Telemetries.SequenceEqual(other.Telemetries);    // Custom comparer that works.
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            hash.Add(ID);
            hash.Add(BeaconID);
            hash.Add(RailroadID);
            hash.Add(BeaconRailroad);
            hash.Add(Direction);
            hash.Add(DpuTrainID);
            hash.Add(Moving);
            hash.Add(Addresses);
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
