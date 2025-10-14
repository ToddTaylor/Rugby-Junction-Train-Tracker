using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Telemetry : EntityBase, IEquatable<Telemetry?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        public int BeaconID { get; set; }

        public Beacon Beacon { get; set; }

        [Required]
        public int AddressID { get; set; }

        public int? TrainID { get; set; }

        public int? MapPinID { get; set; }

        public MapPin? MapPin { get; set; }

        public bool? Moving { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU, HBD
        /// </summary>
        [Required]
        public string Source { get; set; }

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
                   BeaconID == other.BeaconID &&
                   EqualityComparer<Beacon>.Default.Equals(Beacon, other.Beacon) &&
                   AddressID == other.AddressID &&
                   TrainID == other.TrainID &&
                   MapPinID == other.MapPinID &&
                   EqualityComparer<MapPin?>.Default.Equals(MapPin, other.MapPin) &&
                   Moving == other.Moving &&
                   Source == other.Source;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            hash.Add(ID);
            hash.Add(BeaconID);
            hash.Add(Beacon);
            hash.Add(AddressID);
            hash.Add(TrainID);
            hash.Add(MapPinID);
            hash.Add(MapPin);
            hash.Add(Moving);
            hash.Add(Source);
            return hash.ToHashCode();
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
