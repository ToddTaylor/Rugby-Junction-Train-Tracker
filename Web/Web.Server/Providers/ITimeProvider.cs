namespace Web.Server.Providers
{
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }
}
