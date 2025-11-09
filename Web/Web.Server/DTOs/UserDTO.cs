namespace Web.Server.DTOs
{
    public class UserDTO : IEquatable<UserDTO?>
    {
        public int ID { get; set; }

        public required string FirstName { get; set; }

        public required string LastName { get; set; }

        public required string Email { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime LastUpdate { get; set; }

        public List<string> Roles { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as UserDTO);
        }

        public bool Equals(UserDTO? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   FirstName == other.FirstName &&
                   LastName == other.LastName &&
                   Email == other.Email &&
                   IsActive == other.IsActive &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   EqualityComparer<List<string>>.Default.Equals(Roles, other.Roles);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, FirstName, LastName, Email, IsActive, CreatedAt, LastUpdate, Roles);
        }

        public static bool operator ==(UserDTO? left, UserDTO? right)
        {
            return EqualityComparer<UserDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(UserDTO? left, UserDTO? right)
        {
            return !(left == right);
        }
    }
}
