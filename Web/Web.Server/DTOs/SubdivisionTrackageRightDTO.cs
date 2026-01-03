namespace Web.Server.DTOs
{
    public class SubdivisionTrackageRightDTO
    {
        public int ID { get; set; }
        public int FromSubdivisionID { get; set; }
        public int ToSubdivisionID { get; set; }
        public string? FromSubdivisionName { get; set; }
        public string? ToSubdivisionName { get; set; }
        public string? ToRailroadName { get; set; }
    }

    public class CreateSubdivisionTrackageRightDTO
    {
        public int FromSubdivisionID { get; set; }
        public int ToSubdivisionID { get; set; }
    }
}
