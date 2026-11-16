namespace Services.UnitTests;

[TestClass]
public class TelemetryThrottleServiceTests
{
    private DateTime _now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    private TelemetryThrottleService CreateService(int intervalSeconds = 60) => new(intervalSeconds, () => _now);

    [TestMethod]
    public void FirstMessageForAnAddress_IsSent()
    {
        var throttle = CreateService();

        Assert.IsTrue(throttle.ShouldSendMessage(6720));
        Assert.AreEqual(1, throttle.TrackedAddressCount);
    }

    [TestMethod]
    public void SecondMessageWithinTheInterval_IsThrottled()
    {
        var throttle = CreateService();
        throttle.ShouldSendMessage(6720);

        _now = _now.AddSeconds(59);

        Assert.IsFalse(throttle.ShouldSendMessage(6720));
    }

    [TestMethod]
    public void MessageOnceTheIntervalHasPassed_IsSent()
    {
        var throttle = CreateService();
        throttle.ShouldSendMessage(6720);

        _now = _now.AddSeconds(60);

        Assert.IsTrue(throttle.ShouldSendMessage(6720));
    }

    [TestMethod]
    public void ThrottledMessage_DoesNotRestartTheInterval()
    {
        var throttle = CreateService();
        throttle.ShouldSendMessage(6720);

        _now = _now.AddSeconds(30);
        throttle.ShouldSendMessage(6720);
        _now = _now.AddSeconds(30);

        Assert.IsTrue(throttle.ShouldSendMessage(6720));
    }

    [TestMethod]
    public void EachAddress_IsThrottledOnItsOwn()
    {
        var throttle = CreateService();
        throttle.ShouldSendMessage(6720);

        Assert.IsTrue(throttle.ShouldSendMessage(57135));
        Assert.IsFalse(throttle.ShouldSendMessage(6720));
    }

    [TestMethod]
    public void ZeroInterval_SendsEveryMessage()
    {
        var throttle = CreateService(intervalSeconds: 0);

        Assert.IsTrue(throttle.ShouldSendMessage(6720));
        Assert.IsTrue(throttle.ShouldSendMessage(6720));
    }

    [TestMethod]
    public void Clear_ForgetsEveryAddress()
    {
        var throttle = CreateService();
        throttle.ShouldSendMessage(6720);

        throttle.Clear();

        Assert.AreEqual(0, throttle.TrackedAddressCount);
        Assert.IsTrue(throttle.ShouldSendMessage(6720));
    }
}
