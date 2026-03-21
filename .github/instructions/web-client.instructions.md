---
description: "Use when planning or changing the React frontend, map UI, tracked pin UX, SignalR client behavior, client-side refresh logic, or files under Web/web.client/src. Covers where frontend state, API calls, and map behavior live in this repo."
name: "Web Client Instructions"
applyTo: "Web/web.client/src/**"
---
# Web Client Instructions

- The frontend lives in `Web/web.client` and is built with React 19, TypeScript, and Vite.
- Start from `src/App.tsx`, then inspect `src/components`, `src/views`, `src/hooks`, and `src/services` for the actual feature surface.
- Put HTTP and SignalR access in `src/services` rather than embedding request logic directly inside components.
- Keep domain types centralized in `src/types` when a shape is reused across views or services.
- Treat map pin rendering, beacon label rendering, tracked pin state, and stale-refresh behavior as correctness-sensitive UI behavior.
- When a frontend change depends on server semantics, read the matching server service and tests before proposing the client change.
- For tracked-pin behavior, also read `TRACKED_PINS_IMPLEMENTATION.md` so planning stays aligned with the current persistence model.
- Preserve the current client/server split: server owns business rules and persistence, client owns presentation, user interactions, and real-time display updates.
