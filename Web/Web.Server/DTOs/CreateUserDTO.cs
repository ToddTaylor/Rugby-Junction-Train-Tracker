namespace Web.Server.DTOs
{
    public class CreateUserDTO : IEquatable<CreateUserDTO?>
    {
        public required string FirstName { get; set; }

        public required string LastName { get; set; }

        public required string Email { get; set; }

        public required bool IsActive { get; set; }

        // Add this property to assign roles by name
        public List<string> Roles { get; set; } = new();

        public override bool Equals(object? obj)
        {
            return Equals(obj as CreateUserDTO);
        }

        public bool Equals(CreateUserDTO? other)
        {
            return other is not null &&
                   FirstName == other.FirstName &&
                   LastName == other.LastName &&
                   Email == other.Email &&
                   IsActive == other.IsActive &&
                   EqualityComparer<List<string>>.Default.Equals(Roles, other.Roles);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FirstName, LastName, Email, IsActive, Roles);
        }

        public static bool operator ==(CreateUserDTO? left, CreateUserDTO? right)
        {
            return EqualityComparer<CreateUserDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(CreateUserDTO? left, CreateUserDTO? right)
        {
            return !(left == right);
        }
    }
}
