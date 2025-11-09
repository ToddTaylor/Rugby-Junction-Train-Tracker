
namespace Web.Server.Entities
{
    public class Role : IEquatable<Role?>
    {
        public int RoleId { get; set; }

        public string RoleName { get; set; } = string.Empty;

        public string? Description { get; set; }

        // Navigation

        public ICollection<UserRole> UserRoles { get; set; } = [];

        public override bool Equals(object? obj)
        {
            return Equals(obj as Role);
        }

        public bool Equals(Role? other)
        {
            return other is not null &&
                   RoleId == other.RoleId &&
                   RoleName == other.RoleName &&
                   Description == other.Description &&
                   EqualityComparer<ICollection<UserRole>>.Default.Equals(UserRoles, other.UserRoles);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RoleId, RoleName, Description, UserRoles);
        }

        public static bool operator ==(Role? left, Role? right)
        {
            return EqualityComparer<Role>.Default.Equals(left, right);
        }

        public static bool operator !=(Role? left, Role? right)
        {
            return !(left == right);
        }
    }

}
