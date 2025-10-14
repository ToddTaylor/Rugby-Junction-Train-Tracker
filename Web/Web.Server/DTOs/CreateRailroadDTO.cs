namespace Web.Server.DTOs
{
    public class CreateRailroadDTO : IEquatable<CreateRailroadDTO?>
    {
        public required string Name { get; set; } = string.Empty;

        public override bool Equals(object? obj)
        {
            return Equals(obj as CreateRailroadDTO);
        }

        public bool Equals(CreateRailroadDTO? other)
        {
            return other is not null &&
                   Name == other.Name;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name);
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
