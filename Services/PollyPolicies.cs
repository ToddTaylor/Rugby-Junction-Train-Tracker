using Polly;
using Polly.Retry;

namespace Services
{
    public static class PollyPolicies
    {
        private const int RETRY_COUNT = 3;

        public static AsyncRetryPolicy GetExponentialBackoffPolicy(string subscriberId)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    RETRY_COUNT,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        var timestamp = DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                        var requestUrl = context != null && context.TryGetValue("RequestUrl", out var urlObj)
                            ? urlObj?.ToString()
                            : null;
                        var urlPart = string.IsNullOrWhiteSpace(requestUrl)
                            ? string.Empty
                            : $" | URL: {requestUrl}";

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"{timestamp} [{subscriberId}] Retry {retryCount} after {timeSpan.TotalSeconds}s due to: {exception.Message}{urlPart}");
                        Console.ResetColor();
                    });
        }
    }
}