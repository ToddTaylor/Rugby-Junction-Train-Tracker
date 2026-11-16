using System.Net;
using Polly;
using Polly.Retry;

namespace Services
{
    public static class PollyPolicies
    {
        private const int RETRY_COUNT = 3;

        /// <summary>
        /// Retries a failed request up to three times, after 2, 4 and 8 seconds, but only when trying again could
        /// help. A wrong API key, a missing beacon or a bad request fails the same way every time, so it is not
        /// retried and does not hold up the packets queued behind it.
        /// </summary>
        public static AsyncRetryPolicy GetExponentialBackoffPolicy(string subscriberId)
        {
            return Policy
                .Handle<Exception>(IsTransient)
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

        /// <summary>
        /// True for a failure that may not happen again: no connection, a timeout, a server error (5xx), 408 or
        /// 429. False for every other answer from the server and for errors in this program.
        /// </summary>
        public static bool IsTransient(Exception exception) => exception switch
        {
            // No status code: the request never got an answer (DNS, refused connection, dropped connection).
            HttpRequestException { StatusCode: null } => true,
            HttpRequestException { StatusCode: HttpStatusCode status } => IsTransientStatus(status),
            // HttpClient reports its timeout as a cancellation.
            TaskCanceledException or TimeoutException => true,
            _ => false,
        };

        public static bool IsTransientStatus(HttpStatusCode status) =>
            (int)status >= 500 || status == HttpStatusCode.RequestTimeout || status == HttpStatusCode.TooManyRequests;
    }
}
