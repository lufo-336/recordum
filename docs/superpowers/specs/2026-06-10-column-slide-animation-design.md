# Column Slide Animation — Design (#1 polish)

> **Status:** approved (2026-06-10). Part of the Phase 4 polish cluster, filone #1 of 3 (animation → habit visual identity → calendar redesign).

## Problem

Opening the workspace column (`HabitScreenColumn`) or the editor column (`EditorPaneColumn`) is **laggy**, especially over RDP / in a VM.

**Root cause (confirmed from code):** `MainWindow.AnimateColumn` animates `ColumnDefinition.WidthProperty` via the custom `GridLengthAnimation`. Animating a **layout** property forces WPF to run a full **measure + arrange pass over the entire subtree hosted in the column on every animation frame** (~11 frames over 180 ms). For `HabitScreen` that subtree is the heaviest in the app (calendar's 42-button `UniformGrid`, quick-add, completion list; charts when the Detail tab is realized). Layout animation is not GPU-composited and runs on the UI thread — the dominant cost.

**Environment multiplier:** under RDP/VM, `RenderCapability.Tier == 0` (software rendering), which makes any animation heavier. Not the primary cause, but it amplifies the per-frame layout cost.

## Goal

Make the **open** slide smooth ("Obsidian-like") by eliminating per-frame layout, while keeping the **close** smooth. No new user-facing options — purely how the existing slide is rendered.

## Approach (chosen: transform-based slide)

Stop animating the column width on open. Instead set the final width once (a single layout pass), then slide the column's **content** into place with an animated `TranslateTransform` — composition only, no per-frame layout.

### Open (column `0 → W`)

1. Set `column.Width = new GridLength(W)` immediately → **one** layout pass; the sibling Home (`*`) column resizes once.
2. The content element (`HabitScreen` / `EditorPane`, both already `ClipToBounds="True"`) gets a `TranslateTransform`. Set `X = W` (content shifted fully past the right clip edge → invisible), then animate `X: W → 0` over **160 ms**, `CubicEase{EaseOut}`, `FillBehavior.Stop`. On `Completed`, set `X = 0` so the settled base value survives the Stop-fill.
   - Setting `X = W` *before* `BeginAnimation` avoids a one-frame flash of the content at its final position.
   - Visual result: the panel slides in from the right edge; the momentary gap on the left shows the window background. Acceptable, modern "slide-in" feel.

### Close (column `W → 0`)

- The content collapses on its own via the existing `Visibility` binding (`SelectedRow` / `Editor.HasNode` → `BoolToVisible`/`NullToVisibility`), which fires at the same time as the width change.
- With the content collapsed, the column is **empty**, so the existing `GridLengthAnimation` on `WidthProperty` is now **cheap** (no heavy subtree to re-layout). Keep it for close so the Home column re-expands smoothly. `GridLengthAnimation.cs` is **retained** for this path.

### Why this is faster even under software rendering

The win is not GPU acceleration — it's removing the per-frame `measure/arrange` of the heavy subtree. A `TranslateTransform` animation only re-rasterizes/blits; it does not invalidate layout. So it is substantially smoother on RDP/Tier-0, even if not buttery on weak hardware.

## Files

- `src/Recordum.WPF/MainWindow.xaml` — add `x:Name="HabitScreenContent"` to the `<vhs:HabitScreen>` and `x:Name="EditorPaneContent"` to the `<v:EditorPane>` elements (Grid.Column 2 and 4).
- `src/Recordum.WPF/MainWindow.xaml.cs` — rework `AnimateColumn` to take the content `FrameworkElement`; implement the open (transform) / close (existing GridLength) split; add a small `EnsureTranslate` helper. Update `ApplyEditorPaneWidth` / `ApplyHabitScreenWidth` to pass the content element.
- `src/Recordum.WPF/Controls/GridLengthAnimation.cs` — **unchanged** (still used by the close path).

### Sketch (illustrative, not final code)

```csharp
private void ApplyHabitScreenWidth()
    => AnimateColumn(HabitScreenColumn, HabitScreenContent,
                     _viewModel.Home.SelectedRow is not null ? _viewModel.HabitScreenWidth : 0);

private static void AnimateColumn(ColumnDefinition column, FrameworkElement content, double target)
{
    double current = column.ActualWidth;
    if (Math.Abs(current - target) < 0.5) return;

    if (target > 0.5)
    {
        // Open: reserve the width once, slide the content in (no per-frame layout).
        column.Width = new GridLength(target);
        var tt = EnsureTranslate(content);
        tt.X = target;
        var slide = new DoubleAnimation
        {
            From = target, To = 0,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        };
        slide.Completed += (_, _) => tt.X = 0;
        tt.BeginAnimation(TranslateTransform.XProperty, slide);
    }
    else
    {
        // Close: content collapses via its Visibility binding; the empty column shrinks cheaply.
        column.Width = new GridLength(0);
        var collapse = new GridLengthAnimation
        {
            From = current, To = 0,
            Duration = TimeSpan.FromMilliseconds(160),
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        column.BeginAnimation(ColumnDefinition.WidthProperty, collapse);
    }
}

private static TranslateTransform EnsureTranslate(FrameworkElement content)
    => content.RenderTransform as TranslateTransform ?? (TranslateTransform)(content.RenderTransform = new TranslateTransform());
```

> Note: the GridSplitter still resizes the column by writing `ActualWidth` directly on drag-completed; `FillBehavior.Stop` on both animations keeps that working (no held value overrides later splitter changes).

## Edge cases

- **Re-selecting another habit while one is open**: `target == current == W` → the `< 0.5` guard makes it a no-op; the content's `DataContext` swaps with no re-slide. Correct.
- **Startup with nothing selected**: `target == 0`, `current == 0` → no-op.
- **Splitter drag**: unaffected; widths are persisted on `DragCompleted` as before.

## Testing

- **No new unit test.** `AnimateColumn` is view-layer animation glue in code-behind (the current `AnimateColumn` has no unit test either — same precedent). The pure, testable helper `ClampToWorkArea` is untouched and its tests stay green.
- **Build:** `dotnet build src/Recordum.WPF` must stay 0 warning / 0 error.
- **Visual verification (skill `run`):**
  1. Selecting a habit slides the workspace in from the right, smoothly, with no jank (test both Light and Dark; over RDP if that's the usage).
  2. Deselecting collapses it smoothly (Home re-expands).
  3. Opening the editor pane slides smoothly.
  4. The GridSplitter still resizes both columns, and the size persists across reselect/restart.
  5. Switching between two habits does not replay the slide.

## Out of scope

- Render-tier-conditional behavior (the rejected option C) — revisit only if the transform slide still feels janky on the target RDP hardware after verification.
- Any change to the calendar or hover accents (filoni #2 and #3, separate specs).
```