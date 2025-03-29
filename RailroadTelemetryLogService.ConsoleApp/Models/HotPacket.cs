namespace RailroadTelemetryLogService.Models
{
    public class HotPacket : IEquatable<HotPacket?>
    {
        /// <summary>
        /// ID
        /// </summary>
        public int? ID { get; set; }

        /// <summary>
        /// TRB
        /// </summary>
        public int? AirTurbineEquipped { get; set; }

        /// <summary>
        /// BATCU
        /// </summary>
        public int? BatteryChargeUsed { get; set; }

        /// <summary>
        /// BATST
        /// </summary>
        public string? BatteryStatus { get; set; }

        /// <summary>
        /// BP
        /// </summary>
        public int? BreakPipePressure { get; set; }

        /// <summary>
        /// CMD
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// VLV
        /// </summary>
        public int? EmergencyValveHealth { get; set; }

        /// <summary>
        /// SIG
        /// </summary>
        public decimal? EstimatedSignalStrength { get; set; }

        /// <summary>
        /// MRK
        /// </summary>
        public int? MarkerLightStatus { get; set; }

        /// <summary>
        /// TYP
        /// </summary>
        public string? MessageType { get; set; }

        /// <summary>
        /// MOT
        /// </summary>
        public int? MotionStatus { get; set; }

        /// <summary>
        /// RR
        /// </summary>
        public string? MovementRailroad { get; set; }

        /// <summary>
        /// SYMB
        /// </summary>
        public string? MovementSymbol { get; set; }

        /// <summary>
        /// SRC
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Time Received
        /// </summary>
        public DateTime TimeReceived { get; set; }

        /// <summary>
        /// CNF
        /// </summary>
        public int? TwoWayLinkConfirmation { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as HotPacket);
        }

        public bool Equals(HotPacket? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   AirTurbineEquipped == other.AirTurbineEquipped &&
                   BatteryChargeUsed == other.BatteryChargeUsed &&
                   BatteryStatus == other.BatteryStatus &&
                   BreakPipePressure == other.BreakPipePressure &&
                   Command == other.Command &&
                   EmergencyValveHealth == other.EmergencyValveHealth &&
                   EstimatedSignalStrength == other.EstimatedSignalStrength &&
                   MarkerLightStatus == other.MarkerLightStatus &&
                   MessageType == other.MessageType &&
                   MotionStatus == other.MotionStatus &&
                   MovementRailroad == other.MovementRailroad &&
                   MovementSymbol == other.MovementSymbol &&
                   Source == other.Source &&
                   TimeReceived == other.TimeReceived &&
                   TwoWayLinkConfirmation == other.TwoWayLinkConfirmation;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(ID);
            hash.Add(AirTurbineEquipped);
            hash.Add(BatteryChargeUsed);
            hash.Add(BatteryStatus);
            hash.Add(BreakPipePressure);
            hash.Add(Command);
            hash.Add(EmergencyValveHealth);
            hash.Add(EstimatedSignalStrength);
            hash.Add(MarkerLightStatus);
            hash.Add(MessageType);
            hash.Add(MotionStatus);
            hash.Add(MovementRailroad);
            hash.Add(MovementSymbol);
            hash.Add(Source);
            hash.Add(TimeReceived);
            hash.Add(TwoWayLinkConfirmation);
            return hash.ToHashCode();
        }

        public static bool operator ==(HotPacket? left, HotPacket? right)
        {
            return EqualityComparer<HotPacket>.Default.Equals(left, right);
        }

        public static bool operator !=(HotPacket? left, HotPacket? right)
        {
            return !(left == right);
        }
    }
}
