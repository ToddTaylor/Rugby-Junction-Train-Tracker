---
description: "Use when planning or changing the shared Services library, telemetry deserializers, throttling, subscriber integrations, configuration helpers, resilience policies, or ConsoleApp interactions. Covers files under Services and ConsoleApp."
name: "Shared Services Instructions"
applyTo: "Services/**,ConsoleApp/**"
---
# Shared Services Instructions

- The `Services` project contains shared non-web logic such as telemetry deserialization, telemetry throttling, subscriber integrations, logging helpers, and resilience policies.
- Keep protocol-specific parsing and message-shaping logic in the shared library instead of duplicating it in server or client code.
- `ConsoleApp` depends on this shared behavior, so changes here may affect desktop telemetry ingestion paths even when web features are the visible goal.
- Be conservative with telemetry throttling and packet parsing changes because small logic shifts can alter downstream map pin creation frequency or data quality.
- Preserve resilience and retry behavior defined through the existing Polly policy helpers unless there is a clear reason to change them. `PollyPolicies.IsTransient` decides what is retried: no connection, timeouts, 5xx, 408 and 429. Throw `HttpRequestException` with the response's status code so a refusal is not retried.
- Send HTTP requests through `SharedHttpClient.Instance` (10 second timeout). Do not create an `HttpClient` per request.
- Packet handlers run on the log reader's thread. Filter and throttle there, then hand the send to `TelemetrySendQueue.ForSubscriber(id)`; never wait on a request in a handler, or one slow API holds up every subscriber.
- Parse log text with `CultureInfo.InvariantCulture`; beacon PCs can have any regional settings.
- When changing shared behavior, check `Services.UnitTests` and the downstream server flows that consume the affected types or services.
