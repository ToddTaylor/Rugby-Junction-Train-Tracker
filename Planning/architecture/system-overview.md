# RugbyJunctionTrainTracker System Overview

## Purpose
RugbyJunctionTrainTracker is a train telemetry and map-tracking application. It ingests telemetry, applies validation and routing rules, persists current and historical state, pushes live updates to clients, and lets users monitor and track pins in the web UI.

## Solution Map
- `Web/web.client`: React 19 + Vite single-page application.
- `Web/Web.Server`: ASP.NET Core application with REST controllers, SignalR hub, EF Core data access, middleware, and hosted background services.
- `Services`: shared telemetry-support library for deserialization, throttling, subscribers, logging, and resilience policies.
- `ConsoleApp`: desktop telemetry client built on .NET.
- `Services.Models`: shared models and configuration surface for non-web components.
- `Web/Web.ServerTests`: primary server-side unit and service tests.
- `Web/Web.Server.IntegrationTests`: API-flow coverage, including auth and tracked pin flows.
- `Services.UnitTests`: tests for the shared `Services` project.

## Core Backend Flow
1. Telemetry enters the system through server endpoints and background processing paths.
2. `TelemetryService` and the telemetry rule engine validate packets and reject noisy or invalid updates.
3. `MapPinService` matches validated telemetry to beacon and subdivision context, calculates direction, applies map-pin rules, and persists current state.
4. Repositories and `TelemetryDbContext` manage SQLite persistence for current entities, history, auth, users, and user-tracked pins.
5. `NotificationHub` publishes live updates to connected clients.
6. Background services handle health checks, cleanup, and telemetry-consumer tasks.

## Frontend Responsibilities
- Fetch and display map, beacon, telemetry, and user-specific tracked pin data.
- Maintain live updates through SignalR.
- Reflect stale-state recovery and refresh behavior after device or tab wake.
- Keep map pin, beacon label, and tracked pin UI behavior aligned with server semantics.
- Route HTTP access through frontend service modules instead of embedding API logic in components.

## Important Server Boundaries
### Controllers
Controllers under `Web/Web.Server/Controllers/v1` expose HTTP contracts. Keep them thin and delegate behavior to services.

### Services
Service classes under `Web/Web.Server/Services` hold most business logic. This includes auth, map pins, telemetry handling, tracked pin lifecycle, and railroad/subdivision operations.

### Rules
Rule engines under `Web/Web.Server/Services/Rules` enforce telemetry and map-pin constraints. Add or change validation there when behavior is rule-based.

### Repositories And Entities
Repositories under `Web/Web.Server/Repositories` own EF Core persistence logic. Entities under `Web/Web.Server/Entities` model persisted state.

### Real-Time And Background Work
`Web/Web.Server/Hubs/NotificationHub.cs` defines real-time push behavior. Hosted services registered in `Web/Web.Server/Program.cs` are part of the live system behavior and should be considered during planning.

### Beacon PC Health
- Each beacon PC runs the ConsoleApp, which posts `POST api/v1/Beacons/Health/{id}` every `HealthPingIntervalMinutes` (2 in the bundled production settings). The body is a diagnostics report built by `Services/Diagnostics/MachineDiagnosticsCollector`: SDR#, SoftEOT and SoftDPU instance and hung counts, the number of SDR# slice windows (visible SDR# windows titled "Sharp Slicer", counted through `MonitoredProcess.WindowTitleContains`), newest write time per log prefix, Task Scheduler results for `MonitoredScheduledTasks`, telemetry send outcomes, uptime, version and free disk space. Older ConsoleApp versions send an empty body, which only records the check-in.
- The check-in updates every `BeaconRailroad.LastUpdate`, which is the PC's last check-in time, and the report is stored in `BeaconDiagnostics`, one row per beacon, overwritten each time.
- `BeaconServerStatusEvaluator` turns the last check-in, the report and the beacon's expected program counts (`Beacon.Expected*Count`, null = not checked) into Online, Degraded or Offline with plain-language reasons. Log file ages are shown but never mark a PC Degraded, because SoftEOT and SoftDPU only write when they decode a train. A PC is Offline after three missed check-ins, between 5 and 15 minutes. A report older than that threshold is ignored, so a PC that went back to an older ConsoleApp shows as having no diagnostics.
- `ServerHealthController` serves the Server Health admin page. Admins see every beacon; Custodians see beacons on subdivisions where they are the custodian, and may set expected counts and mute alerts only for those.
- Alerts: `ServerHealthMonitorService` runs `ServerHealthService.DetectStatusChangesAsync` once a minute and sends `ServerStatusChanged` to the `user-{id}` group of every active Admin and of the beacon's active Custodians. `BeaconServerState` (one row per beacon) holds the last announced status, a pending change and the mute. A change to or from Offline is announced at once; a change between Online and Degraded must last `StatusConfirmMinutes` (3) first. The first time the monitor sees a beacon, it records the status without announcing it, so a deploy does not set off a burst of alerts.
- Map status: the map's beacon color comes from the confirmed Server Health status, not from `LastUpdate` alone. `BeaconMapStatus.Resolve` applies `ApplyObservedStatus` to a copy of the beacon's `BeaconServerState`, so the map turns gray on the same rules as the alerts but without waiting for the next monitor run. `BeaconRailroadHealthService` broadcasts it once a minute in `BeaconUpdate`, and `GET api/v1/BeaconRailroads` returns it, as `online` (true only for Online), `serverStatus` and `serverStatusReasons`. Every row of a beacon carries the same status. Degraded and Offline beacons are drawn gray, and the map's info badge shows the status and reasons. `telemetryStale` (the hollow dot) is computed only for online beacons, from `TelemetryStaleHoursOverride` or `TelemetryStaleHoursDefault`.
- Muting (`PUT`/`DELETE api/v1/ServerHealth/{id}/mute`) lasts until a set time or until alerts are turned back on. Changes on a muted server are still sent, with `alertsMuted: true`, so an open dashboard refreshes; the client shows no pop-up for them. Changes that happen while muted are not replayed when the mute ends.
- `ServerAlertListener`, mounted in `App.tsx`, holds its own SignalR connection for signed-in Admins and Custodians, shows the pop-ups on every page, raises a browser notification when the tab is in the background and the user has allowed it, and re-raises each change as a window event that the Server Health page uses to refresh.
- Remote reboot and restart apps: see `Planning/implementation/SERVER_REMOTE_COMMANDS_IMPLEMENTATION.md`. In short, `ServerCommandService` stores a `BeaconCommand`, the PC receives it in the response to its next check-in (`200 { command }`), reports `Started` before acting and the result after, and the server announces each step as `ServerCommandUpdated`. A beacon with a `BeaconDeviceKey` must send it in `X-Beacon-Key` on every check-in and report, or the request gets 401; commands are only handed to a PC that presents its key and reports `RemoteCommandsEnabled`.
- The command and device key endpoints of `ServerHealthController` carry `[RequireHeaderAuthToken]`: they refuse a sign-in token sent in the `access_token` query string, which `AuthTokenMiddleware` otherwise accepts for SignalR.

## Domain Concepts
- `Beacon`: a fixed trackside detection point.
- `BeaconRailroad`: the relationship between a beacon and a subdivision or railroad context, including directional information.
- `MapPin`: the active or recent train location shown on the map.
- `MapPinHistory`: historical snapshots of pin state.
- `UserTrackedPin`: a user-owned tracked map pin with symbol, color, and expiration.
- `Telemetry`: raw packet data from DPU, HOT, or EOT sources.
- `BeaconDiagnostics`: the latest health report from a beacon PC.
- `BeaconServerState`: the alert state of a beacon PC: last announced status, pending change and mute.
- `BeaconDeviceKey`: the SHA-256 hash of the secret a beacon PC sends in `X-Beacon-Key`.
- `BeaconCommand`: a reboot or restart-apps request for a beacon PC and its audit record (who asked, delivery, start, result, cancellation).
- `Subdivision` and `Railroad`: route hierarchy and ownership context.
- `SubdivisionTrackageRight`: rules that affect which railroad context is valid.

## Existing Invariants
- Store and compare time in UTC.
- Keep controllers thin and push business logic into services or rule engines.
- Keep persistence logic in repositories.
- Treat map pin direction, freshness, and merge behavior as correctness-sensitive.
- Treat tracked pin persistence as user-specific behavior with expiration semantics.
- Respect ordered rule execution where rule registration order matters.

## Planning Checklist For Future Features
When planning a change, identify:
1. Which API surface or background process receives the input.
2. Which services or rule engines own the business behavior.
3. Which repositories, entities, migrations, or DTOs must change.
4. Whether SignalR payloads or client refresh behavior are affected.
5. Which frontend services, hooks, state flows, or map components need updates.
6. Which unit and integration tests already cover the behavior and which new tests are required.

## Canonical Files To Read First
- `Web/Web.Server/Program.cs`
- `Web/Web.Server/Services/MapPinService.cs`
- `Web/Web.Server/Services/TelemetryService.cs`
- `Web/Web.Server/Hubs/NotificationHub.cs`
- `Web/Web.ServerTests/Services/MapPinServiceTests.cs`
- `TRACKED_PINS_IMPLEMENTATION.md`
- `Web/web.client/src/App.tsx`
- `Web/web.client/src/services/`
- `Web/web.client/README.md`

## Build And Test Entry Points
- `dotnet build RugbyJunctionTrainTracker.sln`
- `dotnet test Web/Web.ServerTests/Web.Server.Tests.csproj`
- `dotnet test Web/Web.Server.IntegrationTests/Web.Server.IntegrationTests.csproj`
- `dotnet test Services.UnitTests/Services.UnitTests.csproj`
- `cd Web/web.client && npm run build`

## Related Notes
- `TRACKED_PINS_IMPLEMENTATION.md` documents the tracked map pin persistence workflow across API, service, database, and frontend layers.
- Repository memory currently contains known gotchas around map pin merge behavior and beacon label direction refresh. Capture similar invariants in repo docs when new bugs are fixed.
