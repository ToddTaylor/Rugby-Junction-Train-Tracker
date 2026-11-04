using System.Globalization;
using Web.Server.Entities;
using Web.Server.Enums;

namespace Web.Server.Services
{
    /// <summary>
    /// Decides whether a beacon PC is Online, Degraded or Offline from its last check-in and its latest
    /// diagnostics report, and lists the problems in plain language.
    /// </summary>
    public static class BeaconServerStatusEvaluator
    {
        public const string SdrSharp = "SDR#";
        public const string SoftEot = "SoftEOT";
        public const string SoftDpu = "SoftDPU";

        /// <summary>
        /// Telemetry sends are reported as failing when failures were seen and nothing succeeded for this long.
        /// </summary>
        public const int TelemetryFailureMinutes = 15;

        public const long LowDiskSpaceBytes = 2L * 1024 * 1024 * 1024;

        // Task Scheduler "Last Result" codes that are not failures: the task is still running, or has not run yet.
        private const int TaskStillRunning = 0x41301;
        private const int TaskHasNotRun = 0x41303;

        public sealed record Result(ServerStatus Status, IReadOnlyList<string> Reasons, int OfflineThresholdMinutes, bool UsesDiagnostics);

        /// <summary>
        /// Minutes without a check-in before a PC is Offline: three missed health checks, at least 5 minutes,
        /// and never longer than the map's 15-minute offline cutoff.
        /// </summary>
        public static int GetOfflineThresholdMinutes(int? healthPingIntervalMinutes)
        {
            if (healthPingIntervalMinutes is not > 0)
            {
                return BeaconRailroadHealthService.HealthCutoffMinutes;
            }

            return Math.Min(BeaconRailroadHealthService.HealthCutoffMinutes, Math.Max(3 * healthPingIntervalMinutes.Value, 5));
        }

        /// <summary>
        /// Programs the dashboard tracks, with the number of instances the beacon should run (null = not checked).
        /// </summary>
        public static IReadOnlyList<(string Name, int? Expected)> GetExpectedPrograms(Beacon beacon) =>
        [
            (SdrSharp, beacon.ExpectedSdrSharpCount),
            (SoftEot, beacon.ExpectedSoftEotCount),
            (SoftDpu, beacon.ExpectedSoftDpuCount),
        ];

        public static Result Evaluate(Beacon beacon, BeaconDiagnostics? diagnostics, DateTime? lastCheckIn, int telemetryStaleHours, DateTime utcNow)
        {
            var threshold = GetOfflineThresholdMinutes(diagnostics?.HealthPingIntervalMinutes);

            if (lastCheckIn == null)
            {
                return new Result(ServerStatus.Offline, ["Never checked in"], threshold, false);
            }

            var sinceCheckIn = utcNow - lastCheckIn.Value;
            if (sinceCheckIn > TimeSpan.FromMinutes(threshold))
            {
                return new Result(ServerStatus.Offline, [$"No check-in for {FormatDuration(sinceCheckIn)}"], threshold, false);
            }

            // A report older than the threshold means the PC is still checking in without diagnostics (an older ConsoleApp).
            if (diagnostics == null || utcNow - diagnostics.LastUpdate > TimeSpan.FromMinutes(threshold))
            {
                return new Result(ServerStatus.Online, [], threshold, false);
            }

            var reasons = new List<string>();
            AddProgramReasons(beacon, diagnostics, reasons);
            AddLogReasons(beacon, diagnostics, telemetryStaleHours, utcNow, reasons);
            AddStartupTaskReasons(diagnostics, reasons);
            AddTelemetrySendReasons(diagnostics, utcNow, reasons);

            if (diagnostics.LogDriveFreeBytes is { } freeBytes && freeBytes < LowDiskSpaceBytes)
            {
                reasons.Add($"Low disk space ({(freeBytes / (1024d * 1024 * 1024)).ToString("0.0", CultureInfo.InvariantCulture)} GB free)");
            }

            return new Result(reasons.Count > 0 ? ServerStatus.Degraded : ServerStatus.Online, reasons, threshold, true);
        }

        private static void AddProgramReasons(Beacon beacon, BeaconDiagnostics diagnostics, List<string> reasons)
        {
            foreach (var (name, expected) in GetExpectedPrograms(beacon))
            {
                var reported = FindProgram(diagnostics, name);
                var running = reported?.Running ?? 0;

                if (expected is > 0 && running == 0)
                {
                    reasons.Add($"{name} is not running");
                }
                else if (expected.HasValue && running < expected.Value)
                {
                    reasons.Add($"Only {running} of {expected.Value} {name} instances running");
                }

                if (reported is { NotResponding: > 0 })
                {
                    reasons.Add(reported.NotResponding == 1
                        ? $"{name} is not responding"
                        : $"{reported.NotResponding} {name} instances are not responding");
                }
            }
        }

        /// <summary>
        /// SoftEOT writes the eot and hot logs and each SoftDPU instance writes dpu1 to dpu4, so a log is only
        /// checked when its program is expected to run, and a dpu log only up to the expected SoftDPU count.
        /// </summary>
        private static void AddLogReasons(Beacon beacon, BeaconDiagnostics diagnostics, int telemetryStaleHours, DateTime utcNow, List<string> reasons)
        {
            foreach (var log in diagnostics.Logs)
            {
                if (log.LastWriteAt is not { } lastWriteAt || !IsLogExpected(beacon, log.Prefix))
                {
                    continue;
                }

                var age = utcNow - lastWriteAt;
                if (age > TimeSpan.FromHours(telemetryStaleHours))
                {
                    reasons.Add($"No {log.Prefix} log written in {FormatDuration(age)}");
                }
            }
        }

        private static bool IsLogExpected(Beacon beacon, string prefix)
        {
            if (prefix.Equals("eot", StringComparison.OrdinalIgnoreCase) || prefix.Equals("hot", StringComparison.OrdinalIgnoreCase))
            {
                return beacon.ExpectedSoftEotCount is > 0;
            }

            if (prefix.StartsWith("dpu", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(prefix.AsSpan(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out var instance))
            {
                return beacon.ExpectedSoftDpuCount >= instance;
            }

            return false;
        }

        private static void AddStartupTaskReasons(BeaconDiagnostics diagnostics, List<string> reasons)
        {
            foreach (var task in diagnostics.StartupTasks)
            {
                if (!task.Found)
                {
                    reasons.Add($"Startup task '{task.Name}' not found");
                }
                else if (task.LastResult is { } result && result != 0 && result != TaskStillRunning && result != TaskHasNotRun)
                {
                    reasons.Add($"Startup task '{task.Name}' failed (0x{result:X})");
                }
            }
        }

        private static void AddTelemetrySendReasons(BeaconDiagnostics diagnostics, DateTime utcNow, List<string> reasons)
        {
            if (diagnostics.TelemetrySendFailuresLastHour == 0)
            {
                return;
            }

            var recentSuccess = diagnostics.LastTelemetrySentAt is { } lastSent &&
                utcNow - lastSent <= TimeSpan.FromMinutes(TelemetryFailureMinutes);

            if (!recentSuccess)
            {
                reasons.Add($"Telemetry sends failing ({diagnostics.TelemetrySendFailuresLastHour} in the last hour)");
            }
        }

        private static DiagnosticsProgramStatus? FindProgram(BeaconDiagnostics diagnostics, string name) =>
            diagnostics.Programs.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        public static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.FromHours(1))
            {
                return $"{Math.Max(1, (int)duration.TotalMinutes)} min";
            }

            if (duration < TimeSpan.FromHours(48))
            {
                return $"{(int)duration.TotalHours} h";
            }

            return $"{(int)duration.TotalDays} days";
        }
    }
}
