using Services.Models;

namespace Services.RemoteCommands
{
    /// <summary>
    /// Carries out a command from the Server Health page: checks the PC's own safeguards, tells the server it is
    /// starting, records it so it never runs twice, then reboots or runs RestartAppsCommand and reports the result.
    /// </summary>
    public class RemoteCommandHandler
    {
        /// <summary>
        /// Time between accepting a reboot and Windows restarting, so anyone at the PC sees the notice.
        /// </summary>
        public static readonly TimeSpan RebootDelay = TimeSpan.FromSeconds(60);

        public static readonly TimeSpan RestartAppsTimeout = TimeSpan.FromMinutes(15);

        private static readonly TimeSpan StateRetention = TimeSpan.FromDays(30);

        // Shared by every subscriber, so the production and beta servers cannot start two commands at once.
        private static readonly SemaphoreSlim Lock = new(1, 1);

        private readonly AppSettings _appSettings;
        private readonly IRemoteCommandRunner _runner;
        private readonly IRemoteCommandStateStore _stateStore;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<string> _log;

        public RemoteCommandHandler(
            AppSettings appSettings,
            IRemoteCommandRunner runner,
            IRemoteCommandStateStore stateStore,
            Func<DateTime> utcNow,
            Action<string> log)
        {
            _appSettings = appSettings;
            _runner = runner;
            _stateStore = stateStore;
            _utcNow = utcNow;
            _log = log;
        }

        public static RemoteCommandHandler CreateDefault(AppSettings appSettings, Action<string> log) => new(
            appSettings,
            new WindowsRemoteCommandRunner(),
            FileRemoteCommandStateStore.CreateDefault(),
            () => DateTime.UtcNow,
            log);

        /// <param name="report">Sends a status and message for the command to the server that issued it.</param>
        public async Task HandleAsync(
            string subscriberId,
            RemoteCommand command,
            Func<int, string, string?, Task<RemoteCommandReportResult>> report)
        {
            await Lock.WaitAsync();
            try
            {
                var utcNow = _utcNow();
                var state = _stateStore.Load();
                state.Prune(utcNow, StateRetention);

                var key = RemoteCommandState.CommandKey(subscriberId, command.ID);
                if (state.HandledCommands.ContainsKey(key))
                {
                    return;
                }

                var refusal = RemoteCommandGuard.GetRefusal(command, _appSettings, state, utcNow, _runner.GetLastBootUtc(utcNow), _runner.FileExists);
                if (refusal != null)
                {
                    _log($"Refused remote {command.Type} from {command.RequestedBy}: {refusal}");
                    await report(command.ID, RemoteCommandReportStatus.Rejected, refusal);
                    MarkHandled(state, key, utcNow, isReboot: false);
                    return;
                }

                // The server answers 409 if the command was cancelled in the meantime; then it must not run.
                if (await report(command.ID, RemoteCommandReportStatus.Started, null) == RemoteCommandReportResult.Refused)
                {
                    _log($"Remote {command.Type} {command.ID} was cancelled or expired on the server; not carried out.");
                    MarkHandled(state, key, utcNow, isReboot: false);
                    return;
                }

                // Recorded before acting, so a reboot cannot repeat even if the result never reaches the server.
                var isReboot = command.Type == RemoteCommandTypes.Reboot;
                MarkHandled(state, key, utcNow, isReboot);

                if (isReboot)
                {
                    await RebootAsync(command, report);
                }
                else
                {
                    await RestartAppsAsync(command, report);
                }
            }
            finally
            {
                Lock.Release();
            }
        }

        private async Task RebootAsync(RemoteCommand command, Func<int, string, string?, Task<RemoteCommandReportResult>> report)
        {
            try
            {
                _runner.ScheduleReboot(RebootDelay, $"Restart requested from the Rugby Junction Server Health page by {command.RequestedBy}.");
                _log($"Remote reboot requested by {command.RequestedBy}. Windows restarts in {RebootDelay.TotalSeconds:0} seconds.");
            }
            catch (Exception ex)
            {
                _log($"Remote reboot failed: {ex.Message}");
                await report(command.ID, RemoteCommandReportStatus.Failed, $"Could not schedule the restart: {ex.Message}");
            }
        }

        private async Task RestartAppsAsync(RemoteCommand command, Func<int, string, string?, Task<RemoteCommandReportResult>> report)
        {
            var path = _appSettings.RestartAppsCommand!;
            _log($"Restarting apps for {command.RequestedBy}: running {path}");

            string status;
            string message;
            try
            {
                var exitCode = await _runner.RunAsync(path, RestartAppsTimeout);
                (status, message) = exitCode switch
                {
                    0 => (RemoteCommandReportStatus.Succeeded, "The apps were restarted."),
                    null => (RemoteCommandReportStatus.Failed, $"RestartAppsCommand did not finish within {RestartAppsTimeout.TotalMinutes:0} minutes."),
                    _ => (RemoteCommandReportStatus.Failed, $"RestartAppsCommand exited with code {exitCode}."),
                };
            }
            catch (Exception ex)
            {
                (status, message) = (RemoteCommandReportStatus.Failed, $"RestartAppsCommand could not run: {ex.Message}");
            }

            _log(message);
            await report(command.ID, status, message);
        }

        private void MarkHandled(RemoteCommandState state, string key, DateTime utcNow, bool isReboot)
        {
            state.HandledCommands[key] = utcNow;
            if (isReboot)
            {
                state.RebootTimes.Add(utcNow);
            }

            _stateStore.Save(state);
        }
    }
}
