namespace Web.Server.DTOs
{
    public class CreateRailroadDTO : IEquatable<CreateRailroadDTO?>
    {
        public bool DpuCapable { get; set; } = false;

        public required string Name { get; set; } = string.Empty;

        public required string Subdivision { get; set; } = string.Empty;

        public override bool Equals(object? obj)
        {
            return Equals(obj as CreateRailroadDTO);
        }

        public bool Equals(CreateRailroadDTO? other)
        {
            return other is not null &&
                   DpuCapable == other.DpuCapable &&
                   Name == other.Name &&
                   Subdivision == other.Subdivision;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DpuCapable, Name, Subdivision);
        }

        public static bool operator ==(CreateRailroadDTO? left, CreateRailroadDTO? right)
        {
            return EqualityComparer<CreateRailroadDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(CreateRailroadDTO? left, CreateRailroadDTO? right)
        {
            return !(left == right);
        }
    }
}
