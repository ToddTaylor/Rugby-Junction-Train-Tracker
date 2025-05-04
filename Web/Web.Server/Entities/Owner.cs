using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Owner : IEquatable<Owner?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        public required string FirstName { get; set; }

        [Required]
        public required string LastName { get; set; }

        [Required]
        public required string Email { get; set; }

        [Required]
        public required string City { get; set; }

        [Required]
        public required string State { get; set; }

        public required ICollection<Beacon> Beacons { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Owner);
        }

        public bool Equals(Owner? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   FirstName == other.FirstName &&
                   LastName == other.LastName &&
                   Email == other.Email &&
                   City == other.City &&
                   State == other.State &&
                   EqualityComparer<ICollection<Beacon>>.Default.Equals(Beacons, other.Beacons);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, FirstName, LastName, Email, City, State, Beacons);
        }

        public static bool operator ==(Owner? left, Owner? right)
        {
            return EqualityComparer<Owner>.Default.Equals(left, right);
        }

        public static bool operator !=(Owner? left, Owner? right)
        {
            return !(left == right);
        }
    }
}
