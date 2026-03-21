---
description: "Use when planning or changing ASP.NET Core API endpoints, controllers, services, repositories, EF Core entities, SignalR hubs, middleware, migrations, auth, telemetry ingestion, map pin behavior, or files under Web/Web.Server."
name: "Web Server Instructions"
applyTo: "Web/Web.Server/**"
---
# Web Server Instructions

- `Web/Web.Server` is the main backend application. It hosts controllers, EF Core persistence, middleware, SignalR, and background services.
- Keep controllers in `Controllers/v1` thin. Move behavior into services, repositories, or rule engines.
- Put business rules in service classes or `Services/Rules`, especially for telemetry validation, map pin decisions, trackage rights, and lifecycle constraints.
- Keep data access in repositories and persistence schema changes in entities, DbContext, and migrations.
- Use `Program.cs` as the source of truth for DI registration, middleware order, hosted services, CORS, and runtime wiring.
- Treat hosted services, auth middleware, and SignalR notifications as part of feature behavior when planning changes, not as separate infrastructure concerns.
- Preserve UTC time handling and be careful with expiration, freshness, and history semantics.
- Before proposing non-trivial server changes, inspect the matching tests in `Web/Web.ServerTests` and `Web/Web.Server.IntegrationTests`.
