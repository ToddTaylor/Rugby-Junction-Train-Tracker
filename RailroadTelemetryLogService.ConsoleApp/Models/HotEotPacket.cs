using System.Text.Json.Serialization;

namespace ConsoleApp.Models
{
    public class HotEotPacket : IEquatable<HotEotPacket?>
    {
        /// <summary>
        /// ID
        /// </summary>
        [JsonPropertyName("ID")]
        public int? ID { get; set; }

        /// <summary>
        /// TRB
        /// </summary>
        [JsonPropertyName("AirTurbineEquipped")]
        public int? TRB { get; set; }

        /// <summary>
        /// BATCU
        /// </summary>
        [JsonPropertyName("BatteryChargeUsed")]
        public int? BATCU { get; set; }

        /// <summary>
        /// BATST
        /// </summary>
        [JsonPropertyName("BatteryStatus")]
        public string? BATST { get; set; }

        /// <summary>
        /// BP
        /// </summary>
        [JsonPropertyName("BreakPipePressure")]
        public int? BP { get; set; }

        /// <summary>
        /// CMD
        /// </summary>
        [JsonPropertyName("Command")]
        public string? CMD { get; set; }

        /// <summary>
        /// VLV
        /// </summary>
        [JsonPropertyName("EmergencyValveHealth")]
        public int? VLV { get; set; }

        /// <summary>
        /// SIG
        /// </summary>
        [JsonPropertyName("EstimatedSignalStrength")]
        public decimal? SIG { get; set; }

        /// <summary>
        /// MRK
        /// </summary>
        [JsonPropertyName(nameof(MarkerLightStatus))]
        public int? MarkerLightStatus { get; set; }

        /// <summary>
        /// TYP
        /// </summary>
        [JsonPropertyName("MessageType")]
        public string? TYP { get; set; }

        /// <summary>
        /// MOT
        /// </summary>
        [JsonPropertyName("MotionStatus")]
        public int? MOT { get; set; }

        /// <summary>
        /// RR
        /// </summary>
        [JsonPropertyName("MovementRailroad")]
        public string? RR { get; set; }

        /// <summary>
        /// SYMB
        /// </summary>
        [JsonPropertyName("MovementSymbol")]
        public string? SYMB { get; set; }

        /// <summary>
        /// SRC
        /// </summary>
        [JsonPropertyName("Source")]
        public string? SRC { get; set; }

        /// <summary>
        /// Time Received
        /// </summary>
        [JsonPropertyName("TimeReceived")]
        public DateTime TimeReceived { get; set; }

        /// <summary>
        /// CNF
        /// </summary>
        [JsonPropertyName("TwoWayLinkConfirmation")]
        public int? CNF { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as HotEotPacket);
        }

        public bool Equals(HotEotPacket? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   TRB == other.TRB &&
                   BATCU == other.BATCU &&
                   BATST == other.BATST &&
                   BP == other.BP &&
                   CMD == other.CMD &&
                   VLV == other.VLV &&
                   SIG == other.SIG &&
                   MarkerLightStatus == other.MarkerLightStatus &&
                   TYP == other.TYP &&
                   MOT == other.MOT &&
                   RR == other.RR &&
                   SYMB == other.SYMB &&
                   SRC == other.SRC &&
                   TimeReceived == other.TimeReceived &&
                   CNF == other.CNF;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(ID);
            hash.Add(TRB);
            hash.Add(BATCU);
            hash.Add(BATST);
            hash.Add(BP);
            hash.Add(CMD);
            hash.Add(VLV);
            hash.Add(SIG);
            hash.Add(MarkerLightStatus);
            hash.Add(TYP);
            hash.Add(MOT);
            hash.Add(RR);
            hash.Add(SYMB);
            hash.Add(SRC);
            hash.Add(TimeReceived);
            hash.Add(CNF);
            return hash.ToHashCode();
        }

        public static bool operator ==(HotEotPacket? left, HotEotPacket? right)
        {
            return EqualityComparer<HotEotPacket>.Default.Equals(left, right);
        }

        public static bool operator !=(HotEotPacket? left, HotEotPacket? right)
        {
            return !(left == right);
        }
    }
}
