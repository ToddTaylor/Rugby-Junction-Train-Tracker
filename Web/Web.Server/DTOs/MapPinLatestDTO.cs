
namespace Web.Server.DTOs
{
    public class MapPinLatestDTO : IEquatable<MapPinLatestDTO?>
    {
        public required int BeaconID { get; set; }

        public required int SubdivisionID { get; set; }

        public string? Railroad { get; set; }

        public string? Subdivision { get; set; }

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
                   SubdivisionID == other.SubdivisionID &&
                   Railroad == other.Railroad &&
                   Subdivision == other.Subdivision &&
                   LastUpdate == other.LastUpdate &&
                   Direction == other.Direction;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BeaconID, SubdivisionID, Railroad, Subdivision, LastUpdate, Direction);
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
