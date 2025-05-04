namespace Web.Server.Entities
{
    public class BeaconRailroad
    {
        public int BeaconID { get; set; }
        public Beacon Beacon { get; set; } = null!;
        public int RailroadID { get; set; }
        public Railroad Railroad { get; set; } = null!;
    }
}
