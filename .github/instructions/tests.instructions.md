---
description: "Use when adding, updating, or reviewing tests, or when planning a feature and you need the best executable description of current behavior. Covers server unit tests, integration tests, and shared services tests."
name: "Testing Instructions"
applyTo: ["Web/Web.ServerTests/**", "Web/Web.Server.IntegrationTests/**", "Services.UnitTests/**"]
---
# Testing Instructions

- Treat tests as a primary source of behavioral truth when planning features for this repo.
- `Web/Web.ServerTests` contains core service and repository behavior coverage; start there for map pin, direction, auth, telemetry, and rule behavior.
- `Web/Web.Server.IntegrationTests` is the place to verify API contracts, auth flows, and user-specific persistence behavior across layers.
- `Services.UnitTests` covers the shared telemetry-support library.
- When changing behavior, update the closest existing test suite rather than creating disconnected new coverage in an arbitrary project.
- Prefer targeted test runs for the affected project first, then expand only if the change crosses multiple layers.
- If a feature plan changes API shape, persistence, or real-time behavior, name the tests that should prove the change before implementation begins.
