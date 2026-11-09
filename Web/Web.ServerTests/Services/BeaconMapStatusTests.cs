using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Services;

namespace Web.ServerTests.Services;

[TestClass]
public class BeaconMapStatusTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void NoSavedState_UsesCurrentEvaluation()
    {
        var beacon = CreateBeacon(lastCheckIn: Now.AddMinutes(-2));

        var status = BeaconMapStatus.Resolve(beacon, diagnostics: null, state: null, Now);

        Assert.AreEqual(ServerStatus.Online, status.Status);
        Assert.IsTrue(status.IsOnline);
        Assert.AreEqual(0, status.Reasons.Count);
    }

    [TestMethod]
    public void NoSavedState_MissingProgram_IsDegradedWithReasons()
    {
        var beacon = CreateBeacon(lastCheckIn: Now.AddMinutes(-2), expectedSdrSharp: 1);

        var status = BeaconMapStatus.Resolve(beacon, CreateDiagnostics(beacon.ID), state: null, Now);

        Assert.AreEqual(ServerStatus.Degraded, status.Status);
        Assert.IsFalse(status.IsOnline);
        Assert.IsTrue(status.Reasons.Any(r => r.Contains("SDR#")), string.Join("; ", status.Reasons));
    }

    [TestMethod]
    public void NeverCheckedIn_IsOffline()
    {
        var beacon = CreateBeacon(lastCheckIn: null);

        var status = BeaconMapStatus.Resolve(beacon, diagnostics: null, state: null, Now);

        Assert.AreEqual(ServerStatus.Offline, status.Status);
        Assert.IsFalse(status.IsOnline);
    }

    [TestMethod]
    public void ConfirmedOnline_StaysOnline_WhileDegradedIsUnconfirmed()
    {
        var beacon = CreateBeacon(lastCheckIn: Now.AddMinutes(-1), expectedSdrSharp: 1);
        var state = new BeaconServerState
        {
            BeaconID = beacon.ID,
            Status = ServerStatus.Online,
            StatusSince = Now.AddHours(-1),
            PendingStatus = ServerStatus.Degraded,
            PendingSince = Now.AddMinutes(-1),
        };

        var status = BeaconMapStatus.Resolve(beacon, CreateDiagnostics(beacon.ID), state, Now);

        Assert.AreEqual(ServerStatus.Online, status.Status, "A brief Degraded spell must not turn the map gray.");
        Assert.AreEqual(0, status.Reasons.Count);
    }

    [TestMethod]
    public void ConfirmedOnline_BecomesDegraded_OnceConfirmWindowPasses()
    {
        var beacon = CreateBeacon(lastCheckIn: Now.AddMinutes(-1), expectedSdrSharp: 1);
        var state = new BeaconServerState
        {
            BeaconID = beacon.ID,
            Status = ServerStatus.Online,
            StatusSince = Now.AddHours(-1),
            PendingStatus = ServerStatus.Degraded,
            PendingSince = Now.AddMinutes(-ServerHealthService.StatusConfirmMinutes - 1),
        };

        var status = BeaconMapStatus.Resolve(beacon, CreateDiagnostics(beacon.ID), state, Now);

        Assert.AreEqual(ServerStatus.Degraded, status.Status);
        Assert.IsTrue(status.Reasons.Count > 0);
    }

    [TestMethod]
    public void ConfirmedOnline_GoesOffline_Immediately()
    {
        var beacon = CreateBeacon(lastCheckIn: Now.AddMinutes(-20));
        var state = new BeaconServerState { BeaconID = beacon.ID, Status = ServerStatus.Online, StatusSince = Now.AddHours(-1) };

        var status = BeaconMapStatus.Resolve(beacon, diagnostics: null, state, Now);

        Assert.AreEqual(ServerStatus.Offline, status.Status);
        Assert.IsTrue(status.Reasons.Any(r => r.StartsWith("No check-in")), string.Join("; ", status.Reasons));
    }

    [TestMethod]
    public void ConfirmedOffline_ComesOnline_Immediately()
    {
        var beacon = CreateBeacon(lastCheckIn: Now.AddMinutes(-1));
        var state = new BeaconServerState { BeaconID = beacon.ID, Status = ServerStatus.Offline, StatusSince = Now.AddHours(-1) };

        var status = BeaconMapStatus.Resolve(beacon, diagnostics: null, state, Now);

        Assert.AreEqual(ServerStatus.Online, status.Status);
    }

    [TestMethod]
    public void ConfirmedDegraded_StaysDegraded_WhileRecoveryIsUnconfirmed_WithoutStaleReasons()
    {
        var beacon = CreateBeacon(lastCheckIn: Now.AddMinutes(-1));
        var state = new BeaconServerState { BeaconID = beacon.ID, Status = ServerStatus.Degraded, StatusSince = Now.AddHours(-1) };

        var status = BeaconMapStatus.Resolve(beacon, diagnostics: null, state, Now);

        Assert.AreEqual(ServerStatus.Degraded, status.Status);
        Assert.AreEqual(0, status.Reasons.Count, "The current reasons describe the recovering status, not Degraded.");
    }

    [TestMethod]
    public void Resolve_DoesNotChangeSavedState()
    {
        var beacon = CreateBeacon(lastCheckIn: Now.AddMinutes(-20));
        var state = new BeaconServerState { BeaconID = beacon.ID, Status = ServerStatus.Online, StatusSince = Now.AddHours(-1) };

        BeaconMapStatus.Resolve(beacon, diagnostics: null, state, Now);

        Assert.AreEqual(ServerStatus.Online, state.Status);
        Assert.IsNull(state.PendingStatus);
        Assert.AreEqual(Now.AddHours(-1), state.StatusSince);
    }

    private static Beacon CreateBeacon(DateTime? lastCheckIn, int? expectedSdrSharp = null)
    {
        var beacon = new Beacon { ID = 7, Name = "Eagle", ExpectedSdrSharpCount = expectedSdrSharp };
        if (lastCheckIn.HasValue)
        {
            beacon.BeaconRailroads.Add(new BeaconRailroad { BeaconID = beacon.ID, SubdivisionID = 1, LastUpdate = lastCheckIn.Value });
        }

        return beacon;
    }

    private static BeaconDiagnostics CreateDiagnostics(int beaconId) => new()
    {
        BeaconID = beaconId,
        HealthPingIntervalMinutes = 1,
        CollectedAt = Now.AddMinutes(-1),
        LastUpdate = Now.AddMinutes(-1),
        Programs = [],
    };
}
