using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Address : EntityBase, IEquatable<Address?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        public int AddressID { get; set; }

        public int MapPinID { get; set; }

        public MapPin MapPin { get; set; }

        /// <summary>
        /// The source of the alert.
        /// HOT, EOT, DPU, HBD
        /// </summary>
        [Required]
        public string Source { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Address);
        }

        public bool Equals(Address? other)
        {
            return other is not null &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   ID == other.ID &&
                   AddressID == other.AddressID &&
                   MapPinID == other.MapPinID &&
                   EqualityComparer<MapPin>.Default.Equals(MapPin, other.MapPin) &&
                   Source == other.Source;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CreatedAt, LastUpdate, ID, AddressID, MapPinID, MapPin, Source);
        }

        public static bool operator ==(Address? left, Address? right)
        {
            return EqualityComparer<Address>.Default.Equals(left, right);
        }

        public static bool operator !=(Address? left, Address? right)
        {
            return !(left == right);
        }
    }
}
