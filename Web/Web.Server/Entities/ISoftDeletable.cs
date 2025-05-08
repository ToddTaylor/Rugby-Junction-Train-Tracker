namespace Web.Server.Entities
{
    public interface ISoftDeletable
    {
        bool IsDeleted { get; set; }
    }

}
