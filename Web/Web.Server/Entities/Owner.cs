using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Owner : EntityBase, IEquatable<Owner?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public string Email { get; set; }

        [Required]
        public string City { get; set; }

        [Required]
        public string State { get; set; }

        public ICollection<Beacon> Beacons { get; set; } = [];

        public override bool Equals(object? obj)
        {
            return Equals(obj as Owner);
        }

        public bool Equals(Owner? other)
        {
            return other is not null &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
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
            HashCode hash = new HashCode();
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            hash.Add(ID);
            hash.Add(FirstName);
            hash.Add(LastName);
            hash.Add(Email);
            hash.Add(City);
            hash.Add(State);
            hash.Add(Beacons);
            return hash.ToHashCode();
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
