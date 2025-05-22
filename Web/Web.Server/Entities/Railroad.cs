using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Railroad : EntityBase, IEquatable<Railroad?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        public required string Name { get; set; }

        public required string Subdivision { get; set; }

        public ICollection<BeaconRailroad> BeaconRailroads { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Railroad);
        }

        public bool Equals(Railroad? other)
        {
            return other is not null &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   ID == other.ID &&
                   Name == other.Name &&
                   Subdivision == other.Subdivision &&
                   EqualityComparer<ICollection<BeaconRailroad>>.Default.Equals(BeaconRailroads, other.BeaconRailroads);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CreatedAt, LastUpdate, ID, Name, Subdivision, BeaconRailroads);
        }

        public static bool operator ==(Railroad? left, Railroad? right)
        {
            return EqualityComparer<Railroad>.Default.Equals(left, right);
        }

        public static bool operator !=(Railroad? left, Railroad? right)
        {
            return !(left == right);
        }
    }
}
