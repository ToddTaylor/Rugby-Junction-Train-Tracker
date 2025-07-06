using System.Text.Json.Serialization;

namespace ConsoleApp.Models
{
    public class DpuPacket : PacketBase, IEquatable<DpuPacket?>
    {
        [JsonPropertyName("Address")]
        public string ADDR { get; set; }

        [JsonPropertyName("TrainID")]
        public string TRID { get; set; }

        [JsonPropertyName("PacketType")]
        public string? TP { get; set; }

        [JsonPropertyName("PacketOrigin")]
        public string? OR { get; set; }

        [JsonPropertyName("RepeatCode")]
        public string? RP { get; set; }

        [JsonPropertyName("SequenceNumber")]
        public int? SEQ { get; set; }

        [JsonPropertyName("NumberOfRemotes")]
        public int? NRM { get; set; }

        [JsonPropertyName("RemoteID")]
        public int? RMID { get; set; }

        [JsonPropertyName("PowerSetting")]
        public string? Power { get; set; }

        [JsonPropertyName("Reverser")]
        public string? REV { get; set; }

        [JsonPropertyName("Motoring")]
        public int? MTR { get; set; }

        [JsonPropertyName("BrakeValveCutIn")]
        public int? BVIN { get; set; }

        [JsonPropertyName("Sand")]
        public int? Sand { get; set; }

        [JsonPropertyName("ParkingBrake")]
        public int? PRK { get; set; }

        [JsonPropertyName("TractiveEffort")]
        public int? TRE { get; set; }

        [JsonPropertyName("BrakePipeReduction")]
        public string? BPRED { get; set; }

        [JsonPropertyName("EqualizingReservoirPressure")]
        public decimal? ER { get; set; }

        [JsonPropertyName("AirFlow")]
        public int? AF { get; set; }

        [JsonPropertyName("MainReservoirPressure")]
        public int? MR { get; set; }

        [JsonPropertyName("IndependentBrakeControl")]
        public int? IB { get; set; }

        [JsonPropertyName("BrakeCylinderPressure")]
        public decimal? BC { get; set; }

        [JsonPropertyName("LeadLocomotiveOwner")]
        public string? LOWN { get; set; }

        [JsonPropertyName("LeadLocomotiveNumber")]
        public string? LLOC { get; set; }

        [JsonPropertyName("RemoteLocomotiveOwner")]
        public string? ROWN { get; set; }

        [JsonPropertyName("RemoteLocomotiveNumber")]
        public string? RLOC { get; set; }

        [JsonPropertyName("MiscellaneousData")]
        public string? MISC { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as DpuPacket);
        }

        public bool Equals(DpuPacket? other)
        {
            return other is not null &&
                   BP == other.BP &&
                   RR == other.RR &&
                   SIG == other.SIG &&
                   SYMB == other.SYMB &&
                   TimeReceived == other.TimeReceived &&
                   ADDR == other.ADDR &&
                   TRID == other.TRID &&
                   TP == other.TP &&
                   OR == other.OR &&
                   RP == other.RP &&
                   SEQ == other.SEQ &&
                   NRM == other.NRM &&
                   RMID == other.RMID &&
                   Power == other.Power &&
                   REV == other.REV &&
                   MTR == other.MTR &&
                   BVIN == other.BVIN &&
                   Sand == other.Sand &&
                   PRK == other.PRK &&
                   TRE == other.TRE &&
                   BPRED == other.BPRED &&
                   ER == other.ER &&
                   AF == other.AF &&
                   MR == other.MR &&
                   IB == other.IB &&
                   BC == other.BC &&
                   LOWN == other.LOWN &&
                   LLOC == other.LLOC &&
                   ROWN == other.ROWN &&
                   RLOC == other.RLOC &&
                   MISC == other.MISC;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(BP);
            hash.Add(RR);
            hash.Add(SIG);
            hash.Add(SYMB);
            hash.Add(TimeReceived);
            hash.Add(ADDR);
            hash.Add(TRID);
            hash.Add(TP);
            hash.Add(OR);
            hash.Add(RP);
            hash.Add(SEQ);
            hash.Add(NRM);
            hash.Add(RMID);
            hash.Add(Power);
            hash.Add(REV);
            hash.Add(MTR);
            hash.Add(BVIN);
            hash.Add(Sand);
            hash.Add(PRK);
            hash.Add(TRE);
            hash.Add(BPRED);
            hash.Add(ER);
            hash.Add(AF);
            hash.Add(MR);
            hash.Add(IB);
            hash.Add(BC);
            hash.Add(LOWN);
            hash.Add(LLOC);
            hash.Add(ROWN);
            hash.Add(RLOC);
            hash.Add(MISC);
            return hash.ToHashCode();
        }

        public static bool operator ==(DpuPacket? left, DpuPacket? right)
        {
            return EqualityComparer<DpuPacket>.Default.Equals(left, right);
        }

        public static bool operator !=(DpuPacket? left, DpuPacket? right)
        {
            return !(left == right);
        }
    }
}