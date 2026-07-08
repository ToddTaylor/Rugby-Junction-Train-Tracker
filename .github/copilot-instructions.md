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
- For map pin or user tracking work, also read `Planning/implementation/TRACKED_PINS_IMPLEMENTATION.md` and the matching server tests before proposing changes.
- Trace end-to-end impact instead of reasoning from one layer in isolation: API/controller, service/rule engine, repository/data model, SignalR or background services, frontend services/hooks/components, and tests.
- If you forget about `/plan-feature`, type `/` in chat and it will appear in the prompt list, or consult this section of the project guidelines.

## Consistency Standards
- Use GitFlow-style branch prefixes so intent is obvious: `feature/`, `bug/`, `docs/`, `refactor/`, `test/`, or `chore/`.
- Branch format: `<prefix>/<issue-id>-<short-kebab-slug>`.
- Example branch names: `feature/1234-map-pin-filter`, `bug/987-null-reference-in-repository`, `docs/456-update-feature-workflow`.
- Always include the GitHub issue ID immediately after the `/` when an issue exists.
- Keep branch names lowercase with hyphenated slugs.
- Match pull request tags to the originating issue type when possible, such as `bug`, `enhancement`, `documentation`, or `refactor`.
- Link the issue number in the PR body and keep the PR scope narrow enough to review in one pass.
- Requirements should state the user problem, in-scope work, out-of-scope work, expected behavior, acceptance criteria, and test plan.
- Before coding, identify the affected layers: API, service or rule logic, repository or persistence, frontend, SignalR or background work, and tests.
- Use the domain vocabulary already defined for beacons, map pins, tracked pins, subdivisions, railroads, and telemetry packets.
- If a requirement cannot be tested or verified, refine it before implementation starts.

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

### Validation Policy For Changes
- For every code change, run and pass the affected automated tests before considering the task complete.
- If a change touches both frontend and backend, run and pass tests for both layers.
- Minimum expected validation by area:
	- Frontend changes: `npm --prefix Web/web.client run test` and `npm --prefix Web/web.client run build`.
	- Web server changes: `dotnet test Web/Web.ServerTests/Web.Server.Tests.csproj`.
	- API/auth/integration-contract changes: `dotnet test Web/Web.Server.IntegrationTests/Web.Server.IntegrationTests.csproj`.
	- Shared `Services` changes: `dotnet test Services.UnitTests/Services.UnitTests.csproj`.
- If a required suite cannot be run, explicitly state what was skipped and why.

## Documentation Upkeep
- When a feature changes a cross-cutting workflow or invariant, update `docs/architecture/system-overview.md` and the relevant implementation brief in the repo.
- Prefer short, factual docs that explain responsibilities, data flow, and edge cases over long narrative history.
