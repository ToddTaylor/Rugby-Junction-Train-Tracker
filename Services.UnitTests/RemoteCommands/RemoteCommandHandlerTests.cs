using FluentAssertions;
using Services.Models;
using Services.RemoteCommands;

namespace Services.UnitTests.RemoteCommands;

[TestClass]
public class RemoteCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private const string RestartAppsPath = @"C:\TrainTracker\AutoIt\Restart_Apps.exe";

    private AppSettings _settings = null!;
    private FakeRunner _runner = null!;
    private InMemoryStateStore _store = null!;
    private List<(int Id, string Status, string? Message)> _reports = null!;
    private RemoteCommandReportResult _serverAnswer;

    [TestInitialize]
    public void Initialize()
    {
        _settings = new AppSettings { AllowRemoteCommands = true, RestartAppsCommand = RestartAppsPath };
        _runner = new FakeRunner { LastBootUtc = Now.AddDays(-2) };
        _runner.Files.Add(RestartAppsPath);
        _store = new InMemoryStateStore();
        _reports = [];
        _serverAnswer = RemoteCommandReportResult.Accepted;
    }

    [TestMethod]
    public async Task Reboot_ReportsStarted_ThenSchedulesTheRestart()
    {
        await HandleAsync(Reboot(12));

        _reports.Should().Equal((12, RemoteCommandReportStatus.Started, (string?)null));
        _runner.Reboots.Should().ContainSingle();
        _runner.Reboots[0].Delay.Should().Be(TimeSpan.FromSeconds(60));
        _runner.Reboots[0].Message.Should().Contain("Ada Admin");
        _store.State.RebootTimes.Should().Equal(Now);
    }

    [TestMethod]
    public async Task SameCommand_IsNeverCarriedOutTwice()
    {
        await HandleAsync(Reboot(12));
        await HandleAsync(Reboot(12));

        _runner.Reboots.Should().ContainSingle();
        _reports.Should().ContainSingle();
    }

    [TestMethod]
    public async Task SameCommandId_FromAnotherServer_IsHandledSeparately()
    {
        await HandleAsync(RestartApps(12), "RugbyJunctionAPI");
        await HandleAsync(RestartApps(12), "RugbyJunctionAPIBeta");

        _runner.Runs.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task RemoteCommandsTurnedOff_IsRejected_AndNothingRuns()
    {
        _settings.AllowRemoteCommands = false;

        await HandleAsync(Reboot(12));

        _runner.Reboots.Should().BeEmpty();
        _reports.Should().ContainSingle().Which.Status.Should().Be(RemoteCommandReportStatus.Rejected);
    }

    [TestMethod]
    public async Task ExpiredCommand_IsRejected()
    {
        var command = Reboot(12);
        command.ExpiresAt = Now.AddSeconds(-1);

        await HandleAsync(command);

        _runner.Reboots.Should().BeEmpty();
        _reports.Single().Message.Should().Contain("expired");
    }

    [TestMethod]
    public async Task Reboot_SoonAfterStarting_IsRejected()
    {
        _runner.LastBootUtc = Now.AddMinutes(-10);

        await HandleAsync(Reboot(12));

        _runner.Reboots.Should().BeEmpty();
        _reports.Single().Message.Should().Contain("started 10 min ago");
    }

    [TestMethod]
    public async Task Reboot_AfterThreeInADay_IsRejected_EvenIfTheServerAsks()
    {
        _store.State.RebootTimes.AddRange([Now.AddHours(-20), Now.AddHours(-10), Now.AddHours(-1)]);

        await HandleAsync(Reboot(12));

        _runner.Reboots.Should().BeEmpty();
        _reports.Single().Status.Should().Be(RemoteCommandReportStatus.Rejected);
    }

    [TestMethod]
    public async Task Reboot_CancelledOnTheServer_IsNotCarriedOut()
    {
        _serverAnswer = RemoteCommandReportResult.Refused;

        await HandleAsync(Reboot(12));

        _runner.Reboots.Should().BeEmpty();
        _store.State.HandledCommands.Should().ContainKey("RugbyJunctionAPI:12");
        _store.State.RebootTimes.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Reboot_WhenTheStartedReportCannotBeSent_IsNotCarriedOut_AndIsTriedAgainLater()
    {
        var failingReport = new Func<int, string, string?, Task<RemoteCommandReportResult>>((_, _, _) => throw new HttpRequestException("offline"));

        await FluentActions.Awaiting(() => CreateHandler().HandleAsync("RugbyJunctionAPI", Reboot(12), failingReport))
            .Should().ThrowAsync<HttpRequestException>();

        _runner.Reboots.Should().BeEmpty();
        _store.State.HandledCommands.Should().BeEmpty();

        await HandleAsync(Reboot(12));
        _runner.Reboots.Should().ContainSingle();
    }

    [TestMethod]
    public async Task RestartApps_ReportsSuccessOrTheExitCode()
    {
        await HandleAsync(RestartApps(1));
        _runner.ExitCode = 3;
        await HandleAsync(RestartApps(2));
        _runner.ExitCode = null;
        await HandleAsync(RestartApps(3));

        _runner.Runs.Should().Equal(RestartAppsPath, RestartAppsPath, RestartAppsPath);
        _reports.Where(r => r.Status != RemoteCommandReportStatus.Started).Select(r => (r.Id, r.Status)).Should().Equal(
            (1, RemoteCommandReportStatus.Succeeded),
            (2, RemoteCommandReportStatus.Failed),
            (3, RemoteCommandReportStatus.Failed));
        _reports.Single(r => r.Id == 2 && r.Status == RemoteCommandReportStatus.Failed).Message.Should().Contain("code 3");
        _store.State.RebootTimes.Should().BeEmpty();
    }

    [TestMethod]
    public async Task RestartApps_WithoutTheCommandFile_IsRejected()
    {
        _runner.Files.Clear();

        await HandleAsync(RestartApps(1));

        _runner.Runs.Should().BeEmpty();
        _reports.Single().Status.Should().Be(RemoteCommandReportStatus.Rejected);
    }

    [TestMethod]
    public async Task UnknownCommand_IsRejected()
    {
        await HandleAsync(new RemoteCommand { ID = 1, Type = "FormatDisk", RequestedBy = "Ada Admin", ExpiresAt = Now.AddMinutes(9) });

        _runner.Reboots.Should().BeEmpty();
        _runner.Runs.Should().BeEmpty();
        _reports.Single().Status.Should().Be(RemoteCommandReportStatus.Rejected);
    }

    [TestMethod]
    public void State_Prune_DropsOldEntries()
    {
        var state = new RemoteCommandState();
        state.HandledCommands["a:1"] = Now.AddDays(-40);
        state.HandledCommands["a:2"] = Now.AddDays(-1);
        state.RebootTimes.AddRange([Now.AddDays(-40), Now.AddHours(-1)]);

        state.Prune(Now, TimeSpan.FromDays(30));

        state.HandledCommands.Keys.Should().Equal("a:2");
        state.RebootTimes.Should().Equal(Now.AddHours(-1));
    }

    [TestMethod]
    public void FileStateStore_RoundTrips_AndToleratesAMissingOrCorruptFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"remote-commands-{Guid.NewGuid():N}.json");
        try
        {
            var store = new FileRemoteCommandStateStore(path);
            store.Load().HandledCommands.Should().BeEmpty();

            var state = new RemoteCommandState();
            state.HandledCommands["RugbyJunctionAPI:5"] = Now;
            state.RebootTimes.Add(Now);
            store.Save(state);
            store.Load().HandledCommands.Should().ContainKey("RugbyJunctionAPI:5");

            File.WriteAllText(path, "{ not json");
            store.Load().RebootTimes.Should().BeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private Task HandleAsync(RemoteCommand command, string subscriberId = "RugbyJunctionAPI") =>
        CreateHandler().HandleAsync(subscriberId, command, (id, status, message) =>
        {
            _reports.Add((id, status, message));
            return Task.FromResult(status == RemoteCommandReportStatus.Started ? _serverAnswer : RemoteCommandReportResult.Accepted);
        });

    private RemoteCommandHandler CreateHandler() => new(_settings, _runner, _store, () => Now, _ => { });

    private static RemoteCommand Reboot(int id) =>
        new() { ID = id, Type = RemoteCommandTypes.Reboot, RequestedBy = "Ada Admin", ExpiresAt = Now.AddMinutes(9) };

    private static RemoteCommand RestartApps(int id) =>
        new() { ID = id, Type = RemoteCommandTypes.RestartApps, RequestedBy = "Ada Admin", ExpiresAt = Now.AddMinutes(9) };

    private sealed class FakeRunner : IRemoteCommandRunner
    {
        public DateTime? LastBootUtc { get; set; }
        public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(TimeSpan Delay, string Message)> Reboots { get; } = [];
        public List<string> Runs { get; } = [];
        public int? ExitCode { get; set; } = 0;

        public DateTime? GetLastBootUtc(DateTime utcNow) => LastBootUtc;
        public bool FileExists(string path) => Files.Contains(path);
        public void ScheduleReboot(TimeSpan delay, string message) => Reboots.Add((delay, message));

        public Task<int?> RunAsync(string path, TimeSpan timeout)
        {
            Runs.Add(path);
            return Task.FromResult(ExitCode);
        }
    }

    private sealed class InMemoryStateStore : IRemoteCommandStateStore
    {
        public RemoteCommandState State { get; private set; } = new();

        public RemoteCommandState Load() => new()
        {
            HandledCommands = new Dictionary<string, DateTime>(State.HandledCommands),
            RebootTimes = [.. State.RebootTimes],
        };

        public void Save(RemoteCommandState state) => State = state;
    }
}
