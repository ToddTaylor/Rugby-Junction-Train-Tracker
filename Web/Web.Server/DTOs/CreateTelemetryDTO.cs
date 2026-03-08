namespace Web.Server.DTOs
{
    public class CreateTelemetryDTO : IEquatable<CreateTelemetryDTO?>
    {
        /// <summary>
        /// The ID of the beacon that sent the telemetry.
        /// </summary>
        /// <remarks>The beacon ID must be obtained from the API administrator.</remarks>
        public required int BeaconID { get; set; }

        /// <summary>
        /// The Address ID of the telemetry from SoftEOT's ID value or 
        /// SoftDPU's ADDR value.
        /// </summary>
        public required int AddressID { get; set; }

        /// <summary>
        /// Optional: The Train ID of the telemetry from SoftDPU's TRID value.
        /// </summary>
        public int? TrainID { get; set; }

        /// <summary>
        /// Optional: The brake pipe pressure of the EOT or DPU.
        /// </summary>
        public decimal? BrakePipePressure { get; set; }

        /// <summary>
        /// Optional: The moving status of the train from SoftEOT's MOT value.
        /// </summary>
        public bool? Moving { get; set; }

        /// <summary>
        /// The source system of the telemetry.
        /// Valid values include: HOT, EOT, DPU, HBD
        /// </summary>
        public required string Source { get; set; }

        /// <summary>
        /// The timestamp of the telemetry event.
        /// </summary>
        public required DateTime Timestamp { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as CreateTelemetryDTO);
        }

        public bool Equals(CreateTelemetryDTO? other)
        {
            return other is not null &&
                   BeaconID == other.BeaconID &&
                   AddressID == other.AddressID &&
                   TrainID == other.TrainID &&
                   Moving == other.Moving &&
                   Source == other.Source &&
                   Timestamp == other.Timestamp;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BeaconID, AddressID, TrainID, Moving, Source);
        }

        public static bool operator ==(CreateTelemetryDTO? left, CreateTelemetryDTO? right)
        {
            return EqualityComparer<CreateTelemetryDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(CreateTelemetryDTO? left, CreateTelemetryDTO? right)
        {
            return !(left == right);
        }
    }
}
