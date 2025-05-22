


namespace Web.Server.DTOs
{
    public class MapPinDTO : IEquatable<MapPinDTO?>
    {
        public required int AddressID { get; set; }

        public required DateTime CreatedAt { get; set; }

        public required DateTime LastUpdate { get; set; }

        public required string Direction { get; set; }

        public required double Latitude { get; set; }

        public required double Longitude { get; set; }

        public required double Milepost { get; set; }

        public bool? Moving { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU, HBD
        /// </summary>
        public required string Source { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MapPinDTO);
        }

        public bool Equals(MapPinDTO? other)
        {
            return other is not null &&
                   AddressID == other.AddressID &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   Direction == other.Direction &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Milepost == other.Milepost &&
                   Moving == other.Moving &&
                   Source == other.Source;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(AddressID);
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            hash.Add(Direction);
            hash.Add(Latitude);
            hash.Add(Longitude);
            hash.Add(Milepost);
            hash.Add(Moving);
            hash.Add(Source);
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
