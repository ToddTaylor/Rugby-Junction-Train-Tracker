using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class Beacon : IEquatable<Beacon?>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        public required Owner Owner { get; set; }

        public required double Latitude { get; set; }

        public required double Longitude { get; set; }

        public required DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public required ICollection<Railroad> Railroads { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Beacon);
        }

        public bool Equals(Beacon? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   EqualityComparer<ICollection<Railroad>>.Default.Equals(Railroads, other.Railroads);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, Latitude, Longitude, Railroads);
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
