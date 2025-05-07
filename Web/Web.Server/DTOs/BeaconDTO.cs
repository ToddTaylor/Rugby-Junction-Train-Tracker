
namespace Web.Server.DTOs
{
    public class BeaconDTO : IEquatable<BeaconDTO?>
    {
        public int ID { get; set; }

        public required DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public required ICollection<BeaconRailroadDTO> BeaconRailroads { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as BeaconDTO);
        }

        public bool Equals(BeaconDTO? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   Timestamp == other.Timestamp &&
                   EqualityComparer<ICollection<BeaconRailroadDTO>>.Default.Equals(BeaconRailroads, other.BeaconRailroads);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, Timestamp, BeaconRailroads);
        }

        public static bool operator ==(BeaconDTO? left, BeaconDTO? right)
        {
            return EqualityComparer<BeaconDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(BeaconDTO? left, BeaconDTO? right)
        {
            return !(left == right);
        }
    }
}
