using System.Text.Json.Serialization;

namespace Services.Models
{
    public class PacketBase
    {
        [JsonPropertyName("BrakePipePressure")]
        public decimal? BP { get; set; }

        [JsonPropertyName("MovementRailroad")]
        public string? RR { get; set; }

        [JsonPropertyName("EstimatedSignalStrength")]
        public decimal? SIG { get; set; }

        [JsonPropertyName("MovementSymbol")]
        public string? SYMB { get; set; }

        [JsonPropertyName("TimeReceived")]
        public DateTime TimeReceived { get; set; }
    }
}