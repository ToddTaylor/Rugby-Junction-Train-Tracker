using Web.Server.Enums;

namespace Web.Server.Entities
{
    public class BeaconRailroad : EntityBase, IEquatable<BeaconRailroad?>
    {
        public int BeaconID { get; set; }
        public Beacon Beacon { get; set; }
        public int RailroadID { get; set; }
        public Railroad Railroad { get; set; }
        public required double Latitude { get; set; }
        public required double Longitude { get; set; }

        /// <summary>
        /// The direction in which telemetry data is moving.
        /// </summary>
        public required Direction Direction { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as BeaconRailroad);
        }

        public bool Equals(BeaconRailroad? other)
        {
            return other is not null &&
                   CreatedAt == other.CreatedAt &&
                   BeaconID == other.BeaconID &&
                   EqualityComparer<Beacon>.Default.Equals(Beacon, other.Beacon) &&
                   RailroadID == other.RailroadID &&
                   EqualityComparer<Railroad>.Default.Equals(Railroad, other.Railroad) &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Direction == other.Direction;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CreatedAt, BeaconID, Beacon, RailroadID, Railroad, Latitude, Longitude, Direction);
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
