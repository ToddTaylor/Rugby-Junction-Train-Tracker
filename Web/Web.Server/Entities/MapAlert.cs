using System.Text.Json.Serialization;

namespace Web.Server.Entities
{
    public class MapAlert : IEquatable<MapAlert?>
    {
        [JsonRequired]
        [JsonPropertyName("addressID")]
        public required int AddressID { get; set; }

        [JsonRequired]
        [JsonPropertyName("direction")]
        public required string Direction { get; set; }

        [JsonRequired]
        [JsonPropertyName("latitude")]
        public required double Latitude { get; set; }

        [JsonRequired]
        [JsonPropertyName("longitude")]
        public required double Longitude { get; set; }

        [JsonRequired]
        [JsonPropertyName("moving")]
        public bool? Moving { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU, HBD
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("source")]
        public required string Source { get; set; }

        [JsonRequired]
        [JsonPropertyName("timestamp")]
        public required DateTime Timestamp { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MapAlert);
        }

        public bool Equals(MapAlert? other)
        {
            return other is not null &&
                   AddressID == other.AddressID &&
                   Direction == other.Direction &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Moving == other.Moving &&
                   Source == other.Source &&
                   Timestamp == other.Timestamp;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AddressID, Direction, Latitude, Longitude, Moving, Source, Timestamp);
        }

        public static bool operator ==(MapAlert? left, MapAlert? right)
        {
            return EqualityComparer<MapAlert>.Default.Equals(left, right);
        }

        public static bool operator !=(MapAlert? left, MapAlert? right)
        {
            return !(left == right);
        }
    }
}
