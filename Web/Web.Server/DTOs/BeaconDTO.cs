namespace Web.Server.DTOs
{
    public class BeaconDTO : IEquatable<BeaconDTO?>
    {
        public int ID { get; set; }

        public int OwnerID { get; set; }

        public bool Online { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime LastUpdate { get; set; }

        public required ICollection<BeaconRailroadDTO> BeaconRailroads { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as BeaconDTO);
        }

        public bool Equals(BeaconDTO? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   OwnerID == other.OwnerID &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   EqualityComparer<ICollection<BeaconRailroadDTO>>.Default.Equals(BeaconRailroads, other.BeaconRailroads);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, OwnerID, CreatedAt, LastUpdate, BeaconRailroads);
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
