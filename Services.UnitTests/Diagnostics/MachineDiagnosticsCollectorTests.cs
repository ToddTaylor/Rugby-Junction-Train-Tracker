using FluentAssertions;
using Services.Diagnostics;
using Services.Models;

namespace Services.UnitTests.Diagnostics;

[TestClass]
public class MachineDiagnosticsCollectorTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void Collect_CountsRunningAndHungInstancesPerProgram()
    {
        var probe = new FakeSystemProbe();
        probe.Processes["SDRSharp*"] = [true, true];
        probe.Processes["SoftEOT"] = [true];
        probe.Processes["SoftDPU"] = [true, false, true, true];

        var report = CreateCollector(probe).Collect(CreateSettings(), CreateSubscriber("collector-programs"), Now);

        report.Programs.Should().BeEquivalentTo(new[]
        {
            new ProgramStatus { Name = "SDR#", Running = 2, NotResponding = 0 },
            new ProgramStatus { Name = "SoftEOT", Running = 1, NotResponding = 0 },
            new ProgramStatus { Name = "SoftDPU", Running = 4, NotResponding = 1 },
        });
    }

    [TestMethod]
    public void Collect_ReportsNewestWriteTimeForEveryLogPrefix()
    {
        var probe = new FakeSystemProbe();
        probe.LogWrites["eot"] = Now.AddMinutes(-12);
        probe.LogWrites["dpu1"] = Now.AddMinutes(-3);

        var report = CreateCollector(probe).Collect(CreateSettings(), CreateSubscriber("collector-logs"), Now);

        report.Logs.Select(l => l.Prefix).Should().Equal("eot", "hot", "dpu1", "dpu2", "dpu3", "dpu4");
        report.Logs.Single(l => l.Prefix == "eot").LastWriteAt.Should().Be(Now.AddMinutes(-12));
        report.Logs.Single(l => l.Prefix == "hot").LastWriteAt.Should().BeNull();
    }

    [TestMethod]
    public void Collect_ReportsConfiguredStartupTasksAndMachineFacts()
    {
        var probe = new FakeSystemProbe();
        var settings = CreateSettings();
        settings.MonitoredScheduledTasks = ["Start SDR#", "Start SoftEOT"];

        var report = CreateCollector(probe).Collect(settings, CreateSubscriber("collector-tasks"), Now);

        report.StartupTasks.Select(t => t.Name).Should().Equal("Start SDR#", "Start SoftEOT");
        report.Hostname.Should().Be("RJ-SDR01");
        report.HealthPingIntervalMinutes.Should().Be(2);
        report.CollectedAt.Should().Be(Now);
        report.LogDriveFreeBytes.Should().Be(41_000_000_000);
    }

    [TestMethod]
    public void Collect_IncludesTelemetrySendOutcomesForThisSubscriberOnly()
    {
        TelemetrySendTracker.RecordSuccess("collector-sends", Now.AddMinutes(-12));
        TelemetrySendTracker.RecordFailure("collector-sends", Now.AddMinutes(-5));
        TelemetrySendTracker.RecordFailure("collector-sends", Now.AddMinutes(-90)); // outside the 1-hour window
        TelemetrySendTracker.RecordFailure("collector-other-subscriber", Now.AddMinutes(-1));

        var report = CreateCollector(new FakeSystemProbe()).Collect(CreateSettings(), CreateSubscriber("collector-sends"), Now);

        report.LastTelemetrySentAt.Should().Be(Now.AddMinutes(-12));
        report.TelemetrySendFailuresLastHour.Should().Be(1);
    }

    private static MachineDiagnosticsCollector CreateCollector(ISystemProbe probe) => new(probe);

    private static AppSettings CreateSettings() => new() { LogDirectoryPath = @"C:\TrainTracker\Logs" };

    private static Subscriber CreateSubscriber(string id) => new()
    {
        ID = id,
        Beacon = new Beacon { BeaconID = "1", HealthPingIntervalMinutes = 2 },
    };

    private sealed class FakeSystemProbe : ISystemProbe
    {
        public Dictionary<string, bool[]> Processes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DateTime> LogWrites { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? Hostname => "RJ-SDR01";
        public string? OsVersion => "Microsoft Windows 10.0.26100";
        public string? ClientVersion => "2.4.1.0";

        public DateTime? GetLastBootUtc(DateTime utcNow) => utcNow.AddDays(-3);

        public long? GetFreeBytes(string path) => 41_000_000_000;

        public IReadOnlyList<bool> GetProcessRespondingStates(string processName) =>
            Processes.TryGetValue(processName, out var states) ? states : [];

        public DateTime? GetNewestFileWriteUtc(string directory, string fileNamePrefix) =>
            LogWrites.TryGetValue(fileNamePrefix, out var time) ? time : null;

        public ScheduledTaskStatus GetScheduledTask(string taskName) =>
            new() { Name = taskName, Found = true, LastResult = 0, LastRunAt = Now.AddHours(-6) };
    }
}
