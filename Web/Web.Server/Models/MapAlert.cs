using System.Text.Json.Serialization;

namespace Web.Server.Models
{
    public class MapAlert
    {
        [JsonRequired]
        [JsonPropertyName("addressID")]
        public required int AddressID { get; set; }

        [JsonRequired]
        [JsonPropertyName("direction")]
        public required string Direction { get; set; }

        [JsonRequired]
        [JsonPropertyName("latitude")]
        public required decimal Latitude { get; set; }

        [JsonRequired]
        [JsonPropertyName("longitude")]
        public required decimal Longitude { get; set; }

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
    }
}
