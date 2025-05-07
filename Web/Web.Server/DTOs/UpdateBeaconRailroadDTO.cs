
namespace Web.Server.DTOs
{
    public class UpdateBeaconRailroadDTO : BeaconRailroadDTO, IEquatable<UpdateBeaconRailroadDTO?>
    {
        public override bool Equals(object? obj)
        {
            return Equals(obj as UpdateBeaconRailroadDTO);
        }

        public bool Equals(UpdateBeaconRailroadDTO? other)
        {
            return other is not null &&
                   base.Equals(other) &&
                   BeaconID == other.BeaconID &&
                   RailroadID == other.RailroadID &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Direction == other.Direction;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), BeaconID, RailroadID, Latitude, Longitude, Direction);
        }

        public static bool operator ==(UpdateBeaconRailroadDTO? left, UpdateBeaconRailroadDTO? right)
        {
            return EqualityComparer<UpdateBeaconRailroadDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(UpdateBeaconRailroadDTO? left, UpdateBeaconRailroadDTO? right)
        {
            return !(left == right);
        }
    }
}
