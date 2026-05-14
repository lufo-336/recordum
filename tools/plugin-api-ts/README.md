# @recordum/plugin-api

TypeScript ambient declarations for the Recordum plugin runtime.

> **Phase 3 status:** types only. The actual runtime (WebView2 host + bridge) ships in Phase 7. These declarations let first-party consumers (Chart Engine in Milestone 3.9, Export System in 3.10) author against the same contract that external plugins will use later.

## What ships here

| Path | Contents |
|---|---|
| `manifest.schema.json` | Canonical JSON Schema for the plugin `manifest.json`. Mirrors `Recordum.Plugins.Contracts.Manifest.PluginManifest`. |
| `src/index.d.ts` | Ambient declarations for the global `recordum` namespace (API modules, events, extension points, permissions). |

## How a plugin would consume this

```ts
/// <reference types="@recordum/plugin-api" />

recordum.events.on("habit.created", async (event) => {
  recordum.log.info(`Habit ${event.payload.habitId} appeared`);
});

await recordum.extensionPoints.register({
  pointId: "chart.renderer",
  id: "my-chart",
  render: (host, series) => { /* … */ },
});
```

## Versioning

Following semver. The `0.1.0-alpha.x` line is the Phase 3 contract preview — breaking changes expected before `1.0.0`. The package is currently `"private": true` and is published from npm only once the runtime ships (Phase 7).

## Generation

The TypeScript declarations are hand-written in Phase 3 and are kept in sync with `Recordum.Plugins.Contracts` by review. Phase 7 will introduce automated generation from the C# contracts.
