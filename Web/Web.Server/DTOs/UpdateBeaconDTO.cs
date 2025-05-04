
namespace Web.Server.DTOs
{
    public class UpdateBeaconDTO : CreateBeaconDTO, IEquatable<UpdateBeaconDTO?>
    {
        public required int ID { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as UpdateBeaconDTO);
        }

        public bool Equals(UpdateBeaconDTO? other)
        {
            return other is not null &&
                   OwnerID == other.OwnerID &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   EqualityComparer<List<int>>.Default.Equals(RailroadIDs, other.RailroadIDs) &&
                   ID == other.ID;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OwnerID, Latitude, Longitude, RailroadIDs, ID);
        }

        public static bool operator ==(UpdateBeaconDTO? left, UpdateBeaconDTO? right)
        {
            return EqualityComparer<UpdateBeaconDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(UpdateBeaconDTO? left, UpdateBeaconDTO? right)
        {
            return !(left == right);
        }
    }
}
