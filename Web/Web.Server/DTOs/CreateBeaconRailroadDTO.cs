
namespace Web.Server.DTOs
{
    public class CreateBeaconRailroadDTO : BeaconRailroadDTO, IEquatable<CreateBeaconRailroadDTO?>
    {
        public override bool Equals(object? obj)
        {
            return Equals(obj as CreateBeaconRailroadDTO);
        }

        public bool Equals(CreateBeaconRailroadDTO? other)
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

        public static bool operator ==(CreateBeaconRailroadDTO? left, CreateBeaconRailroadDTO? right)
        {
            return EqualityComparer<CreateBeaconRailroadDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(CreateBeaconRailroadDTO? left, CreateBeaconRailroadDTO? right)
        {
            return !(left == right);
        }
    }
}
