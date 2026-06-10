# Column Slide Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the workspace/editor column **open** slide smoothly by sliding the column's content with an animated `TranslateTransform` instead of animating `ColumnDefinition.Width` (which re-runs full layout every frame); keep the **close** smooth via the existing cheap empty-column width animation.

**Architecture:** `MainWindow.AnimateColumn` is reworked to take the content `FrameworkElement`. On open it reserves the final column width once (one layout pass) and animates the content's `TranslateTransform.X` from `width → 0` (composition only, no per-frame layout). On close, the content collapses via its existing `Visibility` binding and the empty column shrinks with the retained `GridLengthAnimation`. The two content elements get `x:Name`s so code-behind can reach them.

**Tech Stack:** WPF + CommunityToolkit.Mvvm.

**Spec:** `docs/superpowers/specs/2026-06-10-column-slide-animation-design.md`

**Repo conventions (important):**
- **The WPF app locks its binaries while running** — ensure it is closed before building: `Get-Process Recordum.WPF -ErrorAction SilentlyContinue | Stop-Process -Force`.
- `dotnet build src/Recordum.WPF` must stay **0 warning / 0 error** (TreatWarningsAsErrors on src).
- Full WPF suite: `dotnet test tests/Recordum.WPF.Tests --nologo`. A pre-existing flaky test (`HomeViewModelTests.CompletionRecorded_event_reloads_the_list_for_visual_feedback`) fails intermittently only in the full suite; confirm green by re-running or running the class in isolation.
- Commit messages in English, footer `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- **No unit test in this plan:** `AnimateColumn` is view-layer animation glue in code-behind (the current one has no unit test either). Verification is build (0/0), the unchanged `ClampToWorkArea` tests staying green, and visual verification (Task 3).

---

## File Structure

- `src/Recordum.WPF/MainWindow.xaml` — **modify**: add `x:Name` to the `HabitScreen` and `EditorPane` elements.
- `src/Recordum.WPF/MainWindow.xaml.cs` — **modify**: rework `AnimateColumn` (open = transform slide, close = existing width animation) + add `EnsureTranslate` helper; update the two `Apply…Width` callers.
- `src/Recordum.WPF/Controls/GridLengthAnimation.cs` — **unchanged** (still used by the close path).

---

## Task 1: Name the content elements

**Files:**
- Modify: `src/Recordum.WPF/MainWindow.xaml`

- [ ] **Step 1: Add `x:Name` to the HabitScreen element**

In `src/Recordum.WPF/MainWindow.xaml`, the `<vhs:HabitScreen Grid.Column="2" …>` element currently opens with `Grid.Column="2"` then `ClipToBounds="True"`. Add an `x:Name` as the first attribute so it reads:

```xml
                <vhs:HabitScreen x:Name="HabitScreenContent"
                                 Grid.Column="2"
                                 ClipToBounds="True"
                                 DataContext="{Binding HabitScreen}"
                                 Visibility="{Binding Home.SelectedRow,
                                              RelativeSource={RelativeSource AncestorType=Window},
                                              Converter={StaticResource NullToVisibility}}"/>
```

- [ ] **Step 2: Add `x:Name` to the EditorPane element**

Similarly, change `<v:EditorPane Grid.Column="4" …>` to:

```xml
                <v:EditorPane x:Name="EditorPaneContent"
                              Grid.Column="4"
                              ClipToBounds="True"
                              DataContext="{Binding Editor}"
                              Visibility="{Binding Editor.HasNode, Converter={StaticResource BoolToVisible}}"/>
```

- [ ] **Step 3: Build — verify 0/0**

First ensure the app is closed: `Get-Process Recordum.WPF -ErrorAction SilentlyContinue | Stop-Process -Force`
Run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`. (A misnamed element or duplicate `x:Name` shows up here as a XAML error.)

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/MainWindow.xaml
git commit -m "refactor(wpf): name HabitScreen/EditorPane content elements for slide animation"
```

---

## Task 2: Transform-based open slide in AnimateColumn

**Files:**
- Modify: `src/Recordum.WPF/MainWindow.xaml.cs`

- [ ] **Step 1: Add the `TranslateTransform` using directive**

In `src/Recordum.WPF/MainWindow.xaml.cs`, the file already has `using System.Windows.Media.Animation;`. Add (alphabetically near it):

```csharp
using System.Windows.Media;
```

(`TranslateTransform` lives in `System.Windows.Media`; `DoubleAnimation`/`CubicEase` in `System.Windows.Media.Animation`, already imported.)

- [ ] **Step 2: Update the two callers to pass the content element**

Replace the existing two `Apply…Width` methods:

```csharp
    private void ApplyEditorPaneWidth()
        => AnimateColumn(EditorPaneColumn, _viewModel.Editor.HasNode ? _viewModel.EditorPaneWidth : 0);

    private void ApplyHabitScreenWidth()
        => AnimateColumn(HabitScreenColumn, _viewModel.Home.SelectedRow is not null ? _viewModel.HabitScreenWidth : 0);
```

with:

```csharp
    private void ApplyEditorPaneWidth()
        => AnimateColumn(EditorPaneColumn, EditorPaneContent, _viewModel.Editor.HasNode ? _viewModel.EditorPaneWidth : 0);

    private void ApplyHabitScreenWidth()
        => AnimateColumn(HabitScreenColumn, HabitScreenContent, _viewModel.Home.SelectedRow is not null ? _viewModel.HabitScreenWidth : 0);
```

- [ ] **Step 3: Replace the `AnimateColumn` body**

Replace the entire existing `AnimateColumn` method (the one with the comment "Slides a column open/closed instead of snapping…" and its body that sets `column.Width` then begins a `GridLengthAnimation`) with:

```csharp
    // Opens a column by sliding its content in (animated TranslateTransform — composition only, no
    // per-frame layout), and closes it by shrinking the now-empty column with the cheap width animation.
    // Sets the target width as the base (local) value and uses Stop-fill animations so the GridSplitter
    // can still resize the column afterwards (a held animation would override later width changes).
    private static void AnimateColumn(ColumnDefinition column, FrameworkElement content, double target)
    {
        double current = column.ActualWidth;
        if (Math.Abs(current - target) < 0.5) return;

        if (target > 0.5)
        {
            // Open: reserve the final width once (single layout pass), then slide the content in from
            // the right. Content is ClipToBounds, so the off-screen start position is invisible.
            column.Width = new GridLength(target);
            var tt = EnsureTranslate(content);
            tt.X = target; // set before BeginAnimation to avoid a one-frame flash at the final position
            var slide = new DoubleAnimation
            {
                From = target,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(160),
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            slide.Completed += (_, _) => tt.X = 0; // settle the base value after Stop-fill
            tt.BeginAnimation(TranslateTransform.XProperty, slide);
        }
        else
        {
            // Close: the content collapses via its Visibility binding; the empty column shrinks cheaply.
            column.Width = new GridLength(0);
            var collapse = new GridLengthAnimation
            {
                From = current,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(160),
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            column.BeginAnimation(ColumnDefinition.WidthProperty, collapse);
        }
    }

    private static TranslateTransform EnsureTranslate(FrameworkElement content)
        => content.RenderTransform as TranslateTransform
           ?? (TranslateTransform)(content.RenderTransform = new TranslateTransform());
```

- [ ] **Step 4: Build — verify 0/0**

Ensure the app is closed, then run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`.

- [ ] **Step 5: Confirm the pure-logic tests still pass**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~ClampToWorkArea" --nologo`
Expected: PASS (the `ClampToWorkArea` tests are untouched but prove `MainWindow` still compiles for tests).

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/MainWindow.xaml.cs
git commit -m "perf(wpf): slide columns open via TranslateTransform instead of layout-animating Width"
```

---

## Task 3: Verify + tracker

**Files:**
- Modify: `docs/phase-4-issues.md`

- [ ] **Step 1: Full WPF suite**

Run: `dotnet test tests/Recordum.WPF.Tests --nologo`
Expected: PASS. If the only failure is `HomeViewModelTests.CompletionRecorded_event_reloads_the_list_for_visual_feedback`, re-run the class in isolation (`--filter "FullyQualifiedName~HomeViewModelTests"`) to confirm it's the known flake.

- [ ] **Step 2: Visual verification (skill `run`)**

Launch the app. Confirm:
1. Selecting a habit slides the workspace column in from the right, smoothly, with no jank (Light and Dark; over RDP if that is the real usage).
2. Deselecting collapses it smoothly (the Home column re-expands).
3. Opening the editor pane (Configura / new habit) slides smoothly.
4. The GridSplitter still resizes both the workspace and editor columns, and the size persists across reselect and across an app restart.
5. Switching directly between two habits does **not** replay the slide (no flicker).

- [ ] **Step 3: Update the tracker**

In `docs/phase-4-issues.md`, find the `#10 slide colonna` entry (currently `fixed`, commit `3897ddc`). Append a note that the slide was reworked to be transform-based to remove per-frame layout jank, with the new commit hash and the spec path `2026-06-10-column-slide-animation-design.md`.

- [ ] **Step 4: Commit**

```bash
git add docs/phase-4-issues.md
git commit -m "docs(phase-4): #10 column slide reworked transform-based (smoother open)"
```

---

## Self-Review (result)

- **Spec coverage:** open transform slide → Task 2 Step 3 (the `target > 0.5` branch); close via empty-column width animation → Task 2 Step 3 (`else` branch); `x:Name` on content elements → Task 1; `GridLengthAnimation.cs` retained → unchanged, used by the close branch; testing approach (no unit test, build + ClampToWorkArea + visual) → Tasks 2–3. All spec sections covered.
- **Placeholder scan:** no TBD/TODO; every code step shows the full code to write; commands have expected output.
- **Type consistency:** `AnimateColumn(ColumnDefinition, FrameworkElement, double)` signature matches both callers in Step 2; `EnsureTranslate(FrameworkElement) : TranslateTransform` used in the open branch; `x:Name`s `HabitScreenContent` / `EditorPaneContent` defined in Task 1 and consumed in Task 2 Step 2; `GridLengthAnimation` properties (`From`/`To`/`Duration`/`FillBehavior`/`EasingFunction`) match the existing class in `Controls/GridLengthAnimation.cs`.
- **Known risk:** if the transform slide still feels janky on the target RDP hardware after Task 3, the spec's out-of-scope option C (render-tier-conditional shortening) is the follow-up — do not pre-implement it.
```