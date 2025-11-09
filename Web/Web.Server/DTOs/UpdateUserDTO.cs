namespace Web.Server.DTOs
{
    public class UpdateUserDTO : CreateUserDTO, IEquatable<UpdateUserDTO?>
    {
        public int ID { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as UpdateUserDTO);
        }

        public bool Equals(UpdateUserDTO? other)
        {
            return other is not null &&
                   base.Equals(other) &&
                   FirstName == other.FirstName &&
                   LastName == other.LastName &&
                   Email == other.Email &&
                   City == other.City &&
                   State == other.State &&
                   ID == other.ID;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), FirstName, LastName, Email, City, State, ID);
        }

        public static bool operator ==(UpdateUserDTO? left, UpdateUserDTO? right)
        {
            return EqualityComparer<UpdateUserDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(UpdateUserDTO? left, UpdateUserDTO? right)
        {
            return !(left == right);
        }
    }
}
