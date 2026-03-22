using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Web.Server.Enums;

namespace Web.Server.Entities
{
    public class BeaconRailroad : EntityBase, IEquatable<BeaconRailroad?>
    {
        [Required]
        public int BeaconID { get; set; }

        public Beacon? Beacon { get; set; }

        [Required]
        public Direction Direction { get; set; }

        [Required]
        public int SubdivisionID { get; set; }

        public Subdivision Subdivision { get; set; }

        [Required]
        public double Latitude { get; set; }

        [Required]
        public double Longitude { get; set; }

        [NotMapped]
        public ICollection<MapPin> MapPins { get; set; } = [];

        [Required]
        public double Milepost { get; set; }

        [Required]
        public bool MultipleTracks { get; set; } = false;

        public double? MaxDetectionDistanceMiles { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as BeaconRailroad);
        }

        public bool Equals(BeaconRailroad? other)
        {
            return other is not null &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   BeaconID == other.BeaconID &&
                   EqualityComparer<Beacon?>.Default.Equals(Beacon, other.Beacon) &&
                   Direction == other.Direction &&
                   SubdivisionID == other.SubdivisionID &&
                   EqualityComparer<Subdivision?>.Default.Equals(Subdivision, other.Subdivision) &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   EqualityComparer<ICollection<MapPin>>.Default.Equals(MapPins, other.MapPins) &&
                   Milepost == other.Milepost &&
                   MultipleTracks == other.MultipleTracks &&
                   MaxDetectionDistanceMiles == other.MaxDetectionDistanceMiles;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            hash.Add(BeaconID);
            hash.Add(Beacon);
            hash.Add(Direction);
            hash.Add(SubdivisionID);
            hash.Add(Subdivision);
            hash.Add(Latitude);
            hash.Add(Longitude);
            hash.Add(MapPins);
            hash.Add(Milepost);
            hash.Add(MultipleTracks);
            hash.Add(MaxDetectionDistanceMiles);
            return hash.ToHashCode();
        }

        public static bool operator ==(BeaconRailroad? left, BeaconRailroad? right)
        {
            return EqualityComparer<BeaconRailroad>.Default.Equals(left, right);
        }

        public static bool operator !=(BeaconRailroad? left, BeaconRailroad? right)
        {
            return !(left == right);
        }
    }
}
