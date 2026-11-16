using System.Net;

namespace Services.UnitTests;

/// <summary>
/// Only failures that may not happen again are retried. A refusal is answered the same way every time, and
/// retrying it held up every packet queued behind it for 14 seconds or more.
/// </summary>
[TestClass]
public class PollyPoliciesTests
{
    [TestMethod]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.BadGateway)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    [DataRow(HttpStatusCode.GatewayTimeout)]
    [DataRow(HttpStatusCode.RequestTimeout)]
    [DataRow(HttpStatusCode.TooManyRequests)]
    public void ServerErrorsAndThrottling_AreRetried(HttpStatusCode status)
    {
        Assert.IsTrue(PollyPolicies.IsTransient(new HttpRequestException("API error", null, status)));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.BadRequest)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.NotFound)]
    [DataRow(HttpStatusCode.Conflict)]
    [DataRow(HttpStatusCode.UnprocessableEntity)]
    public void Refusals_AreNotRetried(HttpStatusCode status)
    {
        Assert.IsFalse(PollyPolicies.IsTransient(new HttpRequestException("API error", null, status)));
    }

    [TestMethod]
    public void NoAnswerAndTimeouts_AreRetried()
    {
        // HttpClient throws HttpRequestException without a status code when it cannot connect,
        // and TaskCanceledException when its timeout passes.
        Assert.IsTrue(PollyPolicies.IsTransient(new HttpRequestException("No such host is known.")));
        Assert.IsTrue(PollyPolicies.IsTransient(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.")));
        Assert.IsTrue(PollyPolicies.IsTransient(new TimeoutException()));
    }

    [TestMethod]
    public void ProgramErrors_AreNotRetried()
    {
        Assert.IsFalse(PollyPolicies.IsTransient(new InvalidOperationException("API base URL is not configured.")));
        Assert.IsFalse(PollyPolicies.IsTransient(new ArgumentNullException("apiKey")));
    }

    [TestMethod]
    public async Task Policy_DoesNotRetryARefusal()
    {
        var attempts = 0;
        var policy = PollyPolicies.GetExponentialBackoffPolicy("Test");

        await Assert.ThrowsExceptionAsync<HttpRequestException>(() => policy.ExecuteAsync(() =>
        {
            attempts++;
            throw new HttpRequestException("API error: Unauthorized", null, HttpStatusCode.Unauthorized);
        }));

        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public async Task Policy_RetriesAServerError()
    {
        var attempts = 0;
        var policy = PollyPolicies.GetExponentialBackoffPolicy("Test");

        await policy.ExecuteAsync(() =>
        {
            attempts++;
            return attempts == 1
                ? throw new HttpRequestException("API error: ServiceUnavailable", null, HttpStatusCode.ServiceUnavailable)
                : Task.CompletedTask;
        });

        Assert.AreEqual(2, attempts);
    }
}
