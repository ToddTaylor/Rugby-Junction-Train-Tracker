using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Railroad : EntityBase, IEquatable<Railroad?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        public string Name { get; set; }

        public ICollection<Subdivision> Subdivisions { get; set; }

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
                   EqualityComparer<ICollection<Subdivision>>.Default.Equals(Subdivisions, other.Subdivisions);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CreatedAt, LastUpdate, ID, Name, Subdivisions);
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
