


namespace Web.Server.DTOs
{
    public class TelemetryDTO : IEquatable<TelemetryDTO?>
    {
        public int ID { get; set; }

        public required int BeaconID { get; set; }

        public required string BeaconName { get; set; }

        public required int AddressID { get; set; }

        public int? TrainID { get; set; }

        public decimal? BrakePipePressure { get; set; }

        public bool? Moving { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU, HBD
        /// </summary>
        public required string Source { get; set; }

        /// <summary>
        /// Indicates whether this telemetry record has been discarded and not to be used in processing.
        /// </summary>
        public bool Discarded { get; set; }

        /// <summary>
        /// Reason why this telemetry record was discarded (if applicable).
        /// </summary>
        public string? DiscardReason { get; set; }

        public required DateTime CreatedAt { get; set; }

        public required DateTime LastUpdate { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as TelemetryDTO);
        }

        public bool Equals(TelemetryDTO? other)
        {
            return other is not null &&
                ID == other.ID &&
                BeaconID == other.BeaconID &&
                BeaconName == other.BeaconName &&
                AddressID == other.AddressID &&
                TrainID == other.TrainID &&
                BrakePipePressure == other.BrakePipePressure &&
                Moving == other.Moving &&
                Source == other.Source &&
                Discarded == other.Discarded &&
                DiscardReason == other.DiscardReason &&
                CreatedAt == other.CreatedAt &&
                LastUpdate == other.LastUpdate;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(ID);
            hash.Add(BeaconID);
            hash.Add(BeaconName);
            hash.Add(AddressID);
            hash.Add(TrainID);
            hash.Add(BrakePipePressure);
            hash.Add(Moving);
            hash.Add(Source);
            hash.Add(Discarded);
            hash.Add(DiscardReason);
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            return hash.ToHashCode();
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
