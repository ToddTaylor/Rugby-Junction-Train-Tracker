namespace Web.Server.DTOs
{
    public class RoleDTO : IEquatable<RoleDTO?>
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as RoleDTO);
        }

        public bool Equals(RoleDTO? other)
        {
            return other is not null &&
                   RoleId == other.RoleId &&
                   RoleName == other.RoleName &&
                   Description == other.Description;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RoleId, RoleName, Description);
        }

        public static bool operator ==(RoleDTO? left, RoleDTO? right)
        {
            return EqualityComparer<RoleDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(RoleDTO? left, RoleDTO? right)
        {
            return !(left == right);
        }
    }
}
