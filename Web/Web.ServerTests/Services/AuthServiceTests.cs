using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Web.Server.Entities;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services;

namespace Web.ServerTests.Services;

[TestClass]
public class AuthServiceTests
{
    private class TestTimeProvider : ITimeProvider
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    private readonly TestTimeProvider _time = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IUserRepository> _userRepository = new();

    /// <summary>
    /// The code most recently handed to the email service, which is the only place the plain code exists.
    /// </summary>
    private string? _sentCode;

    // AuthService keeps codes and send history in static dictionaries, so every test uses its own address.
    private readonly string _email = $"user-{Guid.NewGuid():N}@example.com";

    [TestMethod]
    public async Task SendCode_RegisteredUser_SendsTheCode()
    {
        var svc = CreateService(registered: true);

        var (success, errors) = await svc.SendCodeAsync(_email);

        Assert.IsTrue(success);
        Assert.AreEqual(0, errors.Count);
        Assert.AreEqual(AuthService.CodeLength, _sentCode?.Length);
        _emailService.Verify(e => e.SendVerificationCodeAsync(_email, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    [DataRow(false, false, DisplayName = "Address is not registered")]
    [DataRow(true, false, DisplayName = "User is inactive")]
    public async Task SendCode_WithoutAnActiveUser_AnswersLikeARegisteredOne_AndSendsNothing(bool registered, bool active)
    {
        var svc = CreateService(registered, active);

        var (success, errors) = await svc.SendCodeAsync(_email);

        Assert.IsTrue(success);
        Assert.AreEqual(0, errors.Count);
        _emailService.Verify(e => e.SendVerificationCodeAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "Registered address")]
    [DataRow(false, DisplayName = "Address is not registered")]
    public async Task SendCode_RateLimit_IsTheSameForEveryAddress(bool registered)
    {
        var svc = CreateService(registered);
        for (int i = 0; i < AuthService.MaxSendsPerHour; i++)
        {
            var (ok, _) = await svc.SendCodeAsync(_email);
            Assert.IsTrue(ok);
        }

        var (success, errors) = await svc.SendCodeAsync(_email);

        Assert.IsFalse(success);
        Assert.IsTrue(errors.Any(e => e.Contains("Rate limit", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task SendCode_EmailFails_ReportsTheError_AndDoesNotCountAgainstTheLimit()
    {
        var svc = CreateService(registered: true);
        _emailService.Setup(e => e.SendVerificationCodeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, new List<string> { "Mail is down." }));

        for (int i = 0; i <= AuthService.MaxSendsPerHour; i++)
        {
            var (success, errors) = await svc.SendCodeAsync(_email);

            Assert.IsFalse(success);
            CollectionAssert.AreEqual(new[] { "Mail is down." }, errors);
        }
    }

    [TestMethod]
    public async Task VerifyCode_CorrectCode_SignsIn_AndCannotBeUsedTwice()
    {
        var svc = CreateService(registered: true);
        await svc.SendCodeAsync(_email);

        var (success, result, errors) = await svc.VerifyCodeAsync(_email, _sentCode!, false);
        var (secondSuccess, _, secondErrors) = await svc.VerifyCodeAsync(_email, _sentCode!, false);

        Assert.IsTrue(success);
        Assert.AreEqual(0, errors.Count);
        Assert.IsFalse(string.IsNullOrEmpty(result?.Token));
        Assert.AreEqual(_time.UtcNow.Add(AuthService.ShortSessionLifetime), result.ExpiresUtc);
        Assert.IsFalse(secondSuccess);
        CollectionAssert.AreEqual(new[] { AuthService.InvalidCodeError }, secondErrors);
    }

    [TestMethod]
    public async Task VerifyCode_Failures_AllGiveTheSameMessage()
    {
        // "No code was sent" and "wrong code" must read the same, or the message tells who has an account.
        var svc = CreateService(registered: true);

        var notSent = await svc.VerifyCodeAsync(_email, "000000", false);

        await svc.SendCodeAsync(_email);
        var wrong = await svc.VerifyCodeAsync(_email, WrongCode(), false);

        _time.UtcNow = _time.UtcNow.Add(AuthService.CodeLifetime).AddSeconds(1);
        var expired = await svc.VerifyCodeAsync(_email, _sentCode!, false);

        foreach (var (success, result, errors) in new[] { notSent, wrong, expired })
        {
            Assert.IsFalse(success);
            Assert.IsNull(result);
            CollectionAssert.AreEqual(new[] { AuthService.InvalidCodeError }, errors);
        }
    }

    [TestMethod]
    public async Task VerifyCode_AfterTooManyWrongCodes_RefusesTheRightCode()
    {
        var svc = CreateService(registered: true);
        await svc.SendCodeAsync(_email);

        for (int i = 0; i < AuthService.MaxVerifyAttempts; i++)
        {
            await svc.VerifyCodeAsync(_email, WrongCode(), false);
        }

        var (success, result, errors) = await svc.VerifyCodeAsync(_email, _sentCode!, false);

        Assert.IsFalse(success);
        Assert.IsNull(result);
        CollectionAssert.AreEqual(new[] { AuthService.InvalidCodeError }, errors);
    }

    [TestMethod]
    public async Task VerifyCode_OneWrongCodeFewerThanTheLimit_StillAcceptsTheRightCode()
    {
        var svc = CreateService(registered: true);
        await svc.SendCodeAsync(_email);

        for (int i = 0; i < AuthService.MaxVerifyAttempts - 1; i++)
        {
            await svc.VerifyCodeAsync(_email, WrongCode(), false);
        }

        var (success, _, _) = await svc.VerifyCodeAsync(_email, _sentCode!, false);

        Assert.IsTrue(success);
    }

    [TestMethod]
    public async Task VerifyCode_NewCodeAfterTooManyWrongCodes_Works()
    {
        var svc = CreateService(registered: true);
        await svc.SendCodeAsync(_email);
        for (int i = 0; i < AuthService.MaxVerifyAttempts; i++)
        {
            await svc.VerifyCodeAsync(_email, WrongCode(), false);
        }

        await svc.SendCodeAsync(_email);
        var (success, _, _) = await svc.VerifyCodeAsync(_email, _sentCode!, false);

        Assert.IsTrue(success);
    }

    [TestMethod]
    public async Task VerifyCode_UserDeactivatedAfterTheCodeWasSent_IsRefused()
    {
        var svc = CreateService(registered: true);
        await svc.SendCodeAsync(_email);
        _userRepository.Setup(r => r.GetByEmailAsync(_email)).ReturnsAsync(new User { ID = 7, Email = _email, IsActive = false });

        var (success, _, errors) = await svc.VerifyCodeAsync(_email, _sentCode!, false);

        Assert.IsFalse(success);
        CollectionAssert.AreEqual(new[] { AuthService.InvalidCodeError }, errors);
    }

    /// <summary>
    /// A six digit code that is not the one that was sent.
    /// </summary>
    private string WrongCode() => _sentCode == "000000" ? "111111" : "000000";

    private AuthService CreateService(bool registered, bool active = true)
    {
        if (registered)
        {
            var user = new User { ID = 7, Email = _email, IsActive = active, UserRoles = [] };
            _userRepository.Setup(r => r.GetByEmailAsync(_email)).ReturnsAsync(user);
            _userRepository.Setup(r => r.UpdateAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
        }

        _emailService.Setup(e => e.SendVerificationCodeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback((string _, string code) => _sentCode = code)
            .ReturnsAsync((true, new List<string>()));

        var options = new DbContextOptionsBuilder<Server.Data.TelemetryDbContext>()
            .UseInMemoryDatabase(databaseName: "AuthServiceTests_" + Guid.NewGuid())
            .Options;

        return new AuthService(_time, new NullLogger<AuthService>(), _userRepository.Object, new Server.Data.TelemetryDbContext(options), _emailService.Object);
    }
}
