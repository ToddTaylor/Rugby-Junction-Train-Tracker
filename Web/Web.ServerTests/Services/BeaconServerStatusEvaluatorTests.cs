using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Services;

namespace Web.ServerTests.Services;

[TestClass]
public class BeaconServerStatusEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    // ── Offline ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void NeverCheckedIn_IsOffline()
    {
        var result = BeaconServerStatusEvaluator.Evaluate(new Beacon(), null, null, Now);

        Assert.AreEqual(ServerStatus.Offline, result.Status);
        CollectionAssert.AreEqual(new[] { "Never checked in" }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void NoCheckInWithinThreshold_IsOffline_WithDuration()
    {
        var diagnostics = Healthy(receivedAt: Now.AddMinutes(-47));

        var result = BeaconServerStatusEvaluator.Evaluate(Expecting(), diagnostics, Now.AddMinutes(-47), Now);

        Assert.AreEqual(ServerStatus.Offline, result.Status);
        CollectionAssert.AreEqual(new[] { "No check-in for 47 min" }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void OfflineThreshold_IsThreeMissedChecks_BetweenFiveAndFifteenMinutes()
    {
        Assert.AreEqual(6, BeaconServerStatusEvaluator.GetOfflineThresholdMinutes(2));
        Assert.AreEqual(5, BeaconServerStatusEvaluator.GetOfflineThresholdMinutes(1));
        Assert.AreEqual(15, BeaconServerStatusEvaluator.GetOfflineThresholdMinutes(15));
        Assert.AreEqual(15, BeaconServerStatusEvaluator.GetOfflineThresholdMinutes(null));
        Assert.AreEqual(15, BeaconServerStatusEvaluator.GetOfflineThresholdMinutes(0));
    }

    [TestMethod]
    public void TwoMinutePings_SevenMinutesQuiet_IsOffline()
    {
        var diagnostics = Healthy(receivedAt: Now.AddMinutes(-7));

        var result = BeaconServerStatusEvaluator.Evaluate(Expecting(), diagnostics, Now.AddMinutes(-7), Now);

        Assert.AreEqual(ServerStatus.Offline, result.Status);
        Assert.AreEqual(6, result.OfflineThresholdMinutes);
    }

    // ── Legacy clients ──────────────────────────────────────────────────────

    [TestMethod]
    public void CheckingInWithoutDiagnostics_IsOnline_AndDoesNotUseDiagnostics()
    {
        var result = BeaconServerStatusEvaluator.Evaluate(Expecting(), null, Now.AddMinutes(-3), Now);

        Assert.AreEqual(ServerStatus.Online, result.Status);
        Assert.AreEqual(0, result.Reasons.Count);
        Assert.IsFalse(result.UsesDiagnostics);
    }

    [TestMethod]
    public void OldReport_WhileStillCheckingIn_IsIgnored()
    {
        // The PC went back to a ConsoleApp that sends empty health checks; its last report is two hours old.
        var diagnostics = Healthy(receivedAt: Now.AddHours(-2));
        diagnostics.Programs.Single(p => p.Name == "SoftEOT").Running = 0;

        var result = BeaconServerStatusEvaluator.Evaluate(Expecting(), diagnostics, Now.AddMinutes(-3), Now);

        Assert.AreEqual(ServerStatus.Online, result.Status);
        Assert.IsFalse(result.UsesDiagnostics);
    }

    // ── Programs ────────────────────────────────────────────────────────────

    [TestMethod]
    public void AllExpectedProgramsRunning_IsOnline()
    {
        var result = Evaluate(Expecting(), Healthy());

        Assert.AreEqual(ServerStatus.Online, result.Status);
        Assert.AreEqual(0, result.Reasons.Count);
        Assert.IsTrue(result.UsesDiagnostics);
    }

    [TestMethod]
    public void FewerInstancesThanExpected_IsDegraded()
    {
        var diagnostics = Healthy();
        diagnostics.Programs.Single(p => p.Name == "SoftEOT").Running = 1;

        var result = Evaluate(Expecting(softEot: 2), diagnostics);

        Assert.AreEqual(ServerStatus.Degraded, result.Status);
        CollectionAssert.Contains(result.Reasons.ToList(), "Only 1 of 2 SoftEOT instances running");
    }

    [TestMethod]
    public void ExpectedProgramNotRunning_IsDegraded()
    {
        var diagnostics = Healthy();
        diagnostics.Programs.Single(p => p.Name == "SDR#").Running = 0;

        var result = Evaluate(Expecting(), diagnostics);

        CollectionAssert.AreEqual(new[] { "SDR# is not running" }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void ExpectedProgramMissingFromReport_IsReportedNotRunning()
    {
        var diagnostics = Healthy();
        diagnostics.Programs.RemoveAll(p => p.Name == "SoftDPU");

        var result = Evaluate(Expecting(), diagnostics);

        CollectionAssert.AreEqual(new[] { "SoftDPU is not running" }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void ExpectedCountNotSet_SkipsCountCheck()
    {
        var diagnostics = Healthy();
        diagnostics.Programs.Single(p => p.Name == "SoftDPU").Running = 0;
        var beacon = Expecting();
        beacon.ExpectedSoftDpuCount = null;

        var result = Evaluate(beacon, diagnostics);

        Assert.AreEqual(ServerStatus.Online, result.Status);
    }

    [TestMethod]
    public void HungInstance_IsDegraded_EvenWithoutExpectedCount()
    {
        var diagnostics = Healthy();
        diagnostics.Programs.Single(p => p.Name == "SoftDPU").NotResponding = 1;
        var beacon = Expecting();
        beacon.ExpectedSoftDpuCount = null;

        var result = Evaluate(beacon, diagnostics);

        CollectionAssert.AreEqual(new[] { "SoftDPU is not responding" }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void SeveralHungInstances_ReportsCount()
    {
        var diagnostics = Healthy();
        diagnostics.Programs.Single(p => p.Name == "SoftDPU").NotResponding = 2;

        var result = Evaluate(Expecting(), diagnostics);

        CollectionAssert.AreEqual(new[] { "2 SoftDPU instances are not responding" }, result.Reasons.ToArray());
    }

    // ── Logs ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void QuietLogs_DoNotMarkServerDegraded()
    {
        // SoftDPU only writes when it decodes a train, so a healthy instance on a quiet line leaves its log alone for hours.
        var diagnostics = Healthy();
        diagnostics.Logs.Single(l => l.Prefix == "dpu1").LastWriteAt = Now.AddHours(-14);
        diagnostics.Logs.Single(l => l.Prefix == "hot").LastWriteAt = Now.AddHours(-35);

        var result = Evaluate(Expecting(), diagnostics);

        Assert.AreEqual(ServerStatus.Online, result.Status);
    }

    // ── Startup tasks, sends and disk ──────────────────────────────────────

    [TestMethod]
    public void FailedStartupTask_IsDegraded()
    {
        var diagnostics = Healthy();
        diagnostics.StartupTasks.Add(new DiagnosticsStartupTaskStatus { Name = "Start SDR#", Found = true, LastResult = 1, LastRunAt = Now.AddHours(-6) });

        var result = Evaluate(Expecting(), diagnostics);

        CollectionAssert.AreEqual(new[] { "Startup task 'Start SDR#' failed (0x1)" }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void MissingStartupTask_IsDegraded()
    {
        var diagnostics = Healthy();
        diagnostics.StartupTasks.Add(new DiagnosticsStartupTaskStatus { Name = "Start SoftEOT", Found = false });

        var result = Evaluate(Expecting(), diagnostics);

        CollectionAssert.AreEqual(new[] { "Startup task 'Start SoftEOT' not found" }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void RunningOrNeverRunStartupTask_IsNotAFailure()
    {
        var diagnostics = Healthy();
        diagnostics.StartupTasks.Add(new DiagnosticsStartupTaskStatus { Name = "Watchdog", Found = true, LastResult = 0x41301 });
        diagnostics.StartupTasks.Add(new DiagnosticsStartupTaskStatus { Name = "New task", Found = true, LastResult = 0x41303 });

        var result = Evaluate(Expecting(), diagnostics);

        Assert.AreEqual(ServerStatus.Online, result.Status);
    }

    [TestMethod]
    public void TelemetryFailuresWithoutRecentSuccess_IsDegraded()
    {
        var diagnostics = Healthy();
        diagnostics.TelemetrySendFailuresLastHour = 4;
        diagnostics.LastTelemetrySentAt = Now.AddMinutes(-40);

        var result = Evaluate(Expecting(), diagnostics);

        CollectionAssert.AreEqual(new[] { "Telemetry sends failing (4 in the last hour)" }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void TelemetryFailuresWithRecentSuccess_IsOnline()
    {
        var diagnostics = Healthy();
        diagnostics.TelemetrySendFailuresLastHour = 1;
        diagnostics.LastTelemetrySentAt = Now.AddMinutes(-2);

        var result = Evaluate(Expecting(), diagnostics);

        Assert.AreEqual(ServerStatus.Online, result.Status);
    }

    [TestMethod]
    public void LowDiskSpace_IsDegraded()
    {
        var diagnostics = Healthy();
        diagnostics.LogDriveFreeBytes = 1_288_490_189; // 1.2 GB

        var result = Evaluate(Expecting(), diagnostics);

        CollectionAssert.AreEqual(new[] { "Low disk space (1.2 GB free)" }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void FormatDuration_UsesMinutesHoursOrDays()
    {
        Assert.AreEqual("1 min", BeaconServerStatusEvaluator.FormatDuration(TimeSpan.FromSeconds(20)));
        Assert.AreEqual("59 min", BeaconServerStatusEvaluator.FormatDuration(TimeSpan.FromMinutes(59)));
        Assert.AreEqual("5 h", BeaconServerStatusEvaluator.FormatDuration(TimeSpan.FromHours(5.5)));
        Assert.AreEqual("9 days", BeaconServerStatusEvaluator.FormatDuration(TimeSpan.FromDays(9.2)));
    }

    // ── SDR# slices ─────────────────────────────────────────────────────────

    [TestMethod]
    public void AllExpectedSlicesOpen_IsOnline()
    {
        var diagnostics = Healthy();
        diagnostics.Programs.Add(new DiagnosticsProgramStatus { Name = "SDR# slices", Running = 3 });
        var beacon = Expecting();
        beacon.ExpectedSdrSliceCount = 3;

        var result = Evaluate(beacon, diagnostics);

        Assert.AreEqual(ServerStatus.Online, result.Status);
    }

    [TestMethod]
    public void FewerSlicesThanExpected_IsDegraded()
    {
        var diagnostics = Healthy();
        diagnostics.Programs.Add(new DiagnosticsProgramStatus { Name = "SDR# slices", Running = 4 });
        var beacon = Expecting();
        beacon.ExpectedSdrSliceCount = 6;

        var result = Evaluate(beacon, diagnostics);

        Assert.AreEqual(ServerStatus.Degraded, result.Status);
        CollectionAssert.AreEqual(new[] { "Only 4 of 6 SDR# slices open" }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void NoSlicesOpen_IsDegraded()
    {
        var diagnostics = Healthy();
        diagnostics.Programs.Add(new DiagnosticsProgramStatus { Name = "SDR# slices", Running = 0 });
        var beacon = Expecting();
        beacon.ExpectedSdrSliceCount = 6;

        var result = Evaluate(beacon, diagnostics);

        CollectionAssert.AreEqual(new[] { "No SDR# slices are open" }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void SlicesMissingFromOlderReport_AreNotAProblem()
    {
        var beacon = Expecting();
        beacon.ExpectedSdrSliceCount = 6;

        var result = Evaluate(beacon, Healthy());

        Assert.AreEqual(ServerStatus.Online, result.Status);
    }

    [TestMethod]
    public void SliceCountNotSet_SkipsCheck_ButReportsHungSlices()
    {
        var diagnostics = Healthy();
        diagnostics.Programs.Add(new DiagnosticsProgramStatus { Name = "SDR# slices", Running = 0 });

        Assert.AreEqual(ServerStatus.Online, Evaluate(Expecting(), diagnostics).Status);

        diagnostics.Programs.Single(p => p.Name == "SDR# slices").Running = 6;
        diagnostics.Programs.Single(p => p.Name == "SDR# slices").NotResponding = 2;

        CollectionAssert.AreEqual(new[] { "2 SDR# slices are not responding" }, Evaluate(Expecting(), diagnostics).Reasons.ToArray());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static BeaconServerStatusEvaluator.Result Evaluate(Beacon beacon, BeaconDiagnostics diagnostics) =>
        BeaconServerStatusEvaluator.Evaluate(beacon, diagnostics, diagnostics.LastUpdate, Now);

    private static Beacon Expecting(int sdrSharp = 1, int softEot = 1, int softDpu = 2) => new()
    {
        ID = 1,
        Name = "Rugby Junction",
        ExpectedSdrSharpCount = sdrSharp,
        ExpectedSoftEotCount = softEot,
        ExpectedSoftDpuCount = softDpu,
    };

    private static BeaconDiagnostics Healthy(DateTime? receivedAt = null) => new()
    {
        BeaconID = 1,
        LastUpdate = receivedAt ?? Now.AddMinutes(-1),
        CollectedAt = receivedAt ?? Now.AddMinutes(-1),
        HealthPingIntervalMinutes = 2,
        LogDriveFreeBytes = 41_000_000_000,
        LastTelemetrySentAt = Now.AddMinutes(-12),
        Programs =
        [
            new DiagnosticsProgramStatus { Name = "SDR#", Running = 1 },
            new DiagnosticsProgramStatus { Name = "SoftEOT", Running = 1 },
            new DiagnosticsProgramStatus { Name = "SoftDPU", Running = 2 },
        ],
        Logs =
        [
            new DiagnosticsLogFileStatus { Prefix = "eot", LastWriteAt = Now.AddMinutes(-12) },
            new DiagnosticsLogFileStatus { Prefix = "hot", LastWriteAt = Now.AddMinutes(-30) },
            new DiagnosticsLogFileStatus { Prefix = "dpu1", LastWriteAt = Now.AddMinutes(-3) },
            new DiagnosticsLogFileStatus { Prefix = "dpu2", LastWriteAt = Now.AddMinutes(-25) },
        ],
    };
}
