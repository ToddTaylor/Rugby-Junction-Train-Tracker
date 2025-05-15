using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Beacon : EntityBase, IEquatable<Beacon?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        public int OwnerID { get; set; }

        public Owner Owner { get; set; }

        public ICollection<BeaconRailroad> BeaconRailroads { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Beacon);
        }

        public bool Equals(Beacon? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   OwnerID == other.OwnerID &&
                   EqualityComparer<Owner>.Default.Equals(Owner, other.Owner) &&
                   CreatedAt == other.CreatedAt &&
                   EqualityComparer<ICollection<BeaconRailroad>>.Default.Equals(BeaconRailroads, other.BeaconRailroads);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, OwnerID, Owner, CreatedAt, BeaconRailroads);
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
