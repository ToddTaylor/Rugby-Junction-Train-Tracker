using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class MapPin : EntityBase, IEquatable<MapPin?>, ICloneable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        public int BeaconID { get; set; }

        [Required]
        public int SubdivisionId { get; set; }

        [NotMapped]
        public BeaconRailroad? BeaconRailroad { get; set; }

        public string? Direction { get; set; }

        public bool? Moving { get; set; }

        /// <summary>
        /// Indicates if this map pin represents a local train based on the subdivision's local address ID list.
        /// </summary>
        public bool IsLocal { get; set; } = false;

        public ICollection<Address> Addresses { get; set; } = [];

        public MapPin Clone()
        {
            var clone = new MapPin
            {
                ID = this.ID,
                BeaconID = this.BeaconID,
                SubdivisionId = this.SubdivisionId,
                Direction = this.Direction,
                LastUpdate = this.LastUpdate,
                Moving = this.Moving,
                IsLocal = this.IsLocal,
                CreatedAt = this.CreatedAt,
                // Deep clone the Addresses collection
                Addresses = this.Addresses?.Select(a => new Address
                {
                    ID = a.ID,
                    AddressID = a.AddressID,
                    DpuTrainID = a.DpuTrainID,
                    Source = a.Source,
                    CreatedAt = a.CreatedAt,
                    LastUpdate = a.LastUpdate,
                    MapPinID = a.MapPinID
                }).ToList() ?? [],
                // Note: BeaconRailroad is typically a reference type that shouldn't be deep cloned
                // as it represents shared data. If you need a deep clone, add it here.
                BeaconRailroad = this.BeaconRailroad
            };

            return clone;
        }

        object ICloneable.Clone()
        {
            return this.Clone();
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MapPin);
        }

        public bool Equals(MapPin? other)
        {
            return other is not null &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate &&
                   ID == other.ID &&
                   BeaconID == other.BeaconID &&
                   SubdivisionId == other.SubdivisionId &&
                   EqualityComparer<BeaconRailroad?>.Default.Equals(BeaconRailroad, other.BeaconRailroad) &&
                   Direction == other.Direction &&
                   Moving == other.Moving &&
                   IsLocal == other.IsLocal &&
                   Addresses.SequenceEqual(other.Addresses);    // Custom comparer that works.
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            hash.Add(ID);
            hash.Add(BeaconID);
            hash.Add(SubdivisionId);
            hash.Add(BeaconRailroad);
            hash.Add(Direction);
            hash.Add(IsLocal);
            hash.Add(Moving);
            hash.Add(Addresses);
            return hash.ToHashCode();
        }

        public static bool operator ==(MapPin? left, MapPin? right)
        {
            return EqualityComparer<MapPin>.Default.Equals(left, right);
        }

        public static bool operator !=(MapPin? left, MapPin? right)
        {
            return !(left == right);
        }
    }
}
