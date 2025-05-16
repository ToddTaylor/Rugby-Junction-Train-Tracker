
namespace Web.Server.DTOs
{
    public class BeaconDTO
    {
        public int ID { get; set; }

        public int OwnerID { get; set; }

        public DateTime CreatedAt { get; set; }

        public required ICollection<BeaconRailroadDTO> BeaconRailroads { get; set; }

    }
}
