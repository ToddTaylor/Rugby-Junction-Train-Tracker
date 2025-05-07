

namespace Web.Server.DTOs
{
    public class CreateBeaconDTO : IEquatable<CreateBeaconDTO?>
    {
        public required int OwnerID { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as CreateBeaconDTO);
        }

        public bool Equals(CreateBeaconDTO? other)
        {
            return other is not null &&
                   OwnerID == other.OwnerID;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OwnerID);
        }

        public static bool operator ==(CreateBeaconDTO? left, CreateBeaconDTO? right)
        {
            return EqualityComparer<CreateBeaconDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(CreateBeaconDTO? left, CreateBeaconDTO? right)
        {
            return !(left == right);
        }
    }
}
