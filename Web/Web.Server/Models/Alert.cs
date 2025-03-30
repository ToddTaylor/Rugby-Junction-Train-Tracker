using System.Text.Json.Serialization;

namespace Web.Server.Models
{
    public class Alert : IEquatable<Alert?>
    {
        [JsonRequired]
        [JsonPropertyName("BeaconID")]
        public required string BeaconID { get; set; }

        [JsonRequired]
        [JsonPropertyName("AddressID")]
        public required int AddressID { get; set; }

        [JsonPropertyName("TrainID")]
        public int? TrainID { get; set; }

        [JsonRequired]
        [JsonPropertyName("Latitude")]
        public required double Latitude { get; set; }

        [JsonRequired]
        [JsonPropertyName("Longitude")]
        public required double Longitude { get; set; }

        [JsonPropertyName("Moving")]
        public bool? Moving { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU, HBD
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("Source")]
        public required string Source { get; set; }

        [JsonRequired]
        [JsonPropertyName("Timestamp")]
        public required DateTime Timestamp { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Alert);
        }

        public bool Equals(Alert? other)
        {
            return other is not null &&
                   BeaconID == other.BeaconID &&
                   AddressID == other.AddressID &&
                   TrainID == other.TrainID &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Moving == other.Moving &&
                   Source == other.Source &&
                   Timestamp == other.Timestamp;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BeaconID, AddressID, TrainID, Latitude, Longitude, Moving, Source, Timestamp);
        }

        public static bool operator ==(Alert? left, Alert? right)
        {
            return EqualityComparer<Alert>.Default.Equals(left, right);
        }

        public static bool operator !=(Alert? left, Alert? right)
        {
            return !(left == right);
        }
    }
}
