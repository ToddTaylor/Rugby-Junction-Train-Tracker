using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Beacon : EntityBase, IEquatable<Beacon?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        public int OwnerID { get; set; }

        public User Owner { get; set; }

        [Required]
        public string Name { get; set; }

        public ICollection<BeaconRailroad> BeaconRailroads { get; set; } = [];

        public ICollection<Telemetry> Telemetries { get; set; } = [];

        public override bool Equals(object? obj)
        {
            return Equals(obj as Beacon);
        }

        public bool Equals(Beacon? other)
        {
            return other is not null &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   ID == other.ID &&
                   OwnerID == other.OwnerID &&
                   Name == other.Name &&
                   EqualityComparer<User>.Default.Equals(Owner, other.Owner) &&
                   EqualityComparer<ICollection<BeaconRailroad>>.Default.Equals(BeaconRailroads, other.BeaconRailroads) &&
                   EqualityComparer<ICollection<Telemetry>>.Default.Equals(Telemetries, other.Telemetries);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CreatedAt, LastUpdate, ID, OwnerID, Owner, Name, BeaconRailroads, Telemetries);
        }

        public static bool operator ==(Beacon? left, Beacon? right)
        {
            return EqualityComparer<Beacon>.Default.Equals(left, right);
        }

        public static bool operator !=(Beacon? left, Beacon? right)
        {
            return !(left == right);
        }
    }
}
