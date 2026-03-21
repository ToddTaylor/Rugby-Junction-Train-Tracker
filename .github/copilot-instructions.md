# RugbyJunctionTrainTracker Project Guidelines

## Architecture
- This repo is centered on a .NET solution with a web application under `Web/`.
- `Web/web.client` is the React 19 + Vite frontend.
- `Web/Web.Server` is the ASP.NET Core API, SignalR hub host, SQLite/EF Core application, and background-service host.
- `Services` contains shared telemetry parsing, throttling, subscriber, and resilience code used outside the web app.
- `ConsoleApp` is a .NET desktop telemetry client.
- Test projects live in `Web/Web.ServerTests`, `Web/Web.Server.IntegrationTests`, and `Services.UnitTests`.

## Planning Workflow
- **Use `/plan-feature` in chat** when planning a new feature or significant change. This prompt guides you through reading the system overview, area instructions, implementation briefs, and existing tests before proposing a detailed plan.
- For feature planning, read `docs/architecture/system-overview.md` first, then inspect the relevant area instruction file under `.github/instructions/`.
- For map pin or user tracking work, also read `TRACKED_PINS_IMPLEMENTATION.md` and the matching server tests before proposing changes.
- Trace end-to-end impact instead of reasoning from one layer in isolation: API/controller, service/rule engine, repository/data model, SignalR or background services, frontend services/hooks/components, and tests.
- If you forget about `/plan-feature`, type `/` in chat and it will appear in the prompt list, or consult this section of the project guidelines.

## Project Conventions
- Keep controllers thin. Put business rules in services or rule-engine classes, not in controllers.
- Keep EF Core access in repositories and persistence models in `Web/Web.Server/Entities`.
- Preserve UTC-based time handling and expiration logic when working with telemetry, map pins, auth tokens, or tracked pins.
- Treat SignalR notifications, background services, and database cleanup as part of the product behavior, not incidental infrastructure.
- Favor existing naming and dependency-injection patterns from `Web/Web.Server/Program.cs`.

## Domain Vocabulary
- A beacon is a fixed detection point.
- A map pin is the current or recent train location derived from telemetry and beacon context.
- A tracked pin is a user-specific persisted pin marker with symbol, color, and expiration.
- A subdivision belongs to a railroad and may have trackage-rights rules.
- Telemetry packets are filtered by rule engines before they become map pin updates.

## Build And Test
- Build the main solution with `dotnet build RugbyJunctionTrainTracker.sln`.
- Run server tests with `dotnet test Web/Web.ServerTests/Web.Server.Tests.csproj`.
- Run integration tests with `dotnet test Web/Web.Server.IntegrationTests/Web.Server.IntegrationTests.csproj` when API flows or auth behavior change.
- Run shared services tests with `dotnet test Services.UnitTests/Services.UnitTests.csproj`.
- Build the frontend with `cd Web/web.client && npm run build`.

## Documentation Upkeep
- When a feature changes a cross-cutting workflow or invariant, update `docs/architecture/system-overview.md` and the relevant implementation brief in the repo.
- Prefer short, factual docs that explain responsibilities, data flow, and edge cases over long narrative history.
