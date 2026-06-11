namespace Web.Server.DTOs
{
    public class AmtrakTrackedTrainDTO
    {
        public int ID { get; set; }
        public required string TrainNumber { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    public class CreateAmtrakTrackedTrainDTO
    {
        public required string TrainNumber { get; set; }
    }

    public class AmtrakPollingConfigurationDTO
    {
        public int ID { get; set; }
        public int PollIntervalMinutes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    public class UpdateAmtrakPollingConfigurationDTO
    {
        public int PollIntervalMinutes { get; set; }
    }

    public class PassengerMapPinDTO
    {
        public int ID { get; set; }
        public required string Provider { get; set; }
        public required string RouteName { get; set; }
        public required string TrainNum { get; set; }
        public required string TrainId { get; set; }
        public required string Heading { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Velocity { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsStale { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}