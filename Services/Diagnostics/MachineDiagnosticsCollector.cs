using Services.Models;

namespace Services.Diagnostics
{
    /// <summary>
    /// Builds the diagnostics report sent with each beacon health check: radio program instance counts,
    /// log file freshness, startup task results, telemetry send outcomes and basic machine facts.
    /// </summary>
    public class MachineDiagnosticsCollector
    {
        private readonly ISystemProbe _probe;

        public MachineDiagnosticsCollector(ISystemProbe probe)
        {
            _probe = probe;
        }

        public BeaconDiagnostics Collect(AppSettings appSettings, Subscriber subscriber, DateTime utcNow)
        {
            var (lastSuccessAt, failuresLastHour) = TelemetrySendTracker.GetSnapshot(subscriber.ID, utcNow);
            var remoteCommandsEnabled = appSettings.AllowRemoteCommands && !string.IsNullOrWhiteSpace(subscriber.Beacon.DeviceKey);

            return new BeaconDiagnostics
            {
                CollectedAt = utcNow,
                Hostname = _probe.Hostname,
                ClientVersion = _probe.ClientVersion,
                OsVersion = _probe.OsVersion,
                LastBootAt = _probe.GetLastBootUtc(utcNow),
                LogDriveFreeBytes = _probe.GetFreeBytes(appSettings.LogDirectoryPath),
                HealthPingIntervalMinutes = subscriber.Beacon.HealthPingIntervalMinutes,
                LastTelemetrySentAt = lastSuccessAt,
                TelemetrySendFailuresLastHour = failuresLastHour,
                RemoteCommandsEnabled = remoteCommandsEnabled,
                RestartAppsAvailable = remoteCommandsEnabled
                    && !string.IsNullOrWhiteSpace(appSettings.RestartAppsCommand)
                    && _probe.FileExists(appSettings.RestartAppsCommand),
                Programs = appSettings.MonitoredProcesses
                    .Select(p =>
                    {
                        var states = _probe.GetProcessRespondingStates(p.ProcessName);
                        return new ProgramStatus
                        {
                            Name = p.Name,
                            Running = states.Count,
                            NotResponding = states.Count(responding => !responding),
                        };
                    })
                    .ToList(),
                Logs = SoftEOTLogService.LogFilePrefixes
                    .Select(prefix => new LogFileStatus
                    {
                        Prefix = prefix,
                        LastWriteAt = _probe.GetNewestFileWriteUtc(appSettings.LogDirectoryPath, prefix),
                    })
                    .ToList(),
                StartupTasks = appSettings.MonitoredScheduledTasks
                    .Select(_probe.GetScheduledTask)
                    .ToList(),
            };
        }
    }
}
