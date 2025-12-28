using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services;
using System.Linq;

namespace Web.ServerTests.Services;

[TestClass]
public class AuthServiceTests
{
    private class TestTimeProvider : ITimeProvider
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    private AuthService CreateService(TestTimeProvider timeProvider)
    {
        var userRepositoryMock = new Mock<IUserRepository>();
        return new AuthService(timeProvider, new NullLogger<AuthService>(), userRepositoryMock.Object);
    }

    [TestMethod]
    public async Task SendCode_Succeeds()
    {
        var tp = new TestTimeProvider();
        var svc = CreateService(tp);
        var (success, errors) = await svc.SendCodeAsync("user@example.com");
        Assert.IsTrue(success);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public async Task RateLimit_Exceeded()
    {
        var tp = new TestTimeProvider();
        var svc = CreateService(tp);
        for (int i = 0; i < AuthService.MaxSendsPerHour; i++)
        {
            var (ok, errs) = await svc.SendCodeAsync("rl@example.com");
            Assert.IsTrue(ok);
        }
        var (success, errors) = await svc.SendCodeAsync("rl@example.com");
        Assert.IsFalse(success);
        Assert.IsTrue(errors.Any(e => e.Contains("Rate limit", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task VerifyCode_Fails_When_Not_Sent()
    {
        var tp = new TestTimeProvider();
        var svc = CreateService(tp);
        var (success, result, errors) = await svc.VerifyCodeAsync("na@example.com", "000000", false);
        Assert.IsFalse(success);
        Assert.IsNull(result);
        Assert.IsTrue(errors.Any(e => e.Contains("Code not found", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task VerifyCode_Fails_When_Expired()
    {
        var tp = new TestTimeProvider();
        var svc = CreateService(tp);
        var (sent, _) = await svc.SendCodeAsync("exp@example.com");
    Assert.IsTrue(sent);
        // Advance time beyond expiration
        tp.UtcNow = tp.UtcNow.Add(AuthService.CodeLifetime).AddSeconds(1);
        var (success, result, errors) = await svc.VerifyCodeAsync("exp@example.com", "000000", false);
        Assert.IsFalse(success);
        Assert.IsTrue(errors.Any(e => e.Contains("expired", StringComparison.OrdinalIgnoreCase)));
    }
}
