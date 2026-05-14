# Recordum plugin API

> **Status — Phase 3 (2026-05).** This document describes the contract that ships in Milestone 3.1. The runtime that loads external plugins (`.rmpk` packages, WebView2 host, custom-scheme bridge, permission UI) ships in **Phase 7**. Phase 3 publishes the contract — C# types in `Recordum.Plugins.Contracts`, TypeScript types in `tools/plugin-api-ts/`, the manifest JSON Schema, and a single in-process host — so that first-party features built between now and Phase 7 (Chart Engine in 3.9, Export System in 3.10) consume the same surface external plugins will use later.

For the design rationale see [`docs/superpowers/specs/2026-05-13-plugin-system-design.md`](./superpowers/specs/2026-05-13-plugin-system-design.md). This file is the developer-facing reference. The spec is authoritative when the two disagree.

---

## Package layout (`.rmpk`)

```
manifest.json        Required. See manifest.schema.json.
main.js              ES2020 entry point. Loaded inside the WebView2 sandbox.
icon.svg             Optional. Shown in marketplace and settings.
README.md            Optional but recommended.
LICENSE              Required to earn the Open Source badge.
locales/<lang>.json  Optional translations. Loaded by recordum.i18n.t().
assets/              Optional bundled static assets (images, etc.).
```

The `.rmpk` extension is a plain zip. Reserved for the `Plugin` package type today; the same envelope is reused in Phase 7+ for `Template` and `Theme` packages.

## Manifest

The manifest is validated against [`tools/plugin-api-ts/manifest.schema.json`](../tools/plugin-api-ts/manifest.schema.json). Required fields:

- `schema_version` — currently `"1"`.
- `id` — reverse-DNS identifier (`io.recordum.chart-engine`). Case-sensitive.
- `name` — display name.
- `version` — semver 2.0.
- `publisher.id`, `publisher.display_name` — verified at submission time against the publisher's GitHub identity claim.
- `min_recordum_version` — the app refuses to load plugins newer than itself.
- `entry` — relative path to the JS entry point.

Optional fields cover marketplace metadata (`description_key`, `icon`, `source_url`, `supported_locales`), the permission declaration (`permissions`), and the list of extension points the plugin contributes to (`extension_points`).

## Permission model

Four categories. Each is declared statically in the manifest; some require a runtime prompt the first time they are exercised.

| Category | Manifest field | Phase 3 behaviour | Phase 7 behaviour |
|---|---|---|---|
| Data | `permissions.data: DataPermission[]` | First-party callers granted unconditionally by `AlwaysAllowPermissionBroker`. | Install-time consent dialog; runtime broker filters records by `ai_data_sharing` for any API caller with `network` permissions. |
| Network | `permissions.network: string[]` | N/A — no plugin runtime. | Host whitelist enforced; `localhost` and private IPs always denied. `["*"]` triggers a red-flag warning. |
| Filesystem | `permissions.filesystem: "None" \| "PluginStorage" \| "AttachmentsRead" \| "UserPick"` | N/A. | `PluginStorage` implicit (10 MB cap). `UserPick` requires user-initiated picker every call. |
| AI | `permissions.ai: "None" \| "Local"` | N/A. | `Local` permits on-device inference. The three-tier `ai_data_sharing` filter still applies and can never be bypassed by a plugin. |

The complete list of well-known permissions lives in [`Recordum.Plugins.Contracts.Permissions.WellKnownPermissions`](../src/Recordum.Plugins.Contracts/Permissions/WellKnownPermissions.cs) and is mirrored as `DataPermission` in `index.d.ts`.

## Extension points

A plugin contributes capability via well-known extension points. The host advertises 9 points in Phase 3:

| ID | What it contributes |
|---|---|
| `dashboard.widget` | A widget rendered on the home screen. |
| `chart.renderer` | A chart implementation. The Chart Engine ships in 3.9 as the pilot consumer. |
| `habit.detail.tab` | An extra tab on the habit detail screen. |
| `completion.input` | A custom completion input control. |
| `settings.panel` | A panel under the Settings screen. |
| `command-palette` | A command registered in the global palette. |
| `export.format` | A custom export format (3.10 ships PDF/HTML/Markdown/JSON/CSV as first-party consumers). |
| `sync.provider` | A cloud sync target. Activated in Phase 5. |
| `data.importer` | An import source for backfilling history. |

Plugins call `recordum.extensionPoints.register(...)` from `main.js` after they have completed any one-shot setup work.

## Events

A plugin subscribes via `recordum.events.on(name, handler)`. The host emits 12 well-known events in Phase 3:

- `habit.created`, `habit.updated`, `habit.deleted`
- `completion.recorded`, `completion.updated`, `completion.deleted`
- `note.created`, `note.updated`
- `streak.milestone`, `goal.completed`
- `app.theme.changed`, `app.locale.changed`

The full list lives in [`Recordum.Plugins.Contracts.Events.WellKnownEvents`](../src/Recordum.Plugins.Contracts/Events/WellKnownEvents.cs).

Phase 7 will require the matching `data:read:*` permission for any event sourced from a domain entity — e.g. subscribing to `habit.created` requires `read:habit-nodes`.

## API surface

The global `recordum` object exposes 14 modules. All methods are async (`Promise`-returning). Errors are thrown as one of three families:

- `PermissionDeniedError` — plugin bug; missing manifest declaration.
- `ValidationError` — caller-recoverable; bad parameter shape or value.
- `HostUnavailableError` — host shutting down; plugin should retry or give up.

| Module | Purpose |
|---|---|
| `habits` | Read habit and group nodes. |
| `completions` | Read and record completions. |
| `notes` | Read notes attached to a habit/group. |
| `goals` | Read goals. |
| `attachments` | Read attachment blobs. |
| `tags` | Read tag definitions. |
| `templates` | Read habit and export templates. |
| `settings` | Read public settings (sensitive keys are never exposed). |
| `ui` | Post local notifications, register palette commands. |
| `events` | Subscribe to host events. |
| `storage` | Plugin-scoped 10 MB key-value store. |
| `i18n` | Lookup localized strings from `locales/<lang>.json`. |
| `network` | Whitelisted HTTPS fetch (Phase 7+). |
| `log` | Structured log to the plugin diagnostics pane. |
| `extensionPoints` | Register an extension. |

The TypeScript declarations are in [`tools/plugin-api-ts/src/index.d.ts`](../tools/plugin-api-ts/src/index.d.ts).

## Lifecycle

1. **Install.** The marketplace downloads the `.rmpk`. The host validates the manifest, extracts to `%LocalAppData%/Recordum/plugins/<id>/`, and leaves the plugin **disabled**.
2. **Enable.** The user grants the install-time permission set. The host calls `IPluginManager.EnableAsync(manifest)` which dispatches to `IPluginHost.LoadAsync(manifest)` (Phase 3: `InProcessPluginHost`; Phase 7: `WebView2PluginHost`).
3. **Runtime.** The plugin runs `main.js`. It may register extensions and event handlers. The host enforces sandboxing and permissions for every API call.
4. **Update.** A new version is installed silently if its permission set is a subset of the current one. Otherwise the user is prompted with a diff.
5. **Disable.** `IPluginManager.DisableAsync(pluginId)` unregisters every extension owned by the plugin and unloads the runtime.
6. **Uninstall.** The package is removed. **Data the plugin created stays.** It's user data, not plugin data — owned by the user even after the plugin is gone.

## Authoring a first-party consumer (Phase 3)

Until the WebView2 runtime ships, first-party features consume the API directly in C#:

```csharp
public sealed class ChartEngineExtension : IExtensionPoint { /* … */ }

await pluginManager.EnableAsync(new PluginManifest
{
    SchemaVersion = "1",
    Id = "io.recordum.chart-engine",
    Name = "Chart Engine",
    Version = "0.1.0",
    Publisher = new ManifestPublisher { Id = "recordum", DisplayName = "Recordum" },
    MinRecordumVersion = "0.1.0",
    Entry = "in-process",
});

extensionRegistry.Register(new ExtensionRegistration(
    pointId: WellKnownExtensionPoints.ChartRenderer,
    pluginId: "io.recordum.chart-engine",
    extension: new ChartEngineExtension()));
```

The same pattern is what the Phase 7 WebView2 host uses internally — it proxies `recordum.extensionPoints.register(...)` calls from `main.js` into a C#-side `IExtensionPointRegistry.Register(...)`. First-party consumers that follow this pattern today are migration-free when the JavaScript runtime arrives.

## Versioning

The `@recordum/plugin-api` TypeScript package is `0.x` until the runtime ships. Breaking changes are expected. The C# contracts (`Recordum.Plugins.Contracts`) follow the app's version, not the TypeScript package's. Once Phase 7 ships, both will graduate to `1.0.0`.
