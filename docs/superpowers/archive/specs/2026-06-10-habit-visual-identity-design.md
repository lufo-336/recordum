# Habit Visual Identity (#5) — Design

> Phase 4 polish. Make habits stand out from the background and carry a consistent color
> identity from the Home list into the open workspace. Companion to the icon display
> (commit `66d350e`) and the live color refresh (commit `3015f19`).

**Status**: design approved 2026-06-10 (visual brainstorm via companion).

---

## 1. Problem

Home rows are transparent over the near-black background (`Brush.Background` = `#09090B` in
dark), so habits visually blend into the void; the selected habit has no distinct treatment;
and once a habit's workspace is open, nothing beyond the (newly added) header icon and the
calendar markers reminds the user *which* habit they're looking at.

## 2. Goals

- Each habit row reads as a distinct **block** that carries its own color, subtly.
- The **selected** habit is unmistakable without shouting.
- The open **workspace column** echoes the habit's color.
- Works in **both themes**; restrained per the design brief ("colore = identificatore, mai
  decorazione"). No new density, no heatmap redesign, no animation (slide already shipped).

## 3. Visual decisions (from the companion)

| Aspect | Choice |
|---|---|
| Resting row block | **Subtle tint** — habit color at ~10% alpha over the surface |
| Selection | **Inner border + stronger tint** — 1.5px inset border in the habit color + tint ~20% |
| Workspace accents | **Both** — 3px top strip in the habit color + active tab in the habit color |
| Accent bar | **Kept** — the existing 4px left accent bar stays alongside the tinted block (brief's signature element) |
| Icon | Kept (already shipped) |

## 4. Design

### 4.1 Home row block (resting)
Wrap the habit row content in a `Border` ("the block") with:
- `Background` = the habit color at a fixed low alpha (~10%) composited over the row area.
- `CornerRadius` ~6, a small vertical margin so blocks are visually separated.
- The existing **4px accent bar** (left `Rectangle`) and the **icon** are retained inside.

The tint is produced by a converter (see 4.4), not a precomputed brush, so it tracks live
color changes (`HabitUpdated`). Text/heatmap unchanged — legibility is preserved because the
tint is low-alpha.

### 4.2 Selection
A row needs to know it is selected. Today selection lives only in
`HomeViewModel.SelectedRow`; the row VM is unaware.

- Add `HomeRowViewModel.IsSelected` (`[ObservableProperty] bool`).
- `HomeViewModel` keeps it in sync: when `SelectedRow` changes (and on `Rebuild`), set
  `IsSelected = ReferenceEquals(row, SelectedRow)` for every row (clear the old, set the new).
- When `IsSelected` is true, the block: tint rises to ~20% **and** an inset `1.5px`
  border in the full habit color appears (drawn inside the block bounds, so it does not shift
  content).

### 4.3 Workspace accents (HabitScreen)
- **Top strip**: a 3px `Border`/`Rectangle` across the top of the HabitScreen column, filled
  with `Node.Color`.
- **Active tab**: the active tab among Abitudine / Dettaglio / Note shows its label and
  underline in `Node.Color` instead of the generic `Brush.Accent`. Inactive tabs unchanged.
- Both bind to `Node.Color`; the live-refresh wiring (#4, `HabitUpdated`) already reassigns
  `Node`, so these update when the color changes in Configura.

### 4.4 Theming
- A new converter **`HabitTintBrushConverter`** (`IValueConverter`): input habit hex +
  `ConverterParameter` = alpha as a decimal byte 0–255 (e.g. `26` ≈ 10%, `51` ≈ 20%) →
  `SolidColorBrush` of that color at that alpha. Returns a transparent brush for malformed/empty
  input.
- The tint is alpha-over-surface, so it works in **Light and Dark** without separate values;
  the two alpha levels (resting / selected) are the only tuning knobs and live in the XAML
  bindings (easy to retune by eye).
- Selection border = full habit color (`HexToBrush`), legible on both themes.

### 4.5 Groups
`GroupRowTemplate` gets the **same resting tint** of its own `ColorHex` for consistency; no
selection border requirement is added for groups beyond what falls out naturally (groups can
be selected too — same `IsSelected` treatment applies, harmless). No other group-specific
visuals.

## 5. Components touched

- **New**: `Converters/HabitTintBrushConverter.cs` (+ registration in `App.xaml`).
- `ViewModels/HomeRowViewModel.cs` — `IsSelected` observable.
- `ViewModels/HomeViewModel.cs` — sync `IsSelected` on selection change and on `Rebuild`.
- `Views/HabitRowTemplate.xaml` — wrap row in tinted/bordered block.
- `Views/GroupRowTemplate.xaml` — resting tint.
- `Views/HabitScreen/HabitScreen.xaml` — top strip + active-tab color (bound to `Node.Color`).

## 6. Testing

- `HomeViewModel` sets `IsSelected` on the right row when `SelectedRow` changes, and clears the
  previous one (unit test).
- `HabitTintBrushConverter` returns the expected ARGB for a given hex + alpha, and a
  transparent brush for malformed input (unit test).
- Visual verification (skill `run`): rows detach from the background with their tint; selected
  row shows the inner border + stronger tint; workspace top strip + active tab carry the color;
  changing the color in Configura updates all of it live; both themes legible.

## 7. Out of scope

Row density/spacing changes, heatmap redesign, per-row animation, group-vs-habit visual
differentiation beyond the shared tint. The broader responsive-layout pass (R-responsiveUI)
is separate.
