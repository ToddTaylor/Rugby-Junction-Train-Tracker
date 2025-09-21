using System.Text.Json.Serialization;

namespace Services.Subscribers.RugbyJunctionAPI
{
    public class Telemetry
    {
        /// <summary>
        /// Unique identifier for the beacon sending the alert.
        /// </summary>
        [JsonPropertyName("BeaconID")]
        [JsonRequired]
        public required string BeaconID { get; set; }

        /// <summary>
        /// ID for HOT and EOT, ADDR for DPU telemetry.
        /// Range: 0 – 65535
        /// An address uniquely and permanently assigned to the transmitting locomotive‘s 
        /// equipment.
        /// </summary>
        [JsonPropertyName("AddressID")]
        [JsonRequired]
        public required int AddressID { get; set; }

        /// <summary>
        /// Applies to DPU telemetry only (TRID).
        /// Range: 0 – 255
        /// An arbitrary number assigned to the lead and remote consists upon linking. 
        /// Not globally unique among DPU trains, although likely to be unique in the 
        /// monitored area in a short timeframe.
        /// </summary>
        [JsonPropertyName("TrainID")]
        public int? TrainID { get; set; }

        /// <summary>
        /// Indicates whether the train is moving. Only applies to EOT telemetry.
        /// </summary>
        [JsonPropertyName("Moving")]
        public bool? Moving { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU, HBD
        /// </summary>
        [JsonPropertyName("Source")]
        [JsonRequired]
        public required string Source { get; set; }

        /// <summary>
        /// Timestamp of when the alert was generated.
        /// </summary>
        [JsonPropertyName("Timestamp")]
        [JsonRequired]
        public required DateTime Timestamp { get; set; }
    }
}
