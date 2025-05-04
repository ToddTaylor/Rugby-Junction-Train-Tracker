using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Railroad : IEquatable<Railroad?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        public required string Name { get; set; }

        public required string Subdivision { get; set; }

        public List<Beacon> Beacons { get; set; } = new List<Beacon>();

        public override bool Equals(object? obj)
        {
            return Equals(obj as Railroad);
        }

        public bool Equals(Railroad? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   Name == other.Name &&
                   Subdivision == other.Subdivision;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, Name, Subdivision);
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
