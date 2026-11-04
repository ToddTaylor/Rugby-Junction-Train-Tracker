# Server Remote Commands (Reboot and Restart Apps)

Admins, and Custodians of a beacon's subdivisions, can reboot a beacon PC or restart its radio programs from the Server Health page. The server cannot open a connection to a PC, so the PC collects commands with its regular health check.

## Flow
1. A support user clicks **Reboot PC** (and types the server name) or **Restart apps**. The client calls `POST api/v1/ServerHealth/{beaconId}/commands` with `{ type }`.
2. `ServerCommandService.RequestCommandAsync` refuses the request when the beacon has no device key, is Offline, does not report `RemoteCommandsEnabled`, has no restart apps command (for RestartApps), already has a pending or running command, or has reached its daily limit. Otherwise it stores a `BeaconCommand` that expires after 10 minutes and sends `ServerCommandUpdated` to the beacon's support users.
3. On its next check-in (`POST api/v1/Beacons/Health/{id}`, every 2 minutes), a PC that sends the right `X-Beacon-Key` and reports `RemoteCommandsEnabled` receives `200 { command: { id, type, requestedBy, expiresAt } }`. Without a waiting command the response stays `204`.
4. The PC runs its own checks (below), then reports `Started` to `POST api/v1/Beacons/Health/{id}/commands/{commandId}`. The server answers `409` if the command was cancelled, expired or already started, and the PC then does nothing.
5. Reboot: the PC runs `shutdown /r /t 60`. The server marks the command Succeeded when a later check-in reports a boot time after the start, or Failed when the PC checks in 5 minutes after the start without having restarted.
6. Restart apps: the PC runs `RestartAppsCommand`, waits up to 15 minutes, and reports `Succeeded` (exit code 0) or `Failed`.
7. A pending command can be cancelled with `DELETE api/v1/ServerHealth/{beaconId}/commands/{commandId}`. A started command cannot.

Command status is worked out from the timestamps (`BeaconCommand.GetStatus`): Pending, Running, Succeeded, Failed, Rejected, Cancelled or Expired.

## Security
- **Who can ask.** Every command and device key endpoint needs a valid sign-in token and re-checks the role on each request (Admin, or Custodian of a subdivision the beacon serves): 401 without a user, 403 otherwise. These endpoints carry `[RequireHeaderAuthToken]`, so a token in the `access_token` query string is refused with 401. A link, an image tag or a crawler cannot set a header, and every action is a POST or DELETE.
- **Who can receive.** Each beacon can have a device key: 32 random bytes, shown once on the Server Health page, stored only as a SHA-256 hash, and compared in constant time. Once a beacon has a key, check-ins and reports without it get 401 and are not recorded, and the refusal time is shown on the page. Commands are never handed out for a beacon without a key. The public `X-Api-Key` alone is not enough to see or report on a command.
- **Limits on the server.** One pending or running command per beacon (409). At most 3 reboots and 6 app restarts started per beacon in 24 hours (429); commands the PC refused do not count. Commands expire after 10 minutes.
- **Limits on the PC** (`Services/RemoteCommands/RemoteCommandGuard`). Commands run only when `AllowRemoteCommands` is true in the PC's settings (off by default). The PC refuses an expired command, a reboot within 15 minutes of starting, and a fourth reboot in 24 hours, whatever the server asks. It records each handled command (`%LOCALAPPDATA%\RugbyJunctionTrainTracker\remote-commands.json`) before acting, so the same command never runs twice.
- **Audit.** Each `BeaconCommand` keeps who requested it and when, delivery, start and finish times, the result message, and who cancelled it. The page lists the last 10 commands from the last 30 days. Device key changes and command requests are logged as warnings.

## Setting up a PC
1. On the Server Health page, open the server's details and click **Create device key**. Copy the key; it is not shown again. The server shows Offline until the key is entered on the PC.
2. On the PC, press Ctrl+U in the RugbyJunctionTrainTracker console. Press Enter to keep the existing values, paste the key at the device key prompt for the subscriber that sends to this site, and answer `y` to allow reboot and restart apps.
3. Optional: give the full path of a restart apps program at the next prompt. The program should close SDR#, SoftEOT and SoftDPU, start them again the way the startup tasks do, and exit with code 0 on success. The ConsoleApp waits up to 15 minutes for it. Without it, only Reboot is offered.

The settings are saved in `%LOCALAPPDATA%\RugbyJunctionTrainTracker\appsettings.{environment}.json` (`AllowRemoteCommands`, `RestartAppsCommand`, and `Subscribers[].Beacon.DeviceKey`). The production and beta sites each have their own key for their own subscriber.

A reboot relies on the existing startup tasks to open SDR#, SoftEOT, SoftDPU and the ConsoleApp after Windows starts.

## Files
- Server: `Services/ServerCommandService.cs`, `Entities/BeaconCommand.cs`, `Entities/BeaconDeviceKey.cs`, `Filters/RequireHeaderAuthTokenAttribute.cs`, `Controllers/v1/ServerHealthController.cs`, `Controllers/v1/BeaconsController.cs` (`CheckHealth`, `ReportCommand`).
- PC: `Services/RemoteCommands/`, `Services/Subscribers/RugbyJunctionAPI*/BeaconApiClient.cs` and `BeaconHealthService.cs`, `ConsoleApp/Program.cs` (settings prompts).
- Client: `views/AdminServerHealth.tsx` (Remote restart section and dialogs), `components/ServerAlertListener.tsx` (command pop-ups), `utils/serverHealth.ts`.
- Tests: `Web.ServerTests/Services/ServerCommandServiceTests.cs`, `Web.ServerTests/Middleware/HeaderAuthTokenTests.cs`, the Server Health and Beacons controller tests, and `Services.UnitTests/RemoteCommands/RemoteCommandHandlerTests.cs`.
