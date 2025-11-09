
namespace Web.Server.Entities
{
    public class UserRole : IEquatable<UserRole?>
    {
        public int UserId { get; set; }

        public User User { get; set; }

        public int RoleId { get; set; }

        public Role Role { get; set; }

        public DateTime AssignedAt { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as UserRole);
        }

        public bool Equals(UserRole? other)
        {
            return other is not null &&
                   UserId == other.UserId &&
                   EqualityComparer<User>.Default.Equals(User, other.User) &&
                   RoleId == other.RoleId &&
                   EqualityComparer<Role>.Default.Equals(Role, other.Role) &&
                   AssignedAt == other.AssignedAt;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(UserId, User, RoleId, Role, AssignedAt);
        }

        public static bool operator ==(UserRole? left, UserRole? right)
        {
            return EqualityComparer<UserRole>.Default.Equals(left, right);
        }

        public static bool operator !=(UserRole? left, UserRole? right)
        {
            return !(left == right);
        }
    }

}
