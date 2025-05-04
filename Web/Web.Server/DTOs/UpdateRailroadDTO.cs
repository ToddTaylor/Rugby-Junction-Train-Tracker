namespace Web.Server.DTOs
{
    public class UpdateRailroadDTO : CreateRailroadDTO, IEquatable<UpdateRailroadDTO?>
    {
        public required int ID { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as UpdateRailroadDTO);
        }

        public bool Equals(UpdateRailroadDTO? other)
        {
            return other is not null &&
                   Name == other.Name &&
                   Subdivision == other.Subdivision &&
                   ID == other.ID;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Subdivision, ID);
        }

        public static bool operator ==(UpdateRailroadDTO? left, UpdateRailroadDTO? right)
        {
            return EqualityComparer<UpdateRailroadDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(UpdateRailroadDTO? left, UpdateRailroadDTO? right)
        {
            return !(left == right);
        }
    }
}
