# Abstinence in the Streak Engine — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `Resistance` (abstinence) habits compute a real streak — clean time since the last lapse — instead of the inverted, completion-positive streak the generic engine produces.

**Architecture:** Add a dedicated, self-contained abstinence branch to `SchedulingAwareStreakAggregator` (ignores schedule/mode/recovery, honors pauses; returns clean-period `StreakRun`s). The detail view shows the current streak at fine precision (hours/minutes since the last lapse instant) via a new `DurationFormat.FormatElapsed`, repurposes the "Runs (90d)" stat as "Lapses (90d)", and the editor hides the irrelevant Scheduling/Streak sections for Resistance. Seed/template: leave the "Stop smoking" template untouched (the engine fix makes it work); realign the demo "Niente fumo" to the lapse model.

**Tech Stack:** C# / .NET 10, EF Core + SQLite (tests via `DbFixture` in-memory), WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/archive/specs/2026-06-14-abstinence-streak-engine-design.md`

---

## File Structure

- `src/Recordum.Core/Enums/StreakBreakReason.cs` — add `Lapse` value.
- `src/Recordum.Data/Services/Aggregators/SchedulingAwareStreakAggregator.cs` — add abstinence branch + private `ComputeAbstinenceAsync`.
- `tests/Recordum.Data.Tests/Integration/Aggregators/AbstinenceStreakTests.cs` — **new** aggregator tests.
- `src/Recordum.WPF/Formatting/DurationFormat.cs` — add `FormatElapsed(TimeSpan)`.
- `tests/Recordum.WPF.Tests/Formatting/DurationFormatElapsedTests.cs` — **new** formatter tests.
- `src/Recordum.WPF/ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs` — resistance branch (fine elapsed, lapses-90, caption); ctor gains `ICompletionService`.
- `src/Recordum.WPF/ViewModels/HabitScreen/Detail/DetailViewModel.cs:47` — pass `completions` to `StreakSummaryViewModel`.
- `src/Recordum.WPF/Resources/Strings.resx` + `Strings.it.resx` + `Strings.Designer.cs` — `Detail.Streak.Lapses90`.
- `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml:562` — bind caption to `StreakSummary.RunsCaption`.
- `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Detail/StreakSummaryViewModelTests.cs` — update construction + add resistance tests.
- `src/Recordum.WPF/ViewModels/HabitEditor/Sections/SchedulingSectionViewModel.cs:64` + `StreakSectionViewModel.cs:37` — exclude Resistance in `IsApplicable`.
- `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/EditorSectionApplicabilityTests.cs` — **new** applicability tests.
- `src/Recordum.WPF/Bootstrap/DemoDataSeeder.cs` — realign "Niente fumo" to lapses.

---

## Task 1: Abstinence branch in the streak aggregator

**Files:**
- Modify: `src/Recordum.Core/Enums/StreakBreakReason.cs`
- Modify: `src/Recordum.Data/Services/Aggregators/SchedulingAwareStreakAggregator.cs`
- Test: `tests/Recordum.Data.Tests/Integration/Aggregators/AbstinenceStreakTests.cs` (new)

- [ ] **Step 1: Write the failing tests**

Create `tests/Recordum.Data.Tests/Integration/Aggregators/AbstinenceStreakTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Services.Aggregators;
using Recordum.Data.Services.Evaluators;
using Recordum.Data.Services.Policies;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration.Aggregators;

public sealed class AbstinenceStreakTests
{
    private static SchedulingAwareStreakAggregator BuildAggregator(DbFixture fx)
    {
        var nodes = new HabitNodeService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance);
        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        var completions = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var pauses = new StreakPauseService(fx.Factory, fx.Clock, NullLogger<StreakPauseService>.Instance);
        var dailyEval = new DailyStreakModeEvaluator();
        var weeklyEval = new WeeklyStreakModeEvaluator();
        return new SchedulingAwareStreakAggregator(
            nodes, configs, completions, pauses, new ScheduleEvaluator(),
            dailyEval, weeklyEval, new OccurrencesStreakModeEvaluator(), new BothStreakModeEvaluator(dailyEval, weeklyEval),
            new NoneStreakRecoveryPolicy(), new GraceDayStreakRecoveryPolicy(),
            new ManualStreakRecoveryPolicy(), new RetroactiveStreakRecoveryPolicy(), fx.Clock);
    }

    // created = habit CreatedAt; clock is left AT created so the interceptor stamps CreatedAt correctly.
    private static async Task<(DbFixture fx, Guid habitId)> SeedResistanceAsync(DateOnly created)
    {
        var clock = new FakeClock(new DateTimeOffset(created.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var fx = new DbFixture(clock);
        var habitId = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "no smoking" });
        await db.SaveChangesAsync();
        return (fx, habitId);
    }

    private static async Task LapseOnAsync(DbFixture fx, Guid habitId, DateOnly day)
    {
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var dt = day.ToDateTime(new TimeOnly(10, 0));
        await svc.RecordAsync(new Completion
        {
            HabitNodeId = habitId,
            CompletedAt = dt,
            CompletedAtMs = new DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            IsRetroactive = false,
        });
    }

    [Fact]
    public async Task Never_lapsed_is_one_current_run_from_creation_to_today()
    {
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-10);
        var (fx, id) = await SeedResistanceAsync(created);
        fx.Clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var aggr = BuildAggregator(fx);

        var runs = await aggr.GetHistoryAsync(id, created, today);

        runs.Should().ContainSingle();
        runs[0].IsCurrent.Should().BeTrue();
        runs[0].Start.Should().Be(created);
        runs[0].End.Should().Be(today);
    }

    [Fact]
    public async Task A_lapse_closes_the_clean_run_and_a_new_run_starts_the_next_day()
    {
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-30);
        var (fx, id) = await SeedResistanceAsync(created);
        await LapseOnAsync(fx, id, today.AddDays(-10));
        fx.Clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var aggr = BuildAggregator(fx);

        var runs = await aggr.GetHistoryAsync(id, created, today);

        runs.Should().HaveCount(2);
        runs[0].BreakReason.Should().Be(StreakBreakReason.Lapse);
        runs[0].IsCurrent.Should().BeFalse();
        runs[1].IsCurrent.Should().BeTrue();
        runs[1].Start.Should().Be(today.AddDays(-9)); // day after the lapse
        runs[1].End.Should().Be(today);
    }

    [Fact]
    public async Task A_lapse_today_leaves_no_current_clean_run()
    {
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-30);
        var (fx, id) = await SeedResistanceAsync(created);
        await LapseOnAsync(fx, id, today);
        fx.Clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var aggr = BuildAggregator(fx);

        var runs = await aggr.GetHistoryAsync(id, created, today);

        runs.Should().ContainSingle();
        runs[0].IsCurrent.Should().BeFalse();
        runs[0].BreakReason.Should().Be(StreakBreakReason.Lapse);
        runs[0].End.Should().Be(today.AddDays(-1));
    }

    [Fact]
    public async Task Consecutive_lapses_leave_no_clean_run_between_them()
    {
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-10);
        var (fx, id) = await SeedResistanceAsync(created);
        await LapseOnAsync(fx, id, today.AddDays(-5));
        await LapseOnAsync(fx, id, today.AddDays(-4));
        fx.Clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var aggr = BuildAggregator(fx);

        var runs = await aggr.GetHistoryAsync(id, created, today);

        runs.Should().HaveCount(2); // [created..-6] closed, then [-3..today] current; nothing between the two lapses
        runs[1].Start.Should().Be(today.AddDays(-3));
        runs[1].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task Paused_days_are_frozen_not_counted_and_do_not_break_the_run()
    {
        var today = new DateOnly(2026, 6, 1);
        var created = today.AddDays(-10); // 11 clean days inclusive
        var (fx, id) = await SeedResistanceAsync(created);
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.StreakPauses.Add(new StreakPause
            {
                Id = Guid.NewGuid(),
                HabitNodeId = id,
                PauseFrom = today.AddDays(-6),
                PauseTo = today.AddDays(-4), // 3 paused days
            });
            await db.SaveChangesAsync();
        }
        fx.Clock.Set(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var aggr = BuildAggregator(fx);

        var runs = await aggr.GetHistoryAsync(id, created, today);

        runs.Should().ContainSingle();
        runs[0].IsCurrent.Should().BeTrue();
        runs[0].Length.Should().Be(8); // 11 calendar days - 3 paused
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Recordum.Data.Tests --filter "FullyQualifiedName~AbstinenceStreakTests" --no-restore`
Expected: FAIL — `StreakBreakReason.Lapse` does not exist (compile error) and/or runs are computed completion-positive.

- [ ] **Step 3: Add the `Lapse` enum value**

Edit `src/Recordum.Core/Enums/StreakBreakReason.cs`:

```csharp
namespace Recordum.Core.Enums;
public enum StreakBreakReason { Missed, Manual, Closed, Lapse }
```

- [ ] **Step 4: Add the abstinence branch to the aggregator**

In `src/Recordum.Data/Services/Aggregators/SchedulingAwareStreakAggregator.cs`, inside `GetHistoryAsync`, right after the habit-node load + null check (the block that returns `Array.Empty<StreakRun>()` when `habitNode is null || habitNode.NodeType != NodeType.Habit`) and BEFORE loading the config, insert:

```csharp
        if (habitNode.HabitType == HabitType.Resistance)
            return await ComputeAbstinenceAsync(habitNode, fromDate, toDate, ct).ConfigureAwait(false);
```

Then add this private method to the class (e.g. just below `GetHistoryAsync`):

```csharp
    // Abstinence (Resistance) streak: a completion = a lapse. The "streak" is the run of consecutive
    // clean days (no completion) since the last lapse, or since creation if never lapsed. Pauses are
    // frozen (neither clean nor lapse). Schedule/mode/recovery do not apply. See spec 2026-06-14.
    private async ValueTask<IReadOnlyList<StreakRun>> ComputeAbstinenceAsync(
        HabitNode habitNode, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var createdDate = DateOnly.FromDateTime(habitNode.CreatedAt);
        var start = fromDate < createdDate ? createdDate : fromDate;
        if (start > toDate) return Array.Empty<StreakRun>();

        var heatmap = await completions.GetHeatmapAsync(habitNode.Id, start, toDate, ct).ConfigureAwait(false);
        var lapseDays = heatmap.Where(c => c.Count >= 1).Select(c => c.Date).ToHashSet();
        var pauseList = await pauses.ListByHabitAsync(habitNode.Id, ct).ConfigureAwait(false);
        bool IsPaused(DateOnly d) => pauseList.Any(p => d >= p.PauseFrom && d <= p.PauseTo);

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var runs = new List<StreakRun>();
        DateOnly? runStart = null;
        var runEnd = default(DateOnly);
        var length = 0;

        for (var day = start; day <= toDate; day = day.AddDays(1))
        {
            if (IsPaused(day)) continue; // frozen: run stays open, length unchanged

            if (lapseDays.Contains(day))
            {
                if (runStart is { } rs)
                {
                    runs.Add(new StreakRun(rs, runEnd, length, false, 0, StreakBreakReason.Lapse));
                    runStart = null; length = 0;
                }
                continue;
            }

            // clean day
            runStart ??= day;
            runEnd = day;
            length++;
        }

        if (runStart is { } rsFinal)
        {
            var isCurrent = runEnd >= today.AddDays(-1);
            runs.Add(new StreakRun(rsFinal, runEnd, length, isCurrent, 0, null));
        }
        return runs;
    }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Recordum.Data.Tests --filter "FullyQualifiedName~AbstinenceStreakTests" --no-restore`
Expected: PASS (5 tests).

- [ ] **Step 6: Run the full Data suite (no regressions)**

Run: `dotnet test tests/Recordum.Data.Tests --no-restore`
Expected: PASS, count = previous (322) + 5 = 327.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.Core/Enums/StreakBreakReason.cs src/Recordum.Data/Services/Aggregators/SchedulingAwareStreakAggregator.cs tests/Recordum.Data.Tests/Integration/Aggregators/AbstinenceStreakTests.cs
git commit -m "feat(data): abstinence streak — clean run since last lapse (B-abstinence)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: `DurationFormat.FormatElapsed`

**Files:**
- Modify: `src/Recordum.WPF/Formatting/DurationFormat.cs`
- Test: `tests/Recordum.WPF.Tests/Formatting/DurationFormatElapsedTests.cs` (new)

- [ ] **Step 1: Write the failing tests**

Create `tests/Recordum.WPF.Tests/Formatting/DurationFormatElapsedTests.cs`:

```csharp
using FluentAssertions;
using Recordum.WPF.Formatting;
using Xunit;

namespace Recordum.WPF.Tests.Formatting;

public class DurationFormatElapsedTests
{
    [Fact]
    public void Years_and_days() =>
        DurationFormat.FormatElapsed(TimeSpan.FromDays(2 * 365 + 41) + TimeSpan.FromHours(6))
            .Should().Be("2a 41g");

    [Fact]
    public void Days_and_hours() =>
        DurationFormat.FormatElapsed(TimeSpan.FromDays(41) + TimeSpan.FromHours(6))
            .Should().Be("41g 6h");

    [Fact]
    public void Hours_and_minutes() =>
        DurationFormat.FormatElapsed(TimeSpan.FromHours(6) + TimeSpan.FromMinutes(12))
            .Should().Be("6h 12m");

    [Fact]
    public void Minutes_only() =>
        DurationFormat.FormatElapsed(TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(30))
            .Should().Be("12m");

    [Fact]
    public void Under_a_minute_is_just_now() =>
        DurationFormat.FormatElapsed(TimeSpan.FromSeconds(20)).Should().Be("appena ora");

    [Fact]
    public void Negative_is_just_now() =>
        DurationFormat.FormatElapsed(TimeSpan.FromSeconds(-5)).Should().Be("appena ora");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~DurationFormatElapsedTests" --no-restore`
Expected: FAIL — `FormatElapsed` does not exist (compile error).

- [ ] **Step 3: Implement `FormatElapsed`**

In `src/Recordum.WPF/Formatting/DurationFormat.cs`, add after `FormatDays` (keep the same hardcoded Italian abbreviations as `FormatDays`: a=anni, g=giorni, h=ore, m=minuti):

```csharp
    /// <summary>Elapsed-time display for the Resistance current streak: the two most significant
    /// units. "2a 41g" / "41g 6h" / "6h 12m" / "12m" / "appena ora". Years approximated as 365 days,
    /// consistent with <see cref="FormatDays"/>.</summary>
    public static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        int totalDays = (int)elapsed.TotalDays;
        if (totalDays >= 365)
            return string.Create(CultureInfo.InvariantCulture, $"{totalDays / 365}a {totalDays % 365}g");
        if (totalDays >= 1)
            return string.Create(CultureInfo.InvariantCulture, $"{totalDays}g {elapsed.Hours}h");
        if (elapsed.TotalHours >= 1)
            return string.Create(CultureInfo.InvariantCulture, $"{elapsed.Hours}h {elapsed.Minutes}m");
        if (elapsed.TotalMinutes >= 1)
            return string.Create(CultureInfo.InvariantCulture, $"{elapsed.Minutes}m");
        return "appena ora";
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~DurationFormatElapsedTests" --no-restore`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Formatting/DurationFormat.cs tests/Recordum.WPF.Tests/Formatting/DurationFormatElapsedTests.cs
git commit -m "feat(wpf): DurationFormat.FormatElapsed for abstinence current streak

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Resistance display in `StreakSummaryViewModel`

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Detail/DetailViewModel.cs:47`
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml:562`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Detail/StreakSummaryViewModelTests.cs`

- [ ] **Step 1: Add the resx key (3 files)**

In `src/Recordum.WPF/Resources/Strings.resx` add next to `Detail.Streak.Runs90`:

```xml
  <data name="Detail.Streak.Lapses90" xml:space="preserve">
    <value>Lapses (90d)</value>
  </data>
```

In `src/Recordum.WPF/Resources/Strings.it.resx` add:

```xml
  <data name="Detail.Streak.Lapses90" xml:space="preserve">
    <value>Ricadute (90g)</value>
  </data>
```

In `src/Recordum.WPF/Resources/Strings.Designer.cs` add next to the `Detail_Streak_Runs90` property:

```csharp
    public static string Detail_Streak_Lapses90 => Get("Detail.Streak.Lapses90");
```

- [ ] **Step 2: Write the failing tests**

Add to `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Detail/StreakSummaryViewModelTests.cs` (note: this step also requires updating the 6 existing `new StreakSummaryViewModel(aggr, clock)` call sites in this file to `new StreakSummaryViewModel(aggr, new FakeCompletionService(), clock)` — do that in Step 4 once the ctor changes; for now add the new tests which use the new ctor):

```csharp
    [WpfFact]
    public async Task Resistance_current_shows_elapsed_since_last_lapse()
    {
        var today = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(today);
        var node = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance,
            Name = "no smoking", CreatedAt = today.AddDays(-100).UtcDateTime,
        };
        var completions = new FakeCompletionService();
        var lastLapse = today.AddDays(-3).AddHours(-6); // 3d 6h ago
        completions.Store[Guid.NewGuid()] = new Completion
        {
            HabitNodeId = node.Id, CompletedAt = lastLapse.UtcDateTime, CompletedAtMs = lastLapse.ToUnixTimeMilliseconds(),
        };
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), completions, clock);

        await vm.LoadAsync(node);

        vm.CurrentDisplay.Should().Be("3g 6h");
    }

    [WpfFact]
    public async Task Resistance_never_lapsed_counts_from_creation()
    {
        var today = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(today);
        var node = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance,
            Name = "no smoking", CreatedAt = today.AddDays(-10).UtcDateTime,
        };
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), new FakeCompletionService(), clock);

        await vm.LoadAsync(node);

        vm.CurrentDisplay.Should().Be("10g 0h");
    }

    [WpfFact]
    public async Task Resistance_caption_is_lapses_and_counts_lapse_days_in_90()
    {
        var today = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(today);
        var node = new HabitNode
        {
            Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance,
            Name = "no smoking", CreatedAt = today.AddDays(-100).UtcDateTime,
        };
        var completions = new FakeCompletionService();
        completions.HeatmapByHabit[node.Id] = new[]
        {
            new HeatmapCell(DateOnly.FromDateTime(today.AddDays(-10).UtcDateTime), 1, 0),
            new HeatmapCell(DateOnly.FromDateTime(today.AddDays(-40).UtcDateTime), 2, 0),
        };
        var vm = new StreakSummaryViewModel(new FakeStreakAggregator(), completions, clock);

        await vm.LoadAsync(node);

        vm.RunsCaption.Should().Be(Recordum.WPF.Resources.Strings.Detail_Streak_Lapses90);
        vm.RunsLast90Days.Should().Be(2);
    }
```

Note on `HeatmapCell` shape: it is `HeatmapCell(DateOnly Date, int Count, int RetroactiveCount)`. Verify the positional order in `src/Recordum.Core/Abstractions/Services/Dtos/` and adjust the constructor calls if needed before running.

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~StreakSummaryViewModelTests" --no-restore`
Expected: FAIL — ctor has no `ICompletionService` overload (compile error).

- [ ] **Step 4: Change the ViewModel**

Rewrite `src/Recordum.WPF/ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs`:

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Formatting;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen.Detail;

public sealed partial class StreakSummaryViewModel(
    IStreakAggregator aggregator, ICompletionService completions, IClock clock) : ObservableObject
{
    [ObservableProperty] private int _currentLength;
    [ObservableProperty] private int _longestLength;
    [ObservableProperty] private DateOnly? _longestStartedOn;
    [ObservableProperty] private int _runsLast90Days;
    [ObservableProperty] private bool _isEmpty;

    /// <summary>The big "current streak" number, formatted for display. Resistance habits render
    /// the elapsed time since the last lapse ("2a 41g"); other types show the plain day count.</summary>
    [ObservableProperty] private string _currentDisplay = "0";

    /// <summary>Caption for the third stat: "Lapses (90d)" for Resistance, "Runs (90d)" otherwise.</summary>
    [ObservableProperty] private string _runsCaption = Strings.Detail_Streak_Runs90;

    public async Task LoadAsync(HabitNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var allTime = await aggregator.GetHistoryAsync(node.Id, DateOnly.FromDateTime(node.CreatedAt), today, ct).ConfigureAwait(true);
        var last90 = await aggregator.GetHistoryAsync(node.Id, today.AddDays(-89), today, ct).ConfigureAwait(true);

        var longest = allTime.OrderByDescending(r => r.Length).FirstOrDefault();
        LongestLength = longest?.Length ?? 0;
        LongestStartedOn = longest?.Start;

        var current = allTime.FirstOrDefault(r => r.End == today || r.End == today.AddDays(-1));
        CurrentLength = current?.Length ?? 0;
        IsEmpty = allTime.Count == 0;

        if (node.HabitType == HabitType.Resistance)
        {
            RunsCaption = Strings.Detail_Streak_Lapses90;

            // Current streak = fine-precision elapsed since the last lapse instant (or creation).
            var last = await completions.ListByHabitAsync(node.Id, take: 1, ct: ct).ConfigureAwait(true);
            var cleanSince = last.Count > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(last[0].CompletedAtMs)
                : new DateTimeOffset(DateTime.SpecifyKind(node.CreatedAt, DateTimeKind.Utc));
            CurrentDisplay = DurationFormat.FormatElapsed(clock.UtcNow - cleanSince);

            // Third stat = number of lapse days in the last 90 days.
            var heat90 = await completions.GetHeatmapAsync(node.Id, today.AddDays(-89), today, ct).ConfigureAwait(true);
            RunsLast90Days = heat90.Count(c => c.Count >= 1);
        }
        else
        {
            RunsCaption = Strings.Detail_Streak_Runs90;
            RunsLast90Days = last90.Count;
            CurrentDisplay = CurrentLength.ToString(CultureInfo.InvariantCulture);
        }
    }
}
```

- [ ] **Step 5: Pass `completions` from `DetailViewModel`**

In `src/Recordum.WPF/ViewModels/HabitScreen/Detail/DetailViewModel.cs`, line 47, change:

```csharp
        StreakSummary = new StreakSummaryViewModel(streaks, clock);
```
to:
```csharp
        StreakSummary = new StreakSummaryViewModel(streaks, completions, clock);
```

- [ ] **Step 6: Update the 6 existing call sites in the test file**

In `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Detail/StreakSummaryViewModelTests.cs`, replace every `new StreakSummaryViewModel(<aggr>, <clock>)` with `new StreakSummaryViewModel(<aggr>, new FakeCompletionService(), <clock>)` (lines 15, 32, 46, 60, 79, 98 in the original — match the actual two-arg calls). Add `using Recordum.WPF.Tests.Fixtures;` if not present.

- [ ] **Step 7: Bind the caption in XAML**

In `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`, line 562, change:

```xml
                                    <TextBlock Text="{x:Static res:Strings.Detail_Streak_Runs90}"
```
to:
```xml
                                    <TextBlock Text="{Binding StreakSummary.RunsCaption}"
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~StreakSummaryViewModelTests" --no-restore`
Expected: PASS (existing + 3 new).

- [ ] **Step 9: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/Detail/DetailViewModel.cs src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Detail/StreakSummaryViewModelTests.cs
git commit -m "feat(wpf): resistance streak display — fine elapsed + lapses(90d)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Editor hides Scheduling/Streak for Resistance

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/Sections/SchedulingSectionViewModel.cs:64`
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/Sections/StreakSectionViewModel.cs:37`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/EditorSectionApplicabilityTests.cs` (new)

- [ ] **Step 1: Write the failing tests**

Create `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/EditorSectionApplicabilityTests.cs`:

```csharp
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.ViewModels.HabitEditor.Sections;
using Recordum.WPF.Tests.Fixtures;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class EditorSectionApplicabilityTests
{
    [Fact]
    public void Scheduling_is_hidden_for_resistance()
    {
        var s = new SchedulingSectionViewModel(new FakeHabitConfigService());
        s.IsApplicable(NodeType.Habit, HabitType.Resistance).Should().BeFalse();
        s.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeTrue();
    }

    [Fact]
    public void Streak_is_hidden_for_resistance()
    {
        var s = new StreakSectionViewModel(new FakeHabitConfigService());
        s.IsApplicable(NodeType.Habit, HabitType.Resistance).Should().BeFalse();
        s.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeTrue();
    }
}
```

Note: confirm the fake config service type name used elsewhere in `tests/Recordum.WPF.Tests/Fixtures/` (e.g. `FakeHabitConfigService`/`FakeAppSettingService`); use the one that satisfies each section's constructor. Adjust the constructor argument if the section takes a different dependency.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~EditorSectionApplicabilityTests" --no-restore`
Expected: FAIL — both return `true` for Resistance.

- [ ] **Step 3: Exclude Resistance in both sections**

In `src/Recordum.WPF/ViewModels/HabitEditor/Sections/SchedulingSectionViewModel.cs`, line 64, change:

```csharp
    public override bool IsApplicable(NodeType node, HabitType? habit) => node == NodeType.Habit;
```
to:
```csharp
    // Abstinence ignores scheduling — the streak is "time since last lapse" (spec 2026-06-14).
    public override bool IsApplicable(NodeType node, HabitType? habit)
        => node == NodeType.Habit && habit != HabitType.Resistance;
```

In `src/Recordum.WPF/ViewModels/HabitEditor/Sections/StreakSectionViewModel.cs`, line 37, change:

```csharp
    public override bool IsApplicable(NodeType node, HabitType? habit) => node == NodeType.Habit;
```
to:
```csharp
    // Abstinence uses a dedicated streak (clean time since last lapse); mode/recovery don't apply.
    public override bool IsApplicable(NodeType node, HabitType? habit)
        => node == NodeType.Habit && habit != HabitType.Resistance;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~EditorSectionApplicabilityTests" --no-restore`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitEditor/Sections/SchedulingSectionViewModel.cs src/Recordum.WPF/ViewModels/HabitEditor/Sections/StreakSectionViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitEditor/EditorSectionApplicabilityTests.cs
git commit -m "feat(wpf): hide scheduling/streak editor sections for resistance

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Realign demo seeder "Niente fumo" to the lapse model

**Files:**
- Modify: `src/Recordum.WPF/Bootstrap/DemoDataSeeder.cs`

This task changes runtime demo data only (no schema, no tests). Verify visually in Task 6.

- [ ] **Step 1: Rename the metric to a lapse metric**

In `src/Recordum.WPF/Bootstrap/DemoDataSeeder.cs`, line 103, change:

```csharp
        var mVoglie = await MetricAsync(nienteFumo, "Voglie resistite", ct).ConfigureAwait(false);
```
to:
```csharp
        var mSigarette = await MetricAsync(nienteFumo, "Sigarette", ct).ConfigureAwait(false);
```

- [ ] **Step 2: Remove the daily check-in**

In the daily loop, delete these two lines (originally lines 140–141):

```csharp
            if (Chance(0.70))
                await ValueAsync(nienteFumo, mVoglie, _rng.Next(0, 5), day, 20, 22, ct).ConfigureAwait(false);
```

- [ ] **Step 3: Seed a few relapses after the daily loop**

Immediately after the daily `for` loop closes (after the line `}` that ends the loop at original line 146), insert:

```csharp
        // "Niente fumo" abstinence: record only relapses (rare). Clean streak = time since the last
        // one. Latest ~3 weeks ago → a multi-week clean streak with a couple of lapse markers.
        foreach (var lapseAgo in new[] { 67, 40, 21 })
            await ValueAsync(nienteFumo, mSigarette, _rng.Next(1, 4), today.AddDays(-lapseAgo), 18, 20, ct).ConfigureAwait(false);
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build src/Recordum.WPF --no-restore`
Expected: 0 errors, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Bootstrap/DemoDataSeeder.cs
git commit -m "feat(wpf): demo seeder — niente fumo records relapses, not daily check-ins

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: Full verification

**Files:** none (verification only).

- [ ] **Step 1: Run both suites**

Run: `dotnet test tests/Recordum.Data.Tests --no-restore` then `dotnet test tests/Recordum.WPF.Tests --no-restore`
Expected: Data 327 green; WPF = previous (535) + 6 (FormatElapsed) + 3 (StreakSummary resistance) + 2 (editor applicability) = 546 green (plus the known-flaky now stable).

- [ ] **Step 2: Reseed demo + launch (manual visual check)**

Wipe local state and reseed so the new demo data is present:

```bash
rm -rf "$LOCALAPPDATA/Recordum" 2>/dev/null
dotnet run --project src/Recordum.WPF -- --seed-demo
```
Then launch normally (`dotnet run --project src/Recordum.WPF --no-build`) and verify:
1. "Niente fumo" shows a current streak as elapsed time (e.g. "21g Xh") in the Detail → Streak card.
2. The third stat reads "Ricadute (90g)" with a small count (≈3).
3. The calendar/heatmap lights only the relapse days.
4. Instantiating the **"Stop smoking"** template (untouched) produces a Resistance habit whose streak counts up from creation and resets when you record a lapse.
5. The editor for a Resistance habit no longer shows the Scheduling and Streak sections.

- [ ] **Step 3: Update the tracker**

In `docs/phase-4-issues.md`, change the B-abstinence entry status from `triaged` to `fixed` and append the implemented summary (tasks, final suite counts, "Stop smoking template confirmed working via engine; demo realigned"). Commit:

```bash
git add docs/phase-4-issues.md
git commit -m "docs: phase-4 tracker — B-abstinence fixed

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review notes

- **Spec coverage:** §3.1 abstinence algorithm → Task 1; §3.2 fine-precision current → Task 3 (+Task 2 formatter); §3.3 `FormatElapsed` → Task 2; §3.4 "Lapses (90d)" caption → Task 3; §3.5 editor hides sections → Task 4; §3.6 template untouched (verified Task 6) + demo realign → Task 5; §4 testing → Tasks 1–4 + Task 6.
- **Type consistency:** `StreakRun(Start, End, Length, IsCurrent, RecoveriesUsed, BreakReason?)`, `StreakBreakReason.Lapse`, `DurationFormat.FormatElapsed(TimeSpan)`, `StreakSummaryViewModel(IStreakAggregator, ICompletionService, IClock)`, `RunsCaption`, `Strings.Detail_Streak_Lapses90` — used consistently across tasks.
- **Verify-before-run reminders embedded:** `HeatmapCell` positional shape (Task 3 Step 2), fake config service name (Task 4 Step 1), exact two-arg call-site lines (Task 3 Step 6).
