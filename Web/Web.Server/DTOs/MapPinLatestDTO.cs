
namespace Web.Server.DTOs
{
    public class MapPinLatestDTO : IEquatable<MapPinLatestDTO?>
    {
        public required int BeaconID { get; set; }

        public required DateTime LastUpdate { get; set; }

        public string? Direction { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MapPinLatestDTO);
        }

        public bool Equals(MapPinLatestDTO? other)
        {
            return other is not null &&
                   BeaconID == other.BeaconID &&
                   LastUpdate == other.LastUpdate &&
                   Direction == other.Direction;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BeaconID, LastUpdate, Direction);
        }

        public static bool operator ==(MapPinLatestDTO? left, MapPinLatestDTO? right)
        {
            return EqualityComparer<MapPinLatestDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(MapPinLatestDTO? left, MapPinLatestDTO? right)
        {
            return !(left == right);
        }
    }
}
