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

## Domain Concepts
- `Beacon`: a fixed trackside detection point.
- `BeaconRailroad`: the relationship between a beacon and a subdivision or railroad context, including directional information.
- `MapPin`: the active or recent train location shown on the map.
- `MapPinHistory`: historical snapshots of pin state.
- `UserTrackedPin`: a user-owned tracked map pin with symbol, color, and expiration.
- `Telemetry`: raw packet data from DPU, HOT, or EOT sources.
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
