

namespace Web.Server.DTOs
{
    public class UpdateSubdivisionDTO : CreateSubdivisionDTO, IEquatable<UpdateSubdivisionDTO?>
    {
        public required int ID { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as UpdateSubdivisionDTO);
        }

        public bool Equals(UpdateSubdivisionDTO? other)
        {
            return other is not null &&
                   base.Equals(other) &&
                   RailroadID == other.RailroadID &&
                   DpuCapable == other.DpuCapable &&
                   Name == other.Name &&
                   ID == other.ID;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), RailroadID, DpuCapable, Name, ID);
        }

        public static bool operator ==(UpdateSubdivisionDTO? left, UpdateSubdivisionDTO? right)
        {
            return EqualityComparer<UpdateSubdivisionDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(UpdateSubdivisionDTO? left, UpdateSubdivisionDTO? right)
        {
            return !(left == right);
        }
    }
}
