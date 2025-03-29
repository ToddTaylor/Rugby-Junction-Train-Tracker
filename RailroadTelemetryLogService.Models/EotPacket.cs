
namespace RailroadTelemetryDecoder.Models
{
    public class EotPacket : BasePacket, IEquatable<EotPacket?>
    {
        public string ArmStatus { get; set; }
        public string BatteryCharge { get; set; }
        public string BatteryCondition { get; set; }
        public string BatteryConditionText { get; set; }
        public string CipherKey { get; set; }
        public string CheckBitsCipher { get; set; }
        public char ConfirmationIndicator { get; set; }
        public char MarkerBattery { get; set; }
        public char MarkerLight { get; set; }
        public string MessageType { get; set; }
        public char Motion { get; set; }
        public int Pressure { get; set; }
        public char Spare { get; set; }
        public char Turbine { get; set; }
        public int UnitAddress { get; set; }
        public char ValveCktStatus { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as EotPacket);
        }

        public bool Equals(EotPacket? other)
        {
            return other is not null &&
                   CheckBits == other.CheckBits &&
                   CheckBitsRx == other.CheckBitsRx &&
                   DataBlock == other.DataBlock &&
                   FrameSync == other.FrameSync &&
                   Generator == other.Generator &&
                   Valid == other.Valid &&
                   ArmStatus == other.ArmStatus &&
                   BatteryCharge == other.BatteryCharge &&
                   BatteryCondition == other.BatteryCondition &&
                   BatteryConditionText == other.BatteryConditionText &&
                   CipherKey == other.CipherKey &&
                   CheckBitsCipher == other.CheckBitsCipher &&
                   ConfirmationIndicator == other.ConfirmationIndicator &&
                   MarkerBattery == other.MarkerBattery &&
                   MarkerLight == other.MarkerLight &&
                   MessageType == other.MessageType &&
                   Motion == other.Motion &&
                   Pressure == other.Pressure &&
                   Spare == other.Spare &&
                   Turbine == other.Turbine &&
                   UnitAddress == other.UnitAddress &&
                   ValveCktStatus == other.ValveCktStatus;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(CheckBits);
            hash.Add(CheckBitsRx);
            hash.Add(DataBlock);
            hash.Add(FrameSync);
            hash.Add(Generator);
            hash.Add(Valid);
            hash.Add(ArmStatus);
            hash.Add(BatteryCharge);
            hash.Add(BatteryCondition);
            hash.Add(BatteryConditionText);
            hash.Add(CipherKey);
            hash.Add(CheckBitsCipher);
            hash.Add(ConfirmationIndicator);
            hash.Add(MarkerBattery);
            hash.Add(MarkerLight);
            hash.Add(MessageType);
            hash.Add(Motion);
            hash.Add(Pressure);
            hash.Add(Spare);
            hash.Add(Turbine);
            hash.Add(UnitAddress);
            hash.Add(ValveCktStatus);
            return hash.ToHashCode();
        }

        public static bool operator ==(EotPacket? left, EotPacket? right)
        {
            return EqualityComparer<EotPacket>.Default.Equals(left, right);
        }

        public static bool operator !=(EotPacket? left, EotPacket? right)
        {
            return !(left == right);
        }
    }
}
