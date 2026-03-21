---
description: "Use when planning or changing the shared Services library, telemetry deserializers, throttling, subscriber integrations, configuration helpers, resilience policies, or ConsoleApp interactions. Covers files under Services and ConsoleApp."
name: "Shared Services Instructions"
applyTo: ["Services/**", "ConsoleApp/**"]
---
# Shared Services Instructions

- The `Services` project contains shared non-web logic such as telemetry deserialization, telemetry throttling, subscriber integrations, logging helpers, and resilience policies.
- Keep protocol-specific parsing and message-shaping logic in the shared library instead of duplicating it in server or client code.
- `ConsoleApp` depends on this shared behavior, so changes here may affect desktop telemetry ingestion paths even when web features are the visible goal.
- Be conservative with telemetry throttling and packet parsing changes because small logic shifts can alter downstream map pin creation frequency or data quality.
- Preserve resilience and retry behavior defined through the existing Polly policy helpers unless there is a clear reason to change them.
- When changing shared behavior, check `Services.UnitTests` and the downstream server flows that consume the affected types or services.
