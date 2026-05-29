# Editor Sections UI (B6c) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the missing Habit Editor section Views and compose them into `EditorPane`, so users can customize scheduling, streak, metrics, scale, timer, goal, notifications, tags, AI sharing and reset time.

**Architecture:** The 11 section ViewModels already exist with per-field autosave (M3.4). We add one small `UserControl` View per applicable section, map VM→View via implicit `DataTemplate`s, and host them in `EditorPane` as an `ItemsControl` of collapsible `Expander`s. `SectionViewModelBase` gains presentation-only `Title`/`IsVisible`/`IsExpanded`; `HabitEditorViewModel` sets `IsVisible` from each section's existing `IsApplicable`. A new read service lists units/scales for the Metrics/Scales pickers. DataSource (placeholder) is excluded.

**Tech Stack:** WPF + CommunityToolkit.Mvvm, EF Core (read service), xUnit + FluentAssertions, `.resx` strings (English-first).

**Spec:** `docs/superpowers/specs/2026-05-29-editor-sections-ui-design.md`

---

## File structure

- Create `src/Recordum.Core/Abstractions/Services/IReferenceDataQueryService.cs` — list units + scales for pickers.
- Create `src/Recordum.Data/Services/Queries/ReferenceDataQueryService.cs` — EF implementation.
- Modify `src/Recordum.WPF/ViewModels/HabitEditor/SectionViewModelBase.cs` — add `Title`/`IsVisible`/`IsExpanded`.
- Modify each `src/Recordum.WPF/ViewModels/HabitEditor/Sections/*SectionViewModel.cs` — implement `Title`; Metrics/Scales also gain available-lists.
- Modify `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs` — `Sections` list + `IsVisible` wiring.
- Create `src/Recordum.WPF/Views/HabitEditor/Sections/*SectionView.xaml` (+ `.cs`) — one per applicable section.
- Create `src/Recordum.WPF/Views/HabitEditor/Sections/SectionTemplates.xaml` — implicit VM→View DataTemplates.
- Modify `src/Recordum.WPF/Views/EditorPane.xaml` — host `ItemsControl`/`Expander`; merge `SectionTemplates.xaml`.
- Modify `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` — register the read service.
- Modify `src/Recordum.WPF/Resources/Strings.resx` + `Strings.Designer.cs` — section titles + control labels.
- Tests in `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/` and `tests/Recordum.Data.Tests/Integration/`.

**Section applicability** (from each VM's existing `IsApplicable`; do not change it):

| Section | Applicable |
|---|---|
| Scheduling, Streak | Habit |
| Metrics, Timer | Habit + Counter/Resistance |
| Scales | Habit + State |
| Goal, Notifications, Tags, AiSharing, ResetTime | all nodes (no override) |

---

## Task 1: SectionViewModelBase presentation members

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/SectionViewModelBase.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/SectionViewModelBaseTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Recordum.WPF.ViewModels.HabitEditor;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class SectionViewModelBaseTests
{
    private sealed class StubSection : SectionViewModelBase
    {
        public override string Title => "Stub";
    }

    [Fact]
    public void Defaults_visible_false_collapsed()
    {
        var s = new StubSection();
        s.Title.Should().Be("Stub");
        s.IsVisible.Should().BeFalse();   // set explicitly on load
        s.IsExpanded.Should().BeFalse();  // L2 collapsed by default
    }
}
```

- [ ] **Step 2: Run test, expect compile failure** (`Title`/`IsVisible`/`IsExpanded` not defined)

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter FullyQualifiedName~SectionViewModelBaseTests`
Expected: build error — members missing.

- [ ] **Step 3: Add the members**

In `SectionViewModelBase` add:

```csharp
/// <summary>Localized section header shown on the Expander.</summary>
public abstract string Title { get; }

/// <summary>Whether this section applies to the loaded node (set in HabitEditorViewModel.LoadAsync).</summary>
[ObservableProperty] private bool _isVisible;

/// <summary>L2 progressive disclosure: collapsed by default.</summary>
[ObservableProperty] private bool _isExpanded;
```

- [ ] **Step 4: Run test, expect PASS**

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitEditor/SectionViewModelBase.cs tests/Recordum.WPF.Tests/ViewModels/HabitEditor/SectionViewModelBaseTests.cs
git commit -m "feat(wpf): SectionViewModelBase Title/IsVisible/IsExpanded for editor sections (B6c)"
```

---

## Task 2: Section titles (resx + Title overrides)

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx`
- Modify: `src/Recordum.WPF/Resources/Strings.Designer.cs`
- Modify: all 10 applicable `*SectionViewModel.cs`

- [ ] **Step 1: Add resource strings**

In `Strings.resx` add `<data name="Editor.Section.Scheduling"><value>Scheduling</value></data>` and the same for `Editor.Section.Streak`, `Editor.Section.Metrics`, `Editor.Section.Scales`, `Editor.Section.Timer`, `Editor.Section.Goal`, `Editor.Section.Notifications`, `Editor.Section.Tags`, `Editor.Section.AiSharing`, `Editor.Section.ResetTime`. (Italian secondary in `Strings.it.resx` if present.)

In `Strings.Designer.cs` add a property per key following the existing pattern, e.g.:

```csharp
public static string Editor_Section_Scheduling => Get("Editor.Section.Scheduling");
public static string Editor_Section_Streak => Get("Editor.Section.Streak");
public static string Editor_Section_Metrics => Get("Editor.Section.Metrics");
public static string Editor_Section_Scales => Get("Editor.Section.Scales");
public static string Editor_Section_Timer => Get("Editor.Section.Timer");
public static string Editor_Section_Goal => Get("Editor.Section.Goal");
public static string Editor_Section_Notifications => Get("Editor.Section.Notifications");
public static string Editor_Section_Tags => Get("Editor.Section.Tags");
public static string Editor_Section_AiSharing => Get("Editor.Section.AiSharing");
public static string Editor_Section_ResetTime => Get("Editor.Section.ResetTime");
```

- [ ] **Step 2: Add `Title` override to each section VM**

Add `using Recordum.WPF.Resources;` if missing, then in each VM:

- `SchedulingSectionViewModel`: `public override string Title => Strings.Editor_Section_Scheduling;`
- `StreakSectionViewModel`: `=> Strings.Editor_Section_Streak;`
- `MetricsSectionViewModel`: `=> Strings.Editor_Section_Metrics;`
- `ScalesSectionViewModel`: `=> Strings.Editor_Section_Scales;`
- `TimerSectionViewModel`: `=> Strings.Editor_Section_Timer;`
- `GoalSectionViewModel`: `=> Strings.Editor_Section_Goal;`
- `NotificationsSectionViewModel`: `=> Strings.Editor_Section_Notifications;`
- `TagsSectionViewModel`: `=> Strings.Editor_Section_Tags;`
- `AiSharingSectionViewModel`: `=> Strings.Editor_Section_AiSharing;`
- `ResetTimeSectionViewModel`: `=> Strings.Editor_Section_ResetTime;`

(`DataSourceSectionViewModel` keeps no Title — it is excluded from the `Sections` list in Task 3, so it never needs one. If the compiler requires it because the base is abstract, give it `public override string Title => string.Empty;`.)

- [ ] **Step 3: Build**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.Designer.cs src/Recordum.WPF/ViewModels/HabitEditor/Sections/*.cs
git commit -m "feat(wpf): section titles for editor sections (B6c)"
```

---

## Task 3: HabitEditorViewModel.Sections + IsVisible wiring

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/HabitEditorViewModelTests.cs` (add cases; create if absent)

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task LoadAsync_sets_section_visibility_by_habit_type()
{
    var sut = CreateEditor();              // existing helper or build with fakes
    var id = await SeedHabitAsync(HabitType.Action);  // helper: create an Action habit
    await sut.LoadAsync(id);

    sut.Scheduling.IsVisible.Should().BeTrue();   // Habit
    sut.Metrics.IsVisible.Should().BeFalse();     // Counter/Resistance only
    sut.Scales.IsVisible.Should().BeFalse();      // State only
    sut.Sections.Should().NotContain(s => ReferenceEquals(s, sut.DataSource)); // excluded
}
```

(If `HabitEditorViewModelTests` does not exist, construct the SUT with the existing fakes used by `BootstrapSmokeTests`/section tests, and seed via `IHabitNodeService`.)

- [ ] **Step 2: Run test, expect FAIL** (`Sections` not defined / IsVisible false because never set)

- [ ] **Step 3: Implement**

In `HabitEditorViewModel`, replace the private `EnumerateSections()` usage by also exposing an ordered public list (excluding DataSource) and set visibility on load:

```csharp
public IReadOnlyList<SectionViewModelBase> Sections { get; }
```

In the constructor, after creating the sections, initialize:

```csharp
Sections = new SectionViewModelBase[]
{
    Scheduling, Streak, Timer, Metrics, Scales, Goal, Notifications, Tags, AiSharing, ResetTime,
};
```

(Keep `EnumerateSections()` as-is for the status aggregation — it may still include DataSource. `Sections` is the render list only.)

In `LoadAsync`, after `Node = node;` and the section `LoadAsync` calls, add:

```csharp
foreach (var s in Sections)
    s.IsVisible = s.IsApplicable(node.NodeType, node.HabitType);
```

- [ ] **Step 4: Run test, expect PASS**

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitEditor/HabitEditorViewModelTests.cs
git commit -m "feat(wpf): expose ordered editor Sections + per-type visibility (B6c)"
```

---

## Task 4: Reference-data read service (units + scales)

**Files:**
- Create: `src/Recordum.Core/Abstractions/Services/IReferenceDataQueryService.cs`
- Create: `src/Recordum.Data/Services/Queries/ReferenceDataQueryService.cs`
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`
- Test: `tests/Recordum.Data.Tests/Integration/ReferenceDataQueryServiceTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class ReferenceDataQueryServiceTests
{
    [Fact]
    public async Task Lists_seeded_units_and_scales()
    {
        await using var fx = new DbFixture();
        var svc = new ReferenceDataQueryService(fx.Factory);

        (await svc.ListUnitsAsync()).Should().Contain(u => u.Code == "l");
        (await svc.ListScalesAsync()).Should().Contain(s => s.Code == "mood-5");
    }
}
```

- [ ] **Step 2: Run test, expect compile failure** (service missing)

- [ ] **Step 3: Implement interface**

`IReferenceDataQueryService.cs`:

```csharp
using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IReferenceDataQueryService
{
    ValueTask<IReadOnlyList<UnitOfMeasure>> ListUnitsAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<ScaleTemplate>> ListScalesAsync(CancellationToken ct = default);
}
```

`ReferenceDataQueryService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services.Queries;

public sealed class ReferenceDataQueryService(IDbContextFactory<RecordumDbContext> dbFactory)
    : IReferenceDataQueryService
{
    public async ValueTask<IReadOnlyList<UnitOfMeasure>> ListUnitsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.UnitsOfMeasure.AsNoTracking().OrderBy(u => u.Name).ToListAsync(ct);
    }

    public async ValueTask<IReadOnlyList<ScaleTemplate>> ListScalesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ScaleTemplates.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
    }
}
```

- [ ] **Step 4: Register in DI**

In `ServiceRegistrar.cs`, near the other query services:

```csharp
services.AddScoped<IReferenceDataQueryService, ReferenceDataQueryService>();
```

- [ ] **Step 5: Run test, expect PASS**

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Core/Abstractions/Services/IReferenceDataQueryService.cs src/Recordum.Data/Services/Queries/ReferenceDataQueryService.cs src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs tests/Recordum.Data.Tests/Integration/ReferenceDataQueryServiceTests.cs
git commit -m "feat(data): reference-data query service for units + scales (B6c)"
```

---

## Task 5: Metrics + Scales available-lists

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/Sections/MetricsSectionViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/Sections/ScalesSectionViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs` (pass the service)
- Test: extend `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/MetricsSectionViewModelTests.cs` / `ScalesSectionViewModelTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task Metrics_LoadAsync_populates_available_units()
{
    var refData = new FakeReferenceData();      // returns one UnitOfMeasure {Code="l"}
    var sut = new MetricsSectionViewModel(new FakeMetricDefinitionService(), refData);
    await sut.LoadAsync(new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter });
    sut.AvailableUnits.Should().ContainSingle(u => u.Code == "l");
}

[Fact]
public async Task Scales_LoadAsync_populates_available_scales()
{
    var refData = new FakeReferenceData();      // returns one ScaleTemplate {Code="mood-5"}
    var sut = new ScalesSectionViewModel(new FakeMetricDefinitionService(), refData);
    await sut.LoadAsync(new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State });
    sut.AvailableScales.Should().ContainSingle(s => s.Code == "mood-5");
}
```

Create `tests/Recordum.WPF.Tests/Fixtures/FakeReferenceData.cs`:

```csharp
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeReferenceData : IReferenceDataQueryService
{
    public List<UnitOfMeasure> Units { get; } = new() { new UnitOfMeasure { Id = Guid.NewGuid(), Code = "l", Name = "Litre" } };
    public List<ScaleTemplate> Scales { get; } = new() { new ScaleTemplate { Id = Guid.NewGuid(), Code = "mood-5", Name = "Mood (1-5)" } };
    public ValueTask<IReadOnlyList<UnitOfMeasure>> ListUnitsAsync(CancellationToken ct = default) => new((IReadOnlyList<UnitOfMeasure>)Units);
    public ValueTask<IReadOnlyList<ScaleTemplate>> ListScalesAsync(CancellationToken ct = default) => new((IReadOnlyList<ScaleTemplate>)Scales);
}
```

(If `FakeMetricDefinitionService` does not exist, create a minimal one returning empty `ListByHabitAsync` and no-op writes.)

- [ ] **Step 2: Run tests, expect compile failure** (ctor arity / `AvailableUnits`/`AvailableScales` missing)

- [ ] **Step 3: Implement**

`MetricsSectionViewModel`: change primary constructor to
`MetricsSectionViewModel(IMetricDefinitionService metrics, IReferenceDataQueryService refData)`, keep `_metrics`, add `private readonly IReferenceDataQueryService _refData = refData;` and:

```csharp
public ObservableCollection<UnitOfMeasure> AvailableUnits { get; } = new();
```

At the start of `LoadAsync`, before adding metric rows:

```csharp
AvailableUnits.Clear();
foreach (var u in await _refData.ListUnitsAsync()) AvailableUnits.Add(u);
```

`ScalesSectionViewModel`: change constructor to
`ScalesSectionViewModel(IMetricDefinitionService metrics, IReferenceDataQueryService refData)`, add `_refData`, and:

```csharp
public ObservableCollection<ScaleTemplate> AvailableScales { get; } = new();
```

In `LoadAsync`, before reading `_primary`:

```csharp
AvailableScales.Clear();
foreach (var s in await _refData.ListScalesAsync()) AvailableScales.Add(s);
```

`HabitEditorViewModel`: add `IReferenceDataQueryService refData` to the constructor (DI provides it) and pass it:

```csharp
Metrics = new MetricsSectionViewModel(metrics, refData);
Scales = new ScalesSectionViewModel(metrics, refData);
```

- [ ] **Step 4: Run tests + full WPF suite, expect PASS**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj`
Expected: all pass (BootstrapSmoke resolves the editor via DI with the new dep).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitEditor/ tests/Recordum.WPF.Tests/
git commit -m "feat(wpf): available units/scales for Metrics + Scales sections (B6c)"
```

---

## Task 6: EditorPane host + SectionTemplates dictionary

**Files:**
- Create: `src/Recordum.WPF/Views/HabitEditor/Sections/SectionTemplates.xaml` (ResourceDictionary)
- Modify: `src/Recordum.WPF/Views/EditorPane.xaml`

- [ ] **Step 1: Create `SectionTemplates.xaml` (empty templates added per later task)**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:vms="clr-namespace:Recordum.WPF.ViewModels.HabitEditor.Sections"
                    xmlns:vs="clr-namespace:Recordum.WPF.Views.HabitEditor.Sections">
    <!-- One implicit DataTemplate per section VM is added by Tasks 7-16. -->
</ResourceDictionary>
```

- [ ] **Step 2: Wire the host in `EditorPane.xaml`**

Add the dictionary to `UserControl.Resources` (create the element if absent):

```xml
<UserControl.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="HabitEditor/Sections/SectionTemplates.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</UserControl.Resources>
```

Replace the empty `SectionsHost` StackPanel with:

```xml
<ItemsControl x:Name="SectionsHost" ItemsSource="{Binding Sections}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Expander Header="{Binding Title}"
                      IsExpanded="{Binding IsExpanded, Mode=TwoWay}"
                      Margin="0,0,0,6"
                      Foreground="{DynamicResource Brush.Text}"
                      Visibility="{Binding IsVisible, Converter={StaticResource BoolToVisible}}">
                <ContentControl Content="{Binding}" Margin="8,8,0,0"/>
            </Expander>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

- [ ] **Step 3: Build + run app to confirm the editor opens with collapsed section headers**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj` (0 errors), then launch via the `run` skill; open a habit → Configura. Expected: a stack of collapsed Expander headers (Scheduling, Streak, …) with empty bodies (views arrive next).

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Views/HabitEditor/Sections/SectionTemplates.xaml src/Recordum.WPF/Views/EditorPane.xaml
git commit -m "feat(wpf): EditorPane sections host (ItemsControl + Expander) (B6c)"
```

---

## Tasks 7-16: Section Views (one per section)

Each task: create `…/Views/HabitEditor/Sections/<Name>SectionView.xaml` (+ empty code-behind `.xaml.cs` with `InitializeComponent()`), register its implicit `DataTemplate` in `SectionTemplates.xaml`, build, then visually confirm autosave (the footer shows "Saved"). Commit per section. All controls inherit chrome from `BaseStyles.xaml`.

**Shared code-behind shape** for every `<Name>SectionView.xaml.cs`:

```csharp
using System.Windows.Controls;
namespace Recordum.WPF.Views.HabitEditor.Sections;
public partial class <Name>SectionView : UserControl
{
    public <Name>SectionView() => InitializeComponent();
}
```

**Each view's root** is:

```xml
<UserControl x:Class="Recordum.WPF.Views.HabitEditor.Sections.<Name>SectionView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:enums="clr-namespace:Recordum.Core.Enums;assembly=Recordum.Core"
             xmlns:res="clr-namespace:Recordum.WPF.Resources">
  <StackPanel> <!-- controls below --> </StackPanel>
</UserControl>
```

**Register each template** in `SectionTemplates.xaml` (add inside the dictionary):

```xml
<DataTemplate DataType="{x:Type vms:<Name>SectionViewModel}"><vs:<Name>SectionView/></DataTemplate>
```

Enum-bound `ComboBox`es use `ObjectDataProvider` for `Enum.GetValues`. Add this helper once to `SectionTemplates.xaml` per enum used (ScheduleType, StreakMode, StreakRecovery, MetricType, TimerMode, GoalPeriodType, NotifyType, AiDataSharing):

```xml
<ObjectDataProvider x:Key="ScheduleTypeValues" MethodName="GetValues" ObjectType="{x:Type sys:Enum}" xmlns:sys="clr-namespace:System;assembly=mscorlib">
    <ObjectDataProvider.MethodParameters><x:Type TypeName="enums:ScheduleType"/></ObjectDataProvider.MethodParameters>
</ObjectDataProvider>
```

(Repeat per enum with distinct keys: `StreakModeValues`, `StreakRecoveryValues`, `MetricTypeValues`, `TimerModeValues`, `GoalPeriodTypeValues`, `NotifyTypeValues`, `AiDataSharingValues`. Add `xmlns:enums` + `xmlns:sys` to the dictionary root.)

### Task 7: SchedulingSectionView

**Body controls** (bind to `SchedulingSectionViewModel`):

```xml
<TextBlock Text="Schedule type" Foreground="{DynamicResource Brush.TextMuted}"/>
<ComboBox ItemsSource="{Binding Source={StaticResource ScheduleTypeValues}}"
          SelectedItem="{Binding ScheduleType}" Margin="0,2,0,8"/>
<TextBlock Text="Times per week" Foreground="{DynamicResource Brush.TextMuted}"
           Visibility="{Binding ScheduleType, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static enums:ScheduleType.WeeklyX}}"/>
<TextBox Text="{Binding ScheduleWeeklyTarget, UpdateSourceTrigger=LostFocus, TargetNullValue=''}"
         Visibility="{Binding ScheduleType, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static enums:ScheduleType.WeeklyX}}" Margin="0,2,0,8"/>
<TextBlock Text="Every X days" Foreground="{DynamicResource Brush.TextMuted}"
           Visibility="{Binding ScheduleType, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static enums:ScheduleType.EveryXDays}}"/>
<TextBox Text="{Binding ScheduleEveryXDays, UpdateSourceTrigger=LostFocus, TargetNullValue=''}"
         Visibility="{Binding ScheduleType, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static enums:ScheduleType.EveryXDays}}"/>
```

(Day-of-week picker for `SpecificDays`/monthly is deferred — the JSON fields stay unedited this pass; note in commit.)

Register: `<DataTemplate DataType="{x:Type vms:SchedulingSectionViewModel}"><vs:SchedulingSectionView/></DataTemplate>`.
Commit: `feat(wpf): Scheduling section view (B6c)`.

### Task 8: StreakSectionView

```xml
<TextBlock Text="Streak mode" Foreground="{DynamicResource Brush.TextMuted}"/>
<ComboBox ItemsSource="{Binding Source={StaticResource StreakModeValues}}" SelectedItem="{Binding StreakMode}" Margin="0,2,0,8"/>
<TextBlock Text="Recovery" Foreground="{DynamicResource Brush.TextMuted}"/>
<ComboBox ItemsSource="{Binding Source={StaticResource StreakRecoveryValues}}" SelectedItem="{Binding StreakRecovery}" Margin="0,2,0,8"/>
<ToggleButton Content="Advanced" IsChecked="{Binding ShowAdvanced}" Margin="0,4"/>
<StackPanel Visibility="{Binding ShowAdvanced, Converter={StaticResource BoolToVisible}}">
    <TextBlock Text="Tolerance (minutes)" Foreground="{DynamicResource Brush.TextMuted}"/>
    <TextBox Text="{Binding StreakToleranceMinutes, UpdateSourceTrigger=LostFocus, TargetNullValue=''}"/>
</StackPanel>
```

Register + commit `feat(wpf): Streak section view (B6c)`.

### Task 9: MetricsSectionView

```xml
<ItemsControl ItemsSource="{Binding Metrics}">
    <ItemsControl.ItemTemplate>
        <DataTemplate DataType="{x:Type vms:MetricRowViewModel}">
            <Grid Margin="0,2">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/><ColumnDefinition Width="110"/>
                    <ColumnDefinition Width="110"/><ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBox Grid.Column="0" Text="{Binding Name, UpdateSourceTrigger=LostFocus}"/>
                <ComboBox Grid.Column="1" Margin="4,0"
                          ItemsSource="{Binding Source={StaticResource MetricTypeValues}}"
                          SelectedItem="{Binding Type}"/>
                <ComboBox Grid.Column="2" Margin="0,0,4,0"
                          ItemsSource="{Binding DataContext.AvailableUnits, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                          DisplayMemberPath="Name" SelectedValuePath="Id"
                          SelectedValue="{Binding UnitOfMeasureId}"/>
                <Button Grid.Column="3" Content="✕" Width="28"
                        Command="{Binding DataContext.RemoveMetricCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                        CommandParameter="{Binding}"/>
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
<Button Content="+ Add metric" Command="{Binding AddMetricCommand}" HorizontalAlignment="Left" Margin="0,6,0,0"/>
```

Register + commit `feat(wpf): Metrics section view (B6c)`.

### Task 10: ScalesSectionView

```xml
<TextBlock Text="Scale" Foreground="{DynamicResource Brush.TextMuted}"/>
<ComboBox ItemsSource="{Binding AvailableScales}" DisplayMemberPath="Name" SelectedValuePath="Id"
          SelectedValue="{Binding SelectedScaleTemplateId}" Margin="0,2,0,8"/>
<TextBlock Text="Custom labels (JSON array, overrides scale)" Foreground="{DynamicResource Brush.TextMuted}"/>
<TextBox Text="{Binding LabelsJson, UpdateSourceTrigger=LostFocus, TargetNullValue=''}" AcceptsReturn="True" MinHeight="48"/>
```

Register + commit `feat(wpf): Scales section view (B6c)`.

### Task 11: TimerSectionView

```xml
<CheckBox Content="Integrated timer" IsChecked="{Binding HasIntegratedTimer}" Margin="0,0,0,6"/>
<TextBlock Text="Timer mode" Foreground="{DynamicResource Brush.TextMuted}"/>
<ComboBox ItemsSource="{Binding Source={StaticResource TimerModeValues}}" SelectedItem="{Binding TimerMode}" Margin="0,2,0,8"/>
<TextBlock Text="Target seconds" Foreground="{DynamicResource Brush.TextMuted}"/>
<TextBox Text="{Binding TimerTargetSeconds, UpdateSourceTrigger=LostFocus, TargetNullValue=''}" Margin="0,2,0,8"/>
<TextBlock Text="Segments" Foreground="{DynamicResource Brush.TextMuted}" Margin="0,4,0,2"/>
<ItemsControl ItemsSource="{Binding Segments}">
    <ItemsControl.ItemTemplate>
        <DataTemplate DataType="{x:Type vms:SegmentRowViewModel}">
            <Grid Margin="0,2">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                <TextBox Grid.Column="0" Text="{Binding Name, UpdateSourceTrigger=LostFocus}"/>
                <CheckBox Grid.Column="1" Content="Rest" IsChecked="{Binding IsRest}" Margin="6,0" VerticalAlignment="Center"/>
                <Button Grid.Column="2" Content="✕" Width="28"
                        Command="{Binding DataContext.RemoveSegmentCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                        CommandParameter="{Binding}"/>
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
<Button Content="+ Add segment" Command="{Binding AddSegmentCommand}" HorizontalAlignment="Left" Margin="0,6,0,0"/>
<ToggleButton Content="Advanced" IsChecked="{Binding ShowAdvanced}" Margin="0,6,0,0"/>
<StackPanel Visibility="{Binding ShowAdvanced, Converter={StaticResource BoolToVisible}}">
    <CheckBox Content="Auto-prompt" IsChecked="{Binding TimerAutoPrompt}" Margin="0,4,0,0"/>
    <TextBlock Text="Rest timer seconds" Foreground="{DynamicResource Brush.TextMuted}"/>
    <TextBox Text="{Binding RestTimerSeconds, UpdateSourceTrigger=LostFocus, TargetNullValue=''}"/>
</StackPanel>
```

Register + commit `feat(wpf): Timer section view (B6c)`.

### Task 12: GoalSectionView

```xml
<TextBlock Text="Goal name" Foreground="{DynamicResource Brush.TextMuted}"/>
<TextBox Text="{Binding GoalName, UpdateSourceTrigger=LostFocus}" Margin="0,2,0,8"/>
<TextBlock Text="Target" Foreground="{DynamicResource Brush.TextMuted}"/>
<TextBox Text="{Binding TargetValue, UpdateSourceTrigger=LostFocus}" Margin="0,2,0,8"/>
<TextBlock Text="Period" Foreground="{DynamicResource Brush.TextMuted}"/>
<ComboBox ItemsSource="{Binding Source={StaticResource GoalPeriodTypeValues}}" SelectedItem="{Binding PeriodType}" Margin="0,2,0,8"/>
<ToggleButton Content="Advanced" IsChecked="{Binding ShowAdvanced}"/>
<StackPanel Visibility="{Binding ShowAdvanced, Converter={StaticResource BoolToVisible}}">
    <TextBlock Text="Rolling days" Foreground="{DynamicResource Brush.TextMuted}"/>
    <TextBox Text="{Binding RollingDays, UpdateSourceTrigger=LostFocus, TargetNullValue=''}"/>
</StackPanel>
```

Register + commit `feat(wpf): Goal section view (B6c)`.

### Task 13: NotificationsSectionView

```xml
<ItemsControl ItemsSource="{Binding Notifications}">
    <ItemsControl.ItemTemplate>
        <DataTemplate DataType="{x:Type vms:NotificationRowViewModel}">
            <Grid Margin="0,2">
                <Grid.ColumnDefinitions><ColumnDefinition Width="130"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                <ComboBox Grid.Column="0" ItemsSource="{Binding Source={StaticResource NotifyTypeValues}}" SelectedItem="{Binding NotifyType}"/>
                <TextBox Grid.Column="1" Margin="4,0" Text="{Binding NotifyTime, UpdateSourceTrigger=LostFocus, StringFormat=HH\\:mm, TargetNullValue=''}"/>
                <CheckBox Grid.Column="2" IsChecked="{Binding IsEnabled}" VerticalAlignment="Center" Margin="0,0,6,0"/>
                <Button Grid.Column="3" Content="✕" Width="28"
                        Command="{Binding DataContext.RemoveNotificationCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                        CommandParameter="{Binding}"/>
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
<Button Content="+ Add notification" Command="{Binding AddNotificationCommand}" HorizontalAlignment="Left" Margin="0,6,0,0"/>
```

Register + commit `feat(wpf): Notifications section view (B6c)`.

### Task 14: TagsSectionView

```xml
<ItemsControl ItemsSource="{Binding AvailableTags}">
    <ItemsControl.ItemsPanel><ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate DataType="{x:Type models:Tag}" xmlns:models="clr-namespace:Recordum.Core.Models;assembly=Recordum.Core">
            <Button Content="{Binding Name}" Margin="0,0,6,6"
                    Command="{Binding DataContext.ToggleTagCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                    CommandParameter="{Binding}"/>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

(Selected-state styling of chips is a follow-up refinement; toggling persists via the command.) Register + commit `feat(wpf): Tags section view (B6c)`.

### Task 15: AiSharingSectionView

```xml
<TextBlock Text="AI data sharing" Foreground="{DynamicResource Brush.TextMuted}"/>
<ComboBox ItemsSource="{Binding Source={StaticResource AiDataSharingValues}}" SelectedItem="{Binding AiDataSharing}" Margin="0,2,0,0"/>
```

Register + commit `feat(wpf): AI sharing section view (B6c)`.

### Task 16: ResetTimeSectionView

```xml
<TextBlock Text="Reset time (HH:mm, empty = inherit)" Foreground="{DynamicResource Brush.TextMuted}"/>
<TextBox Text="{Binding ResetTimeText, UpdateSourceTrigger=LostFocus}" Width="80" HorizontalAlignment="Left" Margin="0,2,0,0"/>
```

Register + commit `feat(wpf): Reset time section view (B6c)`.

---

## Task 17: Full verification

- [ ] **Step 1: Run all suites**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj` and `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj`
Expected: all green.

- [ ] **Step 2: Visual verification (run skill)**

Build + launch. Create/open a **Counter** habit → Configura: Scheduling, Streak, Metrics (add metric + pick unit), Timer (add segment), Goal, Notifications, Tags, AI, Reset time all appear; Scales hidden. Open a **State** habit: Scales appears (pick a scale), Metrics/Timer hidden. Edit a field → footer shows "Saved"; reopen the habit → value persisted. Confirm with the user.

- [ ] **Step 3: Commit any final fixes**

```bash
git commit -am "test(wpf): editor sections verified end-to-end (B6c)"
```
