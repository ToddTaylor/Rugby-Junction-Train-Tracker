
namespace Web.Server.DTOs
{
    public class CreateSubdivisionDTO : IEquatable<CreateSubdivisionDTO?>
    {
        public bool DpuCapable { get; set; } = false;

        public required string Name { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as CreateSubdivisionDTO);
        }

        public bool Equals(CreateSubdivisionDTO? other)
        {
            return other is not null &&
                   DpuCapable == other.DpuCapable &&
                   Name == other.Name;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DpuCapable, Name);
        }

        public static bool operator ==(CreateSubdivisionDTO? left, CreateSubdivisionDTO? right)
        {
            return EqualityComparer<CreateSubdivisionDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(CreateSubdivisionDTO? left, CreateSubdivisionDTO? right)
        {
            return !(left == right);
        }
    }
}
