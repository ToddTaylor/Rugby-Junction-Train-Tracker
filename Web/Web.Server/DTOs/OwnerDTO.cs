
namespace Web.Server.DTOs
{
    public class OwnerDTO : IEquatable<OwnerDTO?>
    {
        public int ID { get; set; }

        public required string FirstName { get; set; }

        public required string LastName { get; set; }

        public required string Email { get; set; }

        public required string City { get; set; }

        public required string State { get; set; }

        public required DateTime CreatedAt { get; set; }

        public required ICollection<BeaconDTO> Beacons { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as OwnerDTO);
        }

        public bool Equals(OwnerDTO? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   FirstName == other.FirstName &&
                   LastName == other.LastName &&
                   Email == other.Email &&
                   City == other.City &&
                   State == other.State &&
                   CreatedAt == other.CreatedAt &&
                   EqualityComparer<ICollection<BeaconDTO>>.Default.Equals(Beacons, other.Beacons);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, FirstName, LastName, Email, City, State, CreatedAt, Beacons);
        }

        public static bool operator ==(OwnerDTO? left, OwnerDTO? right)
        {
            return EqualityComparer<OwnerDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(OwnerDTO? left, OwnerDTO? right)
        {
            return !(left == right);
        }
    }
}
