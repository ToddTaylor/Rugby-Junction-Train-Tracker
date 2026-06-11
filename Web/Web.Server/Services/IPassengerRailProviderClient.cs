namespace Web.Server.Services
{
    public interface IPassengerRailProviderClient
    {
        string ProviderName { get; }

        Task<IEnumerable<PassengerProviderTrainSnapshot>> GetTrainsAsync(string trainNumber, CancellationToken cancellationToken);
    }

    public class PassengerProviderTrainSnapshot
    {
        public required string Provider { get; init; }
        public required string RouteName { get; init; }
        public required string TrainNum { get; init; }
        public required string TrainId { get; init; }
        public required string Heading { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public int Velocity { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }
}