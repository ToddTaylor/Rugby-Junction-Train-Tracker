using Web.Server.Entities;

namespace Web.Server.DTOs
{
    public class RailroadDTO : IEquatable<RailroadDTO?>
    {
        public int ID { get; set; }

        public string Name { get; set; }

        public ICollection<Subdivision> Subdivisions { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime LastUpdate { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as RailroadDTO);
        }

        public bool Equals(RailroadDTO? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   Name == other.Name &&
                   EqualityComparer<ICollection<Subdivision>>.Default.Equals(Subdivisions, other.Subdivisions) &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, Name, Subdivisions, CreatedAt, LastUpdate);
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
