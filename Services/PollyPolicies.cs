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
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[{DateTime.Now}] [{subscriberId}] Retry {retryCount} after {timeSpan.TotalSeconds}s due to: {exception.Message}");
                        Console.ResetColor();
                    });
        }
    }
}