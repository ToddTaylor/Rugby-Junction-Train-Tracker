

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
                   EqualityComparer<RailroadDTO?>.Default.Equals(Railroad, other.Railroad) &&
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
            hash.Add(Railroad);
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
