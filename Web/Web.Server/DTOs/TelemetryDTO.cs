namespace Web.Server.DTOs
{
    public class TelemetryDTO : IEquatable<TelemetryDTO?>
    {
        public int ID { get; set; }

        public required BeaconDTO Beacon { get; set; }

        public required int AddressID { get; set; }

        public int? TrainID { get; set; }

        public bool? Moving { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU, HBD
        /// </summary>
        public required string Source { get; set; }

        public required DateTime Timestamp { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as TelemetryDTO);
        }

        public bool Equals(TelemetryDTO? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   EqualityComparer<BeaconDTO>.Default.Equals(Beacon, other.Beacon) &&
                   AddressID == other.AddressID &&
                   TrainID == other.TrainID &&
                   Moving == other.Moving &&
                   Source == other.Source &&
                   Timestamp == other.Timestamp;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, Beacon, AddressID, TrainID, Moving, Source, Timestamp);
        }

        public static bool operator ==(TelemetryDTO? left, TelemetryDTO? right)
        {
            return EqualityComparer<TelemetryDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(TelemetryDTO? left, TelemetryDTO? right)
        {
            return !(left == right);
        }
    }
}
