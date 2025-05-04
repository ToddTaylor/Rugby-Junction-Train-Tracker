namespace Web.Server.DTOs
{
    public class CreateTelemetryDTO : IEquatable<CreateTelemetryDTO?>
    {
        public required int BeaconID { get; set; }

        public required int AddressID { get; set; }

        public int? TrainID { get; set; }

        public bool? Moving { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU, HBD
        /// </summary>
        public required string Source { get; set; }

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
                   Source == other.Source;
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
