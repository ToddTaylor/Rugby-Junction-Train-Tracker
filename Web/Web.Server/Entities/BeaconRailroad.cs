using Web.Server.Enums;

namespace Web.Server.Entities
{
    public class BeaconRailroad : EntityBase, IEquatable<BeaconRailroad?>
    {
        public int BeaconID { get; set; }
        public Beacon Beacon { get; set; }
        public required Direction Direction { get; set; }
        public int RailroadID { get; set; }
        public Railroad Railroad { get; set; }
        public required double Latitude { get; set; }
        public required double Longitude { get; set; }
        public required double Milepost { get; set; }

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
                   EqualityComparer<Beacon>.Default.Equals(Beacon, other.Beacon) &&
                   Direction == other.Direction &&
                   RailroadID == other.RailroadID &&
                   EqualityComparer<Railroad>.Default.Equals(Railroad, other.Railroad) &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Milepost == other.Milepost;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            hash.Add(BeaconID);
            hash.Add(Beacon);
            hash.Add(Direction);
            hash.Add(RailroadID);
            hash.Add(Railroad);
            hash.Add(Latitude);
            hash.Add(Longitude);
            hash.Add(Milepost);
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
