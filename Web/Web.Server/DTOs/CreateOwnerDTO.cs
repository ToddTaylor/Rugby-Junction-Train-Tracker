namespace Web.Server.DTOs
{
    public class CreateOwnerDTO : IEquatable<CreateOwnerDTO?>
    {
        public required string FirstName { get; set; }

        public required string LastName { get; set; }

        public required string Email { get; set; }

        public required string City { get; set; }

        public required string State { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as CreateOwnerDTO);
        }

        public bool Equals(CreateOwnerDTO? other)
        {
            return other is not null &&
                   FirstName == other.FirstName &&
                   LastName == other.LastName &&
                   Email == other.Email &&
                   City == other.City &&
                   State == other.State;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FirstName, LastName, Email, City, State);
        }

        public static bool operator ==(CreateOwnerDTO? left, CreateOwnerDTO? right)
        {
            return EqualityComparer<CreateOwnerDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(CreateOwnerDTO? left, CreateOwnerDTO? right)
        {
            return !(left == right);
        }
    }
}
