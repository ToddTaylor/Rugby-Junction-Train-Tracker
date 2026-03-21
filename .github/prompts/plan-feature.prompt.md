---
description: "Plan a new feature or change for RugbyJunctionTrainTracker. Guides end-to-end feature planning by reading architecture, area instructions, implementation briefs, and existing tests before proposing a plan."
name: "Plan Feature"
argument-hint: "Feature description and acceptance criteria..."
agent: "agent"
---
# Plan Feature for RugbyJunctionTrainTracker

You are planning a feature for the RugbyJunctionTrainTracker application.

## Step 1: Read Architecture Context
Start by reading the system overview:
- [System Overview](../../docs/architecture/system-overview.md)

## Step 2: Identify the Affected Area and Read Instructions
Based on the feature description, determine which areas are affected:
- **If frontend or UI**: Read [.github/instructions/web-client.instructions.md](.github/instructions/web-client.instructions.md)
- **If server, API, or persistence**: Read [.github/instructions/web-server.instructions.md](.github/instructions/web-server.instructions.md)
- **If shared telemetry services**: Read [.github/instructions/services.instructions.md](.github/instructions/services.instructions.md)
- **If adding/changing tests**: Read [.github/instructions/tests.instructions.md](.github/instructions/tests.instructions.md)

## Step 3: Read Relevant Implementation Briefs
If they apply:
- For map pin or tracked pin behavior: Read [TRACKED_PINS_IMPLEMENTATION.md](../../TRACKED_PINS_IMPLEMENTATION.md)
- For map pin merging or duplicate beacon behavior: Read [MAP_PIN_MERGING_IMPLEMENTATION.md](../../docs/implementation/MAP_PIN_MERGING_IMPLEMENTATION.md)
- For beacon direction or live label updates: Read [BEACON_LABEL_DIRECTION_IMPLEMENTATION.md](../../docs/implementation/BEACON_LABEL_DIRECTION_IMPLEMENTATION.md)

## Step 4: Read Matching Tests
Inspect the test suite for the affected area:
- **Server logic**: [Web/Web.ServerTests/Services/](../../Web/Web.ServerTests/Services/)
- **Repositories**: [Web/Web.ServerTests/Repositories/](../../Web/Web.ServerTests/Repositories/)
- **Integration flows**: [Web/Web.Server.IntegrationTests/](../../Web/Web.Server.IntegrationTests/)
- **Shared services**: [Services.UnitTests/](../../Services.UnitTests/)

## Step 5: Create a Detailed Plan
Summarize your findings and propose a plan that includes:
1. **Current Behavior**: How the system handles this today (before the feature)
2. **Affected Layers**: Which controllers, services, rules, repositories, entities, DTOs, SignalR hubs, background services, and frontend components are involved
3. **Invariants and Risks**: Which assumptions must be preserved and what could break
4. **End-to-End Flow**: How the feature moves through API → service → rule engine → persistence → SignalR → client
5. **Test Coverage**: Which existing tests prove current behavior and which new tests the feature requires
6. **Implementation Sequence**: The order of changes across layers
7. **Breaking Changes**: Whether the feature changes API contracts, database schema, or client/server behavior
