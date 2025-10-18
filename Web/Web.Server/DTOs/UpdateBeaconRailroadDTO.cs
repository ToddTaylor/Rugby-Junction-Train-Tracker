namespace Web.Server.DTOs
{
    public class UpdateBeaconRailroadDTO : CreateBeaconRailroadDTO, IEquatable<UpdateBeaconRailroadDTO?>
    {
        public int BeaconID { get; set; }

        public int SubdivisionID { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as UpdateBeaconRailroadDTO);
        }

        public bool Equals(UpdateBeaconRailroadDTO? other)
        {
            return other is not null &&
                   base.Equals(other) &&
                   BeaconID == other.BeaconID &&
                   SubdivisionID == other.SubdivisionID &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Milepost == other.Milepost &&
                   MultipleTracks == other.MultipleTracks &&
                   Online == other.Online &&
                   Direction == other.Direction &&
                   BeaconID == other.BeaconID &&
                   SubdivisionID == other.SubdivisionID;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(base.GetHashCode());
            hash.Add(BeaconID);
            hash.Add(SubdivisionID);
            hash.Add(Latitude);
            hash.Add(Longitude);
            hash.Add(Milepost);
            hash.Add(MultipleTracks);
            hash.Add(Online);
            hash.Add(Direction);
            hash.Add(BeaconID);
            hash.Add(SubdivisionID);
            return hash.ToHashCode();
        }

        public static bool operator ==(UpdateBeaconRailroadDTO? left, UpdateBeaconRailroadDTO? right)
        {
            return EqualityComparer<UpdateBeaconRailroadDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(UpdateBeaconRailroadDTO? left, UpdateBeaconRailroadDTO? right)
        {
            return !(left == right);
        }
    }
}
