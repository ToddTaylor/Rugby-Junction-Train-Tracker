namespace Web.Server.DTOs
{
    public class MapPinDTO : IEquatable<MapPinDTO?>
    {
        public required int ID { get; set; }

        public required int BeaconID { get; set; }

        public required string BeaconName { get; set; }

        public required DateTime CreatedAt { get; set; }

        public required DateTime LastUpdate { get; set; }

        public string? Direction { get; set; }

        public required double Latitude { get; set; }

        public required double Longitude { get; set; }

        public required double Milepost { get; set; }

        public bool? Moving { get; set; }

        public bool IsLocal { get; set; }

        public string? Railroad { get; set; }

        public string? Subdivision { get; set; }

        public int? SubdivisionID { get; set; }

        public List<AddressDTO> Addresses { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MapPinDTO);
        }

        public bool Equals(MapPinDTO? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   BeaconID == other.BeaconID &&
                   BeaconName == other.BeaconName &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   Direction == other.Direction &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Milepost == other.Milepost &&
                   Moving == other.Moving &&
                   IsLocal == other.IsLocal &&
                   Railroad == other.Railroad &&
                   Subdivision == other.Subdivision &&
                   SubdivisionID == other.SubdivisionID &&
                   Addresses.SequenceEqual(other.Addresses); // Custom comparer that works.
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(ID);
            hash.Add(BeaconID);
            hash.Add(BeaconName);
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            hash.Add(Direction);
            hash.Add(Latitude);
            hash.Add(Longitude);
            hash.Add(Milepost);
            hash.Add(Moving);
            hash.Add(IsLocal);
            hash.Add(Railroad);
            hash.Add(Subdivision);
            hash.Add(SubdivisionID);
            hash.Add(Addresses);
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
