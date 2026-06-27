# ADR 0001 — Desktop UI framework: WPF (not Avalonia)

- **Status**: Accepted
- **Date**: 2026-06-27 (rationale reconstructed; the original choice predates Phase 3 and was never written down)
- **Deciders**: Author (ITS Web Solutions Architect)
- **Context tags**: architecture, UI, cross-platform

## Context

Recordum's platform strategy is **shared logic + native UI per platform**, not a single
shared UI across operating systems. `Recordum.Core`, `Recordum.Data`,
`Recordum.Plugins.Contracts` and `Recordum.Plugins` are reused unchanged across every
platform; each platform gets its own UI layer on top:

| Platform | Phase | UI technology |
|---|---|---|
| Desktop | 3 → v1.0 | **WPF** |
| Mobile | 5 (was 7) | .NET MAUI |
| Web | Future | ASP.NET Core + React/Svelte PWA |

The roadmap freeze (2026-06-16) is explicit: **a working, pleasant Windows desktop v1.0
before expanding the platform.** Cross-OS *desktop* (macOS/Linux desktop builds) is **not**
a goal at any phase — mobile is reached via MAUI, web via a PWA.

## Decision

Use **WPF** for the desktop UI. Do **not** adopt Avalonia.

## Rationale

Avalonia's headline advantage is *one desktop UI that also runs on macOS and Linux*. Given
the platform strategy above, that advantage buys nothing here — there is no planned cross-OS
desktop target. Weighed against that, WPF wins on the factors that actually matter for this
project:

1. **WebView2 + Toast UI Editor.** The Markdown editor (Journal/Notes) is built on
   **WebView2**, which is first-class and mature on WPF/Windows. Avalonia's WebView support
   is third-party and far less solid — an unacceptable risk on a core feature.
2. **Windows-first, ship v1.0 first.** For a single target platform, WPF is the
   lowest-risk, best-supported path.
3. **Mature ecosystem/tooling.** LiveCharts2, QuestPDF, designer tooling, and the broad
   component ecosystem are all battle-tested on WPF.
4. **Continuity.** Phases 0–2 prototyping was already WPF — existing in-house experience.

Non-differentiators (work on both, so they did not drive the choice): `CommunityToolkit.Mvvm`,
EF Core + SQLite + SQLCipher — all platform-agnostic.

## Consequences

- We give up native macOS/Linux **desktop** builds. This is acceptable: they were never on
  the roadmap, and mobile/web are covered by MAUI and a PWA respectively.
- The desktop UI is Windows-only and tied to the Windows compositor and WebView2.
- Should a cross-OS desktop ever become a goal, this ADR would need to be revisited
  (Avalonia or Uno would be the candidates to re-evaluate then).

## When to revisit

Only if a **cross-OS desktop** (macOS/Linux as first-class desktop apps) becomes an actual
product goal. As long as the strategy stays "native UI per platform + shared Core", WPF
remains the right desktop choice.
