using FluentAssertions;
using Services.Diagnostics;

namespace Services.UnitTests.Diagnostics;

[TestClass]
public class WindowsSystemProbeTests
{
    [TestMethod]
    public void GetNewestFileWriteUtc_ReturnsNewestMatchingLogOnly()
    {
        var directory = Directory.CreateTempSubdirectory("rjtt-probe-").FullName;
        try
        {
            var older = Path.Combine(directory, "dpu1_20261102.log");
            var newer = Path.Combine(directory, "dpu1_20261103.log");
            var otherPrefix = Path.Combine(directory, "eot_20261103.log");
            File.WriteAllText(older, "");
            File.WriteAllText(newer, "");
            File.WriteAllText(otherPrefix, "");
            File.SetLastWriteTimeUtc(older, new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newer, new DateTime(2026, 11, 3, 9, 30, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(otherPrefix, new DateTime(2026, 11, 3, 11, 0, 0, DateTimeKind.Utc));

            var probe = new WindowsSystemProbe();

            probe.GetNewestFileWriteUtc(directory, "dpu1").Should().Be(new DateTime(2026, 11, 3, 9, 30, 0, DateTimeKind.Utc));
            probe.GetNewestFileWriteUtc(directory, "dpu2").Should().BeNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void GetProcessRespondingStates_TrailingStar_MatchesNamePrefix()
    {
        var currentName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        var probe = new WindowsSystemProbe();

        var exact = probe.GetProcessRespondingStates(currentName);
        var prefixed = probe.GetProcessRespondingStates(currentName[..^1] + "*");

        exact.Should().NotBeEmpty();
        prefixed.Count.Should().BeGreaterThanOrEqualTo(exact.Count);
        probe.GetProcessRespondingStates("NoSuchProcessRjtt*").Should().BeEmpty();
    }

    [TestMethod]
    public void GetWindowRespondingStates_NoMatchingProcessOrTitle_ReturnsEmpty()
    {
        var currentName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        var probe = new WindowsSystemProbe();

        probe.GetWindowRespondingStates("NoSuchProcessRjtt*", "Sharp Slicer").Should().BeEmpty();
        probe.GetWindowRespondingStates(currentName, "No window has this title Rjtt").Should().BeEmpty();
    }

    [TestMethod]
    public void GetNewestFileWriteUtc_MissingDirectory_ReturnsNull()
    {
        new WindowsSystemProbe().GetNewestFileWriteUtc(@"C:\does-not-exist-rjtt", "eot").Should().BeNull();
    }
}
