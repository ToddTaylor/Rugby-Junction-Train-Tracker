using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Telemetry : EntityBase, IEquatable<Telemetry?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        public Beacon Beacon { get; set; }

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
            return Equals(obj as Telemetry);
        }

        public bool Equals(Telemetry? other)
        {
            return other is not null &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   ID == other.ID &&
                   EqualityComparer<Beacon>.Default.Equals(Beacon, other.Beacon) &&
                   AddressID == other.AddressID &&
                   TrainID == other.TrainID &&
                   Moving == other.Moving &&
                   Source == other.Source;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CreatedAt, LastUpdate, ID, Beacon, AddressID, TrainID, Moving, Source);
        }

        public static bool operator ==(Telemetry? left, Telemetry? right)
        {
            return EqualityComparer<Telemetry>.Default.Equals(left, right);
        }

        public static bool operator !=(Telemetry? left, Telemetry? right)
        {
            return !(left == right);
        }
    }
}
