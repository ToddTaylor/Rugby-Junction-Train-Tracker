namespace Web.Server.DTOs
{
    public class UpdateUserDTO : CreateUserDTO, IEquatable<UpdateUserDTO?>
    {
        public int ID { get; set; }

        // Roles property inherited from CreateUserDTO

        public override bool Equals(object? obj)
        {
            return Equals(obj as UpdateUserDTO);
        }
            
        public bool Equals(UpdateUserDTO? other)
        {
            return other is not null &&
                   ID == other.ID &&
                   base.Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, base.GetHashCode());
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
