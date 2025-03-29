namespace RailroadTelemetryLogService.Models
{
    public class HotPacket : BasePacket
    {
        /// <summary>
        /// TRB
        /// </summary>
        public int AirTurbineEquipped { get; set; }

        /// <summary>
        /// BATCU
        /// </summary>
        public int BatteryChargeUsed { get; set; }

        /// <summary>
        /// BATST
        /// </summary>
        public string? BatteryStatus { get; set; }

        /// <summary>
        /// BP
        /// </summary>
        public int BreakPipePressure { get; set; }

        /// <summary>
        /// CMD
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// VLV
        /// </summary>
        public int EmergencyValveHealth { get; set; }

        /// <summary>
        /// SIG
        /// </summary>
        public decimal EstimatedSignalStrength { get; set; }

        /// <summary>
        /// MRK
        /// </summary>
        public int MarkerLightStatus { get; set; }

        /// <summary>
        /// TYP
        /// </summary>
        public string? MessageType { get; set; }

        /// <summary>
        /// MOT
        /// </summary>
        public int MotionStatus { get; set; }

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
        /// CNF
        /// </summary>
        public int TwoWayLinkConfirmation { get; set; }
    }
}
