# Quick-Add Live Chrono Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the per-cell stopwatch in the counter/measured quick-add a **live ticking display** while it runs (seconds counting up, reusing `LiveElapsedClock` like abstinence/detail) plus a **start↔stop** affordance on the button, so clicking ⏱ no longer looks like "nothing happened".

**Architecture:** The chrono state (`ChronoRunning`, `ToggleChrono`) already lives on `SeriesQuickAddViewModelBase`; only one `active_timer` runs per habit. Add a shared `LiveElapsedClock ChronoClock` to that base, `Start(startedAtMs)` it from the `ActiveTimer` returned by `StartAsync`, `Stop()` it on the second click. Mark the specific `SeriesCellViewModel` being timed with `IsChronoRunning` so only that cell shows the running clock and the stop icon. Make the base `IDisposable` (the `DispatcherTimer` inside `LiveElapsedClock` is rooted by the dispatcher and leaks otherwise) and have `HabitScreenViewModel` dispose the previous quick-add when it swaps or tears down.

**Tech Stack:** C# / .NET 10, WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions.

## Global Constraints

- **English-first strings = 3 edits each:** key in `src/Recordum.WPF/Resources/Strings.resx` (EN) + `Strings.it.resx` (IT) + manual accessor in `src/Recordum.WPF/Resources/Strings.Designer.cs`. The `guard-generated.js` hook denies Edit/Write on `*.Designer.cs` → **edit `Strings.Designer.cs` only via the Bash shell with `sed`** (this VM has no python).
- **`IClock` for time, never `DateTime.Now`.** `LiveElapsedClock` already takes an `IClock`; feed it the base's `Clock` property.
- **Analyzer rules are errors:** CA1859 (concrete types on private fields), CA1822 (make members static when possible), CA1063/CA1816 (proper `Dispose` pattern — for a `sealed` class a plain `public void Dispose()` that disposes owned fields is fine, no `GC.SuppressFinalize` needed since there is no finalizer, but CA1816 may still flag; if it does, add `GC.SuppressFinalize(this);` at the end of `Dispose`).
- **Never `Clear()` a bound ItemsSource while persist handlers can fire** (data-wipe `4d01f9c`) — not exercised here, but keep in mind.
- **WPF binding through the quick-add host:** the per-cell chrono button reaches VM members via `RelativeSource AncestorType=ContentControl` → `Content.<Member>`. Do NOT introduce a nested `ContentControl` around a cell/row (it would shadow that ancestor). `ContentPresenter` is NOT a `ContentControl`, so it does not interfere.
- **Build/test:** `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo`. If the app is running it locks `Recordum.WPF.dll` — kill the specific PID named in the MSB3021 error. `-p:UseAppHost=false` deletes the WPF exe → a later `dotnet run --project src/Recordum.WPF` must run **without** `--no-build`.
- **Branch:** continue on `wave-2-3-registration` (the chrono was reported during that wave's visual pass). The `guard-main.js` hook blocks direct commits on `main`.
- **Author visual verification closes it**, not green tests alone.

## Environment / Interface facts (verified 2026-07-18)

- `SeriesQuickAddViewModelBase` (`src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesQuickAddViewModelBase.cs`): `abstract partial : QuickAddViewModelBase`; fields `IActiveTimerService _timers`, `SeriesCellViewModel? _chronoCell`; `[ObservableProperty] bool _chronoRunning`; ctor `(ICompletionService completions, IClock clock, IActiveTimerService timers)` → `base(completions, clock)`. `ToggleChrono(SeriesCellViewModel? cell, CancellationToken ct)` is a `[RelayCommand]`: start branch guards on `GetActiveAsync != null`, calls `StartAsync(Node.Id, TimerMode.Stopwatch)`, sets `_chronoCell`/`ChronoRunning=true`; stop branch calls `StopWithoutRecordingAsync` → `(_chronoCell ?? cell).SetDurationSeconds(secs)`.
- `QuickAddViewModelBase` exposes `protected IClock Clock { get; }` and `[ObservableProperty] HabitNode? _node`.
- `LiveElapsedClock` (`src/Recordum.WPF/ViewModels/HabitScreen/LiveElapsedClock.cs`, namespace `Recordum.WPF.ViewModels.HabitScreen`): ctor `(IClock)`; `[ObservableProperty] string _display`; `Start(long startedAtMs)`, `Stop()`, `Dispose()`. The `DispatcherTimer` is only created under a running `Application.Current` (null in headless tests) — so in tests `Start`/`Stop` are safe no-ops on the timer and `Display` stays `""`; assert on the flags, not the ticking string.
- `IActiveTimerService.StartAsync(Guid, TimerMode, ct)` returns `ValueTask<ActiveTimer>`; `ActiveTimer.StartedAtMs` (long) is the anchor for the clock. `FakeActiveTimerService` (`tests/Recordum.WPF.Tests/Fixtures/FakeActiveTimerService.cs`) returns an `ActiveTimer` with `StartedAtMs = 0`, records `StartCalls`/`StopWithoutRecordingCalls`, and honors `StopWithoutRecordingFactory`.
- `SeriesCellViewModel` (`src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesCellViewModel.cs`): `sealed partial : ObservableObject`; `bool IsDuration`; `SetDurationSeconds(int)`; ctor `(MetricDefinition metric, string? unitCode = null, ScaleInfo? scale = null)`.
- `HabitScreenViewModel` (`src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`): `sealed partial : ObservableObject, IDisposable`; `[ObservableProperty] QuickAddViewModelBase? _quickAdd` with `partial void OnQuickAddChanged(QuickAddViewModelBase? value) => NotifyQuickAddRouting();`. `Dispose()` (~line 570) already unhooks the abstinence VM event and disposes Notes/Detail/Records/TodayStrip. `BuildQuickAddAsync` assigns `QuickAdd = vm`.
- The per-cell chrono button + duration block live in `MetricCellTemplate` (`src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` ~lines 401-425): a `Button` (Command `Content.ToggleChronoCommand` via `AncestorType=ContentControl`, `CommandParameter={Binding}`, ToolTip `HabitScreen_QuickAdd_ChronoStart`) whose child `Path` uses `{StaticResource Icon.Clock}`; the duration `StackPanel` (TextBox `DurationValue` + unit ComboBox) is just above at ~line 290.
- `Icons.xaml` (`src/Recordum.WPF/Resources/Icons.xaml`): `Icon.Clock` at line 117 (`<Geometry x:Key="Icon.Clock">…</Geometry>`). No stop/square icon yet.
- Strings: `HabitScreen.QuickAdd.ChronoStart` = "Avvia cronometro" family exists (resx line ~536 EN / ~515 IT; Designer accessor `HabitScreen_QuickAdd_ChronoStart` at line 38).

---

## Task 1: Live clock + per-cell running flag in the quick-add base (VM only)

`SeriesQuickAddViewModelBase` gains a `ChronoClock` that ticks between start and stop; the timed `SeriesCellViewModel` carries an `IsChronoRunning` flag so the view can scope the running display + stop icon to exactly that cell. The base becomes `IDisposable` to stop/detach the clock.

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesCellViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesQuickAddViewModelBase.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/QuickAddViewModelTests.cs` (extend the existing chrono test + add a dispose test)

**Interfaces:**
- Consumes: `LiveElapsedClock(IClock)`, `IActiveTimerService.StartAsync → ActiveTimer.StartedAtMs`, `SetDurationSeconds(int)`, `Clock` (base).
- Produces: on `SeriesCellViewModel`: `[ObservableProperty] bool _isChronoRunning`. On `SeriesQuickAddViewModelBase`: `public LiveElapsedClock ChronoClock { get; }`; `IDisposable` with `Dispose()` that stops the timer state and disposes the clock. `ChronoRunning`/`ToggleChrono` unchanged in signature.

- [ ] **Step 1: Failing test.** Replace the body of the existing `Chrono_toggle_fills_the_duration_cell` in `QuickAddViewModelTests` and add a dispose test right after it:

```csharp
[Fact]
public async Task Chrono_toggle_marks_the_cell_running_then_pours_and_clears()
{
    var (vm, timers) = CreateCounterSeriesVmWithTimers();
    await vm.LoadMetricsAsync();
    var cell = vm.Series[0].Cells.First(c => c.IsDuration);

    await vm.ToggleChronoCommand.ExecuteAsync(cell);  // start
    Assert.True(vm.ChronoRunning);
    Assert.True(cell.IsChronoRunning);                 // the timed cell is flagged
    Assert.Single(timers.StartCalls);

    await vm.ToggleChronoCommand.ExecuteAsync(cell);  // stop → 95s (fake factory)
    Assert.False(vm.ChronoRunning);
    Assert.False(cell.IsChronoRunning);                // flag cleared on stop
    Assert.Equal(TimeUnit.Seconds, cell.DurationUnit);
    Assert.Equal(95m, cell.DurationValue);
    Assert.Single(timers.StopWithoutRecordingCalls);
}

[Fact]
public async Task Disposing_the_quick_add_clears_a_running_chrono()
{
    var (vm, _) = CreateCounterSeriesVmWithTimers();
    await vm.LoadMetricsAsync();
    var cell = vm.Series[0].Cells.First(c => c.IsDuration);
    await vm.ToggleChronoCommand.ExecuteAsync(cell);  // start
    Assert.True(vm.ChronoRunning);

    vm.Dispose();

    // ChronoClock display is reset and no exception is thrown (the DispatcherTimer, if any, is detached).
    Assert.Equal(string.Empty, vm.ChronoClock.Display);
}
```

Confirm `CreateCounterSeriesVmWithTimers` sets `timers.StopWithoutRecordingFactory = _ => 95;` — if the existing helper does not, set it in the helper or inline. Add `using` for `Recordum.WPF.ViewModels.HabitScreen.QuickAdd` / `.HabitScreen` if the test file lacks them.

- [ ] **Step 2: Run to verify failure.**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~QuickAddViewModelTests"`
Expected: FAIL — `cell.IsChronoRunning` and `vm.ChronoClock` / `vm.Dispose()` don't exist.

- [ ] **Step 3: Implement the cell flag.** In `SeriesCellViewModel`, add near the other duration members:

```csharp
/// <summary>True while THIS cell's stopwatch is running (U8 live chrono): scopes the running
/// clock readout and the stop icon to the timed cell only. Set by the parent quick-add VM.</summary>
[ObservableProperty] private bool _isChronoRunning;
```

- [ ] **Step 4: Implement the base clock + dispose.** Edit `SeriesQuickAddViewModelBase`:

Add the using for the parent namespace at the top (where `LiveElapsedClock` lives):

```csharp
using Recordum.WPF.ViewModels.HabitScreen;
```

Change the class declaration to implement `IDisposable`:

```csharp
public abstract partial class SeriesQuickAddViewModelBase : QuickAddViewModelBase, IDisposable
```

Add the field/property (after `_chronoCell`):

```csharp
/// <summary>Live-ticking elapsed readout shown next to the running duration cell (U8). One timer
/// per habit, so one shared clock. MUST be disposed with the VM (the DispatcherTimer leaks otherwise).</summary>
public LiveElapsedClock ChronoClock { get; }
```

Assign it in the ctor (after `_timers = timers;`):

```csharp
ChronoClock = new LiveElapsedClock(Clock);
```

Replace the `ToggleChrono` body's start/stop branches so they drive the clock + the cell flag:

```csharp
if (!ChronoRunning)
{
    if (await _timers.GetActiveAsync(Node.Id, ct).ConfigureAwait(true) is not null) return;
    var timer = await _timers.StartAsync(Node.Id, TimerMode.Stopwatch, ct).ConfigureAwait(true);
    _chronoCell = cell;
    cell.IsChronoRunning = true;
    ChronoClock.Start(timer.StartedAtMs);
    ChronoRunning = true;
}
else
{
    var secs = await _timers.StopWithoutRecordingAsync(Node.Id, ct).ConfigureAwait(true);
    var target = _chronoCell ?? cell;
    target.SetDurationSeconds(secs);
    target.IsChronoRunning = false;
    ChronoClock.Stop();
    _chronoCell = null;
    ChronoRunning = false;
}
```

Add `Dispose` at the end of the class:

```csharp
/// <summary>Stops any running chrono and detaches the clock's DispatcherTimer so the VM can be
/// collected when the screen swaps or tears down the quick-add.</summary>
public void Dispose()
{
    if (_chronoCell is not null) _chronoCell.IsChronoRunning = false;
    _chronoCell = null;
    ChronoRunning = false;
    ChronoClock.Dispose();
    GC.SuppressFinalize(this);
}
```

(If CA1063 complains that a public non-virtual `Dispose` on a sealed-hierarchy base is fine — it is; the class is `abstract` but its concrete subclasses are `sealed`. If the analyzer still errors, keep the simple form above; `GC.SuppressFinalize` satisfies CA1816.)

- [ ] **Step 5: Run the tests + full WPF suite.**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo`
Expected: PASS (both new tests green; nothing else regressed).

- [ ] **Step 6: Commit.**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesCellViewModel.cs \
        src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesQuickAddViewModelBase.cs \
        tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/QuickAddViewModelTests.cs
git commit -m "feat(wpf): live-ticking chrono readout + per-cell running flag in the quick-add"
```

---

## Task 2: Dispose the previous quick-add on swap and teardown

The quick-add VM now owns a `DispatcherTimer` (via `ChronoClock`). `HabitScreenViewModel` replaces `QuickAdd` on every habit/date change and never disposes the old one — a leak. Dispose the outgoing VM when a new one is assigned and when the screen is disposed.

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenViewModelTests.cs` (add one test)

**Interfaces:**
- Consumes: `SeriesQuickAddViewModelBase : IDisposable` (Task 1).
- Produces: `OnQuickAddChanged(oldValue, newValue)` disposes `oldValue` when it is `IDisposable`; `Dispose()` disposes the current `QuickAdd` when it is `IDisposable`.

- [ ] **Step 1: Failing test.** In `HabitScreenViewModelTests`, test the swap wiring directly with a tiny disposable stub, so the test doesn't depend on driving a full node-selection flow. Add the stub as a private nested class in the test file and one test that uses the file's existing SUT builder (`CreateSut()` / equivalent — reuse whatever the file already calls to construct `HabitScreenViewModel` with fakes):

```csharp
private sealed class DisposableStubQuickAdd : QuickAddViewModelBase, IDisposable
{
    public DisposableStubQuickAdd() : base(new FakeCompletionService(), new FakeClock()) { }
    public bool Disposed { get; private set; }
    public override Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Dispose() => Disposed = true;
}

[Fact]
public void Assigning_a_new_quick_add_disposes_the_previous_one()
{
    var sut = CreateSut();                         // the file's existing HabitScreenViewModel builder
    var first = new DisposableStubQuickAdd();
    sut.QuickAdd = first;                            // QuickAdd is a public [ObservableProperty] setter

    sut.QuickAdd = new DisposableStubQuickAdd();     // swap

    first.Disposed.Should().BeTrue();
}

[Fact]
public void Disposing_the_screen_disposes_the_current_quick_add()
{
    var sut = CreateSut();
    var current = new DisposableStubQuickAdd();
    sut.QuickAdd = current;

    sut.Dispose();

    current.Disposed.Should().BeTrue();
}
```

Add `using`s for `Recordum.WPF.ViewModels.HabitScreen.QuickAdd` and the `Fixtures` namespace (`FakeCompletionService`/`FakeClock`) if the file lacks them. If `CreateSut()` isn't the exact helper name, use whatever the file already uses to build the SUT — do not add a new heavyweight builder.

- [ ] **Step 2: Run to verify failure.**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~HabitScreenViewModelTests"`
Expected: FAIL — the old quick-add is not disposed today (if the test drives a running chrono, its display would still be non-empty / the timer still attached).

- [ ] **Step 3: Implement.** In `HabitScreenViewModel`, change the generated change-handler to the two-argument overload so it can see the outgoing value, and dispose it. Keep the routing notification:

```csharp
partial void OnQuickAddChanged(QuickAddViewModelBase? oldValue, QuickAddViewModelBase? newValue)
{
    if (oldValue is IDisposable d) d.Dispose();
}

partial void OnQuickAddChanged(QuickAddViewModelBase? value) => NotifyQuickAddRouting();
```

(The MVVM Toolkit calls BOTH the two-arg and one-arg `OnQuickAddChanged` overloads from the generated setter, so routing still fires; the two-arg one only handles disposal.)

In `Dispose()` (~line 570), dispose the current quick-add alongside the existing teardown — add this line next to the other component disposals:

```csharp
if (QuickAdd is IDisposable qd) qd.Dispose();
```

Leave the existing `if (QuickAdd is AbstinenceQuickAddViewModel ab) ab.Changed -= OnAbstinenceChanged;` line as is (AbstinenceQuickAddViewModel is not `IDisposable`, so it is unaffected).

- [ ] **Step 4: Run the test + full WPF suite.**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo`
Expected: PASS.

- [ ] **Step 5: Commit.**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs \
        tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenViewModelTests.cs
git commit -m "fix(wpf): dispose the previous quick-add (chrono timer) on swap and teardown"
```

---

## Task 3: XAML — start↔stop button + live readout next to the running cell

The chrono button toggles its icon (clock → stop square) and tooltip (Avvia → Ferma) on the cell's `IsChronoRunning`; a live elapsed readout appears beside the duration field while it runs, bound to the shared `ChronoClock.Display` and scoped to the running cell.

**Files:**
- Modify: `src/Recordum.WPF/Resources/Icons.xaml` (add `Icon.Square`)
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs` (via sed) — add `HabitScreen.QuickAdd.ChronoStop`
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` (`MetricCellTemplate`: button icon/tooltip triggers + live readout `TextBlock`)

**Interfaces:**
- Consumes: `SeriesCellViewModel.IsChronoRunning`, `SeriesQuickAddViewModelBase.ChronoClock.Display` (via `Content.` on the `AncestorType=ContentControl` host), `Icon.Square`, `Strings.HabitScreen_QuickAdd_ChronoStop`.

- [ ] **Step 1: Add the stop icon.** In `Icons.xaml`, right after the `Icon.Clock` line (117), add a rounded-square "stop" glyph:

```xml
<Geometry x:Key="Icon.Square">M 7 6 h 10 a 1 1 0 0 1 1 1 v 10 a 1 1 0 0 1 -1 1 h -10 a 1 1 0 0 1 -1 -1 v -10 a 1 1 0 0 1 1 -1 z</Geometry>
```

- [ ] **Step 2: Add the string (3 edits).** In `Strings.resx` add after the `HabitScreen.QuickAdd.ChronoStart` block:

```xml
<data name="HabitScreen.QuickAdd.ChronoStop" xml:space="preserve">
  <value>Stop timer</value>
</data>
```

In `Strings.it.resx` add the same key with `<value>Ferma cronometro</value>`. Then add the Designer accessor via the shell (Edit/Write are hook-blocked on `*.Designer.cs`):

```bash
sed -i '/public static string HabitScreen_QuickAdd_ChronoStart => Get("HabitScreen.QuickAdd.ChronoStart");/a\    public static string HabitScreen_QuickAdd_ChronoStop => Get("HabitScreen.QuickAdd.ChronoStop");' src/Recordum.WPF/Resources/Strings.Designer.cs
```

Verify: `grep -n "ChronoStop" src/Recordum.WPF/Resources/Strings.Designer.cs src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx` → 3 hits.

- [ ] **Step 3: Button icon + tooltip toggle.** In `MetricCellTemplate` (HabitScreen.xaml ~line 403), replace the chrono `Button` (through its closing `</Button>`) with a version whose ToolTip and `Path.Data` switch on `IsChronoRunning`:

```xml
<Button Width="28" Height="28" Padding="0" Margin="6,0,0,0" VerticalAlignment="Center"
        Background="Transparent" BorderThickness="0" Cursor="Hand"
        Command="{Binding Content.ToggleChronoCommand, RelativeSource={RelativeSource AncestorType=ContentControl}}"
        CommandParameter="{Binding}">
    <Button.Style>
        <Style TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Setter Property="ToolTip" Value="{x:Static res:Strings.HabitScreen_QuickAdd_ChronoStart}"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsDuration}" Value="True">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
                <DataTrigger Binding="{Binding IsChronoRunning}" Value="True">
                    <Setter Property="ToolTip" Value="{x:Static res:Strings.HabitScreen_QuickAdd_ChronoStop}"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Button.Style>
    <Path Width="16" Height="16" Stretch="Uniform"
          StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round">
        <Path.Style>
            <Style TargetType="Path">
                <Setter Property="Stroke" Value="{DynamicResource Brush.TextMuted}"/>
                <Setter Property="Data" Value="{StaticResource Icon.Clock}"/>
                <Style.Triggers>
                    <DataTrigger Binding="{Binding IsChronoRunning}" Value="True">
                        <Setter Property="Stroke" Value="{DynamicResource Brush.Accent}"/>
                        <Setter Property="Data" Value="{StaticResource Icon.Square}"/>
                    </DataTrigger>
                </Style.Triggers>
            </Style>
        </Path.Style>
    </Path>
</Button>
```

- [ ] **Step 4: Live readout.** Immediately AFTER that `Button` (still inside the horizontal `StackPanel`, before the trailing `UnitLabel` TextBlock), add a readout that shows only while THIS cell is timing:

```xml
<!-- Live elapsed while this cell's stopwatch runs (U8). Value comes from the shared ChronoClock on
     the quick-add host; visibility is scoped to the running cell so other duration cells stay quiet. -->
<TextBlock Margin="8,0,0,0" VerticalAlignment="Center" FontFamily="Consolas"
           Foreground="{DynamicResource Brush.Accent}"
           Text="{Binding Content.ChronoClock.Display, RelativeSource={RelativeSource AncestorType=ContentControl}}"
           Visibility="{Binding IsChronoRunning, Converter={StaticResource BoolToVisible}}"/>
```

- [ ] **Step 5: Build the app (XAML compiles through the WPF build) + full WPF suite.**

Run: `dotnet build src/Recordum.WPF --nologo` then `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo`
Expected: build succeeds (0 errors; `NU1903` warning is the known declassed CVE), tests green.

- [ ] **Step 6: Commit.**

```bash
git add src/Recordum.WPF/Resources/Icons.xaml \
        src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx \
        src/Recordum.WPF/Resources/Strings.Designer.cs \
        src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml
git commit -m "feat(wpf): start/stop chrono button + live elapsed readout in the quick-add"
```

---

## Final verification (after all tasks)

- [ ] Full WPF suite green; Data/Core untouched (no need to re-run, but a quick `dotnet test tests/Recordum.Core.Tests` costs little).
- [ ] `dotnet run --project src/Recordum.WPF` (NOT `--no-build`) and **author visual pass** on a counter with a **Tempo** field (or the Measured quick-add):
  - Click ⏱ → the icon turns into a **stop square (accent color)**, the tooltip reads **"Ferma cronometro"**, and a **time starts ticking up** (0:01, 0:02, …) next to the field.
  - Click ⏱ again → ticking stops, the elapsed **seconds fill the duration field** with the unit dropdown on **sec**, and the icon returns to the clock.
  - Switch habit/date while a chrono runs → no crash, no lingering ticking (the old quick-add is disposed).
- [ ] Tracker `docs/phase-4-issues.md`: note the quick-add chrono now has live feedback (closes the "cronometro non funziona" observation from the U8 visual pass).

## Out of scope

- Rethinking the `repeatable` flag / its label (parked for a separate session — see memory `recordum-counter-repeatability-rethink`).
- Timer **recovery** for the quick-add chrono after an app restart (the status-bar recovery path is separate; the quick-add chrono is a fresh start/stop).
- Disabling chip/series switching while a chrono runs (acceptable edge case: the timer persists; return to the series to stop it).
- Pause/resume of the per-cell chrono (`StopWithoutRecordingAsync` is a plain stop).
