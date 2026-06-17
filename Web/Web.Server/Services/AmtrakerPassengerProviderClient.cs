using System.Text.Json;

namespace Web.Server.Services
{
    public class AmtrakerPassengerProviderClient : IPassengerRailProviderClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AmtrakerPassengerProviderClient> _logger;

        public AmtrakerPassengerProviderClient(HttpClient httpClient, ILogger<AmtrakerPassengerProviderClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public string ProviderName => "Amtrak";

        public async Task<IEnumerable<PassengerProviderTrainSnapshot>> GetTrainsAsync(string trainNumber, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync($"v3/trains/{trainNumber}", cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!TryGetTrainsElement(document.RootElement, trainNumber, out var trainsElement) || trainsElement.GetArrayLength() == 0)
            {
                _logger.LogDebug("No Amtraker train data returned for train number {TrainNumber}.", trainNumber);
                return [];
            }

            var snapshots = new List<PassengerProviderTrainSnapshot>();

            foreach (var trainElement in trainsElement.EnumerateArray())
            {
                if (!trainElement.TryGetProperty("trainID", out _))
                {
                    continue;
                }

                var rawHeading = ReadString(trainElement, "heading") ?? "Unknown";
                var updatedAt = ParseDateTimeOffset(ReadString(trainElement, "updatedAt"))
                    ?? ParseDateTimeOffset(ReadString(trainElement, "lastValTS"))
                    ?? DateTimeOffset.UtcNow;

                snapshots.Add(new PassengerProviderTrainSnapshot
                {
                    Provider = ReadString(trainElement, "provider") ?? ProviderName,
                    RouteName = ReadString(trainElement, "routeName") ?? trainNumber,
                    TrainNum = ReadString(trainElement, "trainNum") ?? trainNumber,
                    TrainId = ReadString(trainElement, "trainID") ?? trainNumber,
                    Heading = NormalizeHeading(rawHeading),
                    Latitude = ReadDouble(trainElement, "lat"),
                    Longitude = ReadDouble(trainElement, "lon"),
                    Velocity = (int)Math.Round(ReadDouble(trainElement, "velocity")),
                    UpdatedAtUtc = updatedAt.UtcDateTime
                });
            }

            return snapshots;
        }

        private bool TryGetTrainsElement(JsonElement rootElement, string trainNumber, out JsonElement trainsElement)
        {
            trainsElement = default;

            switch (rootElement.ValueKind)
            {
                case JsonValueKind.Object:
                    if (!rootElement.TryGetProperty(trainNumber, out var trainBucket) || trainBucket.ValueKind != JsonValueKind.Array)
                    {
                        return false;
                    }

                    trainsElement = trainBucket;
                    return true;

                case JsonValueKind.Array:
                    trainsElement = rootElement;
                    return true;

                default:
                    _logger.LogWarning(
                        "Amtraker response payload had unsupported root type {RootType} for train number {TrainNumber}.",
                        rootElement.ValueKind,
                        trainNumber);
                    return false;
            }
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
                ? property.GetString()
                : null;
        }

        private static double ReadDouble(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.TryGetDouble(out var value)
                ? value
                : 0;
        }

        private static DateTimeOffset? ParseDateTimeOffset(string? value)
        {
            return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
        }

        private static string NormalizeHeading(string heading)
        {
            return heading.Trim().ToUpperInvariant() switch
            {
                "N" => "Northbound",
                "NE" => "Northeastbound",
                "S" => "Southbound",
                "SE" => "Southeastbound",
                "E" => "Eastbound",
                "W" => "Westbound",
                "NW" => "Northwestbound",
                "SW" => "Southwestbound",
                _ => "Unknown"
            };
        }
    }
}