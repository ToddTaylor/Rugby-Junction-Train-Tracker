namespace Web.Server.DTOs
{
    public class BeaconDTO : IEquatable<BeaconDTO?>
    {
        public int ID { get; set; }

        public required UpdateOwnerDTO Owner { get; set; }

        public required double Latitude { get; set; }

        public required double Longitude { get; set; }

        public required DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public required ICollection<RailroadDTO> Railroads { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as BeaconDTO);
        }

        public bool Equals(BeaconDTO? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   EqualityComparer<UpdateOwnerDTO>.Default.Equals(Owner, other.Owner) &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Timestamp == other.Timestamp &&
                   EqualityComparer<ICollection<RailroadDTO>>.Default.Equals(Railroads, other.Railroads);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, Owner, Latitude, Longitude, Timestamp, Railroads);
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
