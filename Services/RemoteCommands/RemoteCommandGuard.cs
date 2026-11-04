using Services.Models;

namespace Services.RemoteCommands
{
    /// <summary>
    /// The PC's own checks before it carries out a remote command. These hold even if the server is wrong or has
    /// been tampered with.
    /// </summary>
    public static class RemoteCommandGuard
    {
        public const int MaxRebootsPerDay = 3;

        /// <summary>
        /// A reboot is refused when the PC started less than this long ago, so a fault cannot cause a reboot loop.
        /// </summary>
        public static readonly TimeSpan MinUptimeForReboot = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Returns null when the command may run, or the reason it is refused.
        /// </summary>
        public static string? GetRefusal(
            RemoteCommand command,
            AppSettings appSettings,
            RemoteCommandState state,
            DateTime utcNow,
            DateTime? lastBootUtc,
            Func<string, bool> fileExists)
        {
            if (!appSettings.AllowRemoteCommands)
            {
                return "Remote commands are turned off on this PC (AllowRemoteCommands).";
            }

            if (command.ExpiresAt.ToUniversalTime() <= utcNow)
            {
                return "The command expired before the PC received it.";
            }

            switch (command.Type)
            {
                case RemoteCommandTypes.Reboot:
                    if (lastBootUtc.HasValue && utcNow - lastBootUtc.Value < MinUptimeForReboot)
                    {
                        var minutes = Math.Max(0, (int)(utcNow - lastBootUtc.Value).TotalMinutes);
                        return $"The PC started {minutes} min ago. It does not restart again within {MinUptimeForReboot.TotalMinutes:0} minutes of starting.";
                    }

                    var rebootsToday = state.RebootTimes.Count(t => t > utcNow.AddDays(-1));
                    if (rebootsToday >= MaxRebootsPerDay)
                    {
                        return $"The PC has already restarted {rebootsToday} times in the last 24 hours, the most it allows.";
                    }

                    return null;

                case RemoteCommandTypes.RestartApps:
                    if (string.IsNullOrWhiteSpace(appSettings.RestartAppsCommand) || !fileExists(appSettings.RestartAppsCommand))
                    {
                        return "RestartAppsCommand is not set up on this PC.";
                    }

                    return null;

                default:
                    return $"This ConsoleApp version does not know the command '{command.Type}'.";
            }
        }
    }
}
