using System.Text.Json.Serialization;

namespace ConsoleApp.Subscribers.LiveTrainTrackingAPI
{
    public class Telemetry
    {
        /// <summary>
        /// ID for HOT and EOT, ADDR for DPU telemetry.
        /// Range: 0 – 65535
        /// An address uniquely and permanently assigned to the transmitting locomotive‘s 
        /// equipment.
        /// </summary>
        [JsonPropertyName("addressId")]
        [JsonRequired]
        public required int AddressID { get; set; }

        /// <summary>
        /// Unique identifier for the beacon sending the alert.
        /// </summary>
        [JsonPropertyName("beaconID")]
        [JsonRequired]
        public required string BeaconID { get; set; }

        /// <summary>
        /// Fixed GUID identifier provided by service host.
        /// </summary>
        [JsonPropertyName("detector")]
        [JsonRequired]
        public required string Detector { get; set; }

        /// <summary>
        /// Indicates whether the train is moving. Only applies to EOT telemetry.
        /// </summary>
        [JsonPropertyName("motion")]
        public bool? Motion { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU
        /// </summary>
        [JsonPropertyName("source")]
        [JsonRequired]
        public required string Source { get; set; }

        /// <summary>
        /// Applies to DPU telemetry only (TRID).
        /// Range: 0 – 255
        /// An arbitrary number assigned to the lead and remote consists upon linking. 
        /// Not globally unique among DPU trains, although likely to be unique in the 
        /// monitored area in a short timeframe.
        /// </summary>
        [JsonPropertyName("trainId")]
        public int? TrainID { get; set; }
    }
}
