namespace Web.Server.DTOs
{
    public class RailroadDTO : IEquatable<RailroadDTO?>
    {
        public int ID { get; set; }

        public required string Name { get; set; } = string.Empty;

        public required string Subdivision { get; set; } = string.Empty;

        public override bool Equals(object? obj)
        {
            return Equals(obj as RailroadDTO);
        }

        public bool Equals(RailroadDTO? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   Name == other.Name &&
                   Subdivision == other.Subdivision;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, Name, Subdivision);
        }

        public static bool operator ==(RailroadDTO? left, RailroadDTO? right)
        {
            return EqualityComparer<RailroadDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(RailroadDTO? left, RailroadDTO? right)
        {
            return !(left == right);
        }
    }
}
