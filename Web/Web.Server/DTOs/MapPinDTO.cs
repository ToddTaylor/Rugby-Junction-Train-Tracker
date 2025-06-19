namespace Web.Server.DTOs
{
    public class MapPinDTO : IEquatable<MapPinDTO?>
    {
        // Use a list of AddressDTO instead of Tuple
        public List<AddressDTO> Addresses { get; set; }

        public required int BeaconID { get; set; }

        public required DateTime CreatedAt { get; set; }

        public required DateTime LastUpdate { get; set; }

        public string? Direction { get; set; }

        public required double Latitude { get; set; }

        public required double Longitude { get; set; }

        public required double Milepost { get; set; }

        public bool? Moving { get; set; }

        public string? Railroad { get; set; }

        public int? RailroadID { get; set; }

        public string? Subdivision { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MapPinDTO);
        }

        public bool Equals(MapPinDTO? other)
        {
            return other is not null &&
                   Addresses.SequenceEqual(other.Addresses) && // Custom comparer that works.
                   BeaconID == other.BeaconID &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   Direction == other.Direction &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Milepost == other.Milepost &&
                   Moving == other.Moving &&
                   Railroad == other.Railroad &&
                   RailroadID == other.RailroadID &&
                   Subdivision == other.Subdivision;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(Addresses);
            hash.Add(BeaconID);
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            hash.Add(Direction);
            hash.Add(Latitude);
            hash.Add(Longitude);
            hash.Add(Milepost);
            hash.Add(Moving);
            hash.Add(Railroad);
            hash.Add(RailroadID);
            hash.Add(Subdivision);
            return hash.ToHashCode();
        }

        public static bool operator ==(MapPinDTO? left, MapPinDTO? right)
        {
            return EqualityComparer<MapPinDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(MapPinDTO? left, MapPinDTO? right)
        {
            return !(left == right);
        }
    }
}
