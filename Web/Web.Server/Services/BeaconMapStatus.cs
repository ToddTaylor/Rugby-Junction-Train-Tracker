using Web.Server.Entities;
using Web.Server.Enums;

namespace Web.Server.Services
{
    /// <summary>
    /// The Server Health status a beacon shows on the map, with the reasons behind it.
    /// </summary>
    public sealed record BeaconMapStatus(ServerStatus Status, IReadOnlyList<string> Reasons)
    {
        public bool IsOnline => Status == ServerStatus.Online;

        /// <summary>
        /// Resolves the confirmed Server Health status of a beacon PC: the status that support users have been
        /// (or are about to be) alerted to. A change to or from Offline counts at once; a change between Online
        /// and Degraded counts only after it has lasted ServerHealthService.StatusConfirmMinutes, so a program
        /// restart does not flicker the map. The beacon's BeaconRailroads must be loaded, since health checks
        /// are recorded on them.
        /// </summary>
        public static BeaconMapStatus Resolve(Beacon beacon, BeaconDiagnostics? diagnostics, BeaconServerState? state, DateTime utcNow)
        {
            var observed = BeaconServerStatusEvaluator.Evaluate(
                beacon, diagnostics, ServerHealthService.GetLastCheckIn(beacon, diagnostics), utcNow);

            if (state == null)
            {
                return new BeaconMapStatus(observed.Status, observed.Reasons);
            }

            // Work on a copy so resolving a status never changes the saved alert state.
            var projected = new BeaconServerState
            {
                BeaconID = state.BeaconID,
                Status = state.Status,
                StatusSince = state.StatusSince,
                PendingStatus = state.PendingStatus,
                PendingSince = state.PendingSince,
            };
            ServerHealthService.ApplyObservedStatus(projected, observed.Status, utcNow);

            // While a change is still being confirmed, the current reasons describe the new status, not this one.
            var reasons = projected.Status == observed.Status ? observed.Reasons : [];
            return new BeaconMapStatus(projected.Status, reasons);
        }
    }
}
