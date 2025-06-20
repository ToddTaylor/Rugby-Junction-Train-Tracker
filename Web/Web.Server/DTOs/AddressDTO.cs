
namespace Web.Server.DTOs
{
    public class AddressDTO : IEquatable<AddressDTO?>
    {
        public string Source { get; set; }
        public int AddressID { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as AddressDTO);
        }

        public bool Equals(AddressDTO? other)
        {
            return other is not null &&
                   Source == other.Source &&
                   AddressID == other.AddressID;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Source, AddressID);
        }

        public static bool operator ==(AddressDTO? left, AddressDTO? right)
        {
            return EqualityComparer<AddressDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(AddressDTO? left, AddressDTO? right)
        {
            return !(left == right);
        }
    }
}