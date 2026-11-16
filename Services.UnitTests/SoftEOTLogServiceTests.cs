namespace Services.UnitTests;

[TestClass]
public class SoftEOTLogServiceTests
{
    private sealed class Recorder
    {
        public List<string> Packets { get; } = new();

        public void OnPacket(object? sender, string packet) => Packets.Add(packet);
    }

    private sealed class Faulty
    {
        public void OnPacket(object? sender, string packet) => throw new FormatException("TRID is not a number");
    }

    [TestMethod]
    public void AHandlerThatThrows_DoesNotHideThePacketFromTheHandlersAfterIt()
    {
        var before = new Recorder();
        var after = new Recorder();
        var log = new List<string>();
        EventHandler<string>? handlers = null;
        handlers += before.OnPacket;
        handlers += new Faulty().OnPacket;
        handlers += after.OnPacket;

        SoftEOTLogService.InvokeEachHandler(handlers, "packet-1", log.Add);

        CollectionAssert.AreEqual(new[] { "packet-1" }, before.Packets);
        CollectionAssert.AreEqual(new[] { "packet-1" }, after.Packets);
        Assert.AreEqual(1, log.Count);
        StringAssert.Contains(log[0], nameof(Faulty));
        StringAssert.Contains(log[0], "TRID is not a number");
    }

    [TestMethod]
    public void NoHandlers_DoesNothing()
    {
        SoftEOTLogService.InvokeEachHandler<string>(null, "packet-1", _ => Assert.Fail("Nothing should be logged."));
    }
}
