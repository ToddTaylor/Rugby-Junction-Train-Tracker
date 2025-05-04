namespace Web.Server.DTOs
{
    public class UpdateOwnerDTO : CreateOwnerDTO, IEquatable<UpdateOwnerDTO?>
    {
        public int ID { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as UpdateOwnerDTO);
        }

        public bool Equals(UpdateOwnerDTO? other)
        {
            return other is not null &&
                   base.Equals(other) &&
                   FirstName == other.FirstName &&
                   LastName == other.LastName &&
                   Email == other.Email &&
                   City == other.City &&
                   State == other.State &&
                   EqualityComparer<ICollection<BeaconDTO>>.Default.Equals(Beacons, other.Beacons) &&
                   ID == other.ID;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), FirstName, LastName, Email, City, State, Beacons, ID);
        }

        public static bool operator ==(UpdateOwnerDTO? left, UpdateOwnerDTO? right)
        {
            return EqualityComparer<UpdateOwnerDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(UpdateOwnerDTO? left, UpdateOwnerDTO? right)
        {
            return !(left == right);
        }
    }
}
