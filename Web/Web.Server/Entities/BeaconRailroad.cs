namespace Web.Server.Entities
{
    public class BeaconRailroad
    {
        public int BeaconID { get; set; }
        public Beacon Beacon { get; set; }
        public int RailroadID { get; set; }
        public Railroad Railroad { get; set; }
        public required double Latitude { get; set; }
        public required double Longitude { get; set; }
    }
}
