using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Subdivision : EntityBase, IEquatable<Subdivision?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        public int RailroadID { get; set; }

        public Railroad Railroad { get; set; }

        [Required]
        public bool DpuCapable { get; set; } = false;

        [Required]
        public string Name { get; set; }

        public ICollection<BeaconRailroad> BeaconRailroads { get; set; } = [];

        public override bool Equals(object? obj)
        {
            return Equals(obj as Subdivision);
        }

        public bool Equals(Subdivision? other)
        {
            return other is not null &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   ID == other.ID &&
                   RailroadID == other.RailroadID &&
                   EqualityComparer<Railroad>.Default.Equals(Railroad, other.Railroad) &&
                   DpuCapable == other.DpuCapable &&
                   Name == other.Name &&
                   BeaconRailroads.SequenceEqual(other.BeaconRailroads); // Custom comparer that works.
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CreatedAt, LastUpdate, ID, RailroadID, Railroad, DpuCapable, Name, BeaconRailroads);
        }

        public static bool operator ==(Subdivision? left, Subdivision? right)
        {
            return EqualityComparer<Subdivision>.Default.Equals(left, right);
        }

        public static bool operator !=(Subdivision? left, Subdivision? right)
        {
            return !(left == right);
        }
    }
}
