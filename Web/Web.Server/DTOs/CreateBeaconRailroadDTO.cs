

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
                   Milepost == other.Milepost &&
                   MultipleTracks == other.MultipleTracks &&
                   Online == other.Online &&
                   Direction == other.Direction &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(base.GetHashCode());
            hash.Add(BeaconID);
            hash.Add(RailroadID);
            hash.Add(Latitude);
            hash.Add(Longitude);
            hash.Add(Milepost);
            hash.Add(MultipleTracks);
            hash.Add(Online);
            hash.Add(Direction);
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            return hash.ToHashCode();
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
