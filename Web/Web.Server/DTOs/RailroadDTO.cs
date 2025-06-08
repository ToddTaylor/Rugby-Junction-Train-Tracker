namespace Web.Server.DTOs
{
    public class RailroadDTO : IEquatable<RailroadDTO?>
    {
        public int ID { get; set; }

        public bool DpuCapable { get; set; }

        public required string Name { get; set; }

        public required string Subdivision { get; set; }

        public required DateTime CreatedAt { get; set; }

        public required DateTime LastUpdate { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as RailroadDTO);
        }

        public bool Equals(RailroadDTO? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   DpuCapable == other.DpuCapable &&
                   Name == other.Name &&
                   Subdivision == other.Subdivision &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, DpuCapable, Name, Subdivision, CreatedAt, LastUpdate);
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
