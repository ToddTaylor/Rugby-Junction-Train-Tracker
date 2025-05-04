using System.Text.Json.Serialization;

namespace ConsoleApp.Models
{
    public class HotEotPacket : PacketBase, IEquatable<HotEotPacket?>
    {
        [JsonPropertyName("ID")]
        public required string ID { get; set; }

        [JsonPropertyName("AirTurbineEquipped")]
        public int? TRB { get; set; }

        [JsonPropertyName("BatteryChargeUsed")]
        public int? BATCU { get; set; }

        [JsonPropertyName("BatteryStatus")]
        public string? BATST { get; set; }

        [JsonPropertyName("Command")]
        public string? CMD { get; set; }

        [JsonPropertyName("EmergencyValveHealth")]
        public int? VLV { get; set; }

        [JsonPropertyName("MarkerLightStatus")]
        public int? MRK { get; set; }

        [JsonPropertyName("MessageType")]
        public string? TYP { get; set; }

        [JsonPropertyName("MotionStatus")]
        public int? MOT { get; set; }

        [JsonPropertyName("Source")]
        public required string SRC { get; set; }

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
                   MRK == other.MRK &&
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
            hash.Add(MRK);
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
