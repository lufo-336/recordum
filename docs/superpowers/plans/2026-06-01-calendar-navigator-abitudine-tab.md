# CalendarNavigator + Abitudine tab (Piano 2a) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sostituire la striscia heatmap della prima tab di `HabitScreen` con un **calendario mensile navigabile** (`CalendarNavigator`) che mostra marcatori "completato" (colore della habit) e "nota" (colore configurabile), rinominando la tab in **Abitudine**; selezionare un giorno guida la marcatura per-tipo già esistente (`QuickAdd.CompletionDate`).

**Architecture:** Un widget riusabile `CalendarNavigator` (UserControl WPF + `CalendarNavigatorViewModel`) **disaccoppiato** dal dominio: riceve i marcatori del mese tramite un delegate `CalendarMarkerProvider` (così la futura colonna Gruppo lo riuserà con marcatori aggregati). In `HabitScreen` il provider compone due servizi già esistenti — `ICompletionService.GetHeatmapAsync` (giorni completati) e `INoteService.ListNoteDaysInMonthAsync` (giorni con nota, da Piano 1). Il giorno selezionato pilota `QuickAdd.CompletionDate`, esattamente come fa oggi `Heatmap.DateClicked`. Nessuna modifica schema; nessun nuovo servizio.

**Tech Stack:** C# / .NET 10, WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions.

**Riferimento spec:** `docs/superpowers/specs/2026-06-01-abitudine-column-calendar-design.md` §4, §5, §6. NB: lo spec descrive le colonne-chip; per decisione utente Abitudine vive nei **tab attuali** ora (rimpiazza la tab Cronologia). L'editor nota-del-giorno e la timeline Note sono **Piano 2b** (fuori da questo piano).

---

## File Structure

- **Create** `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarDayCellViewModel.cs` — un giorno della griglia (data, numero, flag in-mese/oggi/selezionato/futuro/completato/nota).
- **Create** `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs` — stato mese, griglia 6×7, nav mesi, salto mese/anno, marcatori, selezione. Espone `CalendarMarkerProvider` (delegate) + `record CalendarMonthMarkers`.
- **Create** `src/Recordum.WPF/Controls/HexToBrushConverter.cs` — converte stringa hex (#RRGGBB) → `SolidColorBrush` (per i pallini marcatore).
- **Create** `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml` (+ `.xaml.cs`) — il controllo visivo.
- **Modify** `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` — istanzia `CalendarNavigatorViewModel`, gli passa il marker provider composto + i colori, collega `SelectedDateChanged → QuickAdd.CompletionDate`, ricarica i marcatori sugli eventi Completion.
- **Modify** `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` — nella prima tab, rimpiazza `ContentControl Content="{Binding Heatmap}"` con `<v:CalendarNavigator DataContext="{Binding Calendar}"/>`; cambia l'header tab in `HabitScreen_Tab_Abitudine`.
- **Modify** `src/Recordum.WPF/Resources/Strings.resx` + `Strings.it.resx` — nuova chiave `HabitScreen_Tab_Abitudine`.
- **Tests** `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModelTests.cs` (new), e un test su `HabitScreenViewModel` per il wiring selezione→QuickAdd.

> Il vecchio `HeatmapViewModel` resta in uso nella tab **Dettaglio** (year-heatmap) — NON va rimosso. Qui si stacca solo dalla prima tab.

---

## Task 1: `CalendarNavigatorViewModel` + `CalendarDayCellViewModel` (logica, TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarDayCellViewModel.cs`
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModelTests.cs`:

```csharp
using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Calendar;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Calendar;

public class CalendarNavigatorViewModelTests
{
    // Clock fixed at 2026-06-15 (UTC) so "today" is deterministic.
    private static CalendarNavigatorViewModel NewSut(out FakeClock clock,
        CalendarMarkerProvider? provider = null)
    {
        clock = new FakeClock(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var sut = new CalendarNavigatorViewModel(clock) { CompletedColorHex = "#e08a4a", NoteColorHex = "#e0b341" };
        if (provider is not null) sut.MarkerProvider = provider;
        return sut;
    }

    [Fact]
    public async Task LoadAsync_builds_42_cells_with_correct_in_month_flags()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null);

        sut.Cells.Should().HaveCount(42);
        sut.Cells.Count(c => c.InCurrentMonth).Should().Be(30); // June has 30 days
        // First in-month cell is June 1, last is June 30.
        sut.Cells.First(c => c.InCurrentMonth).Date.Should().Be(new DateOnly(2026, 6, 1));
        sut.Cells.Last(c => c.InCurrentMonth).Date.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Fact]
    public async Task LoadAsync_flags_today_and_future()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null);

        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 15)).IsToday.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 16)).IsFuture.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 14)).IsFuture.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_applies_markers_from_provider()
    {
        CalendarMarkerProvider provider = (y, m, ct) => Task.FromResult(
            new CalendarMonthMarkers(
                Completed: new HashSet<DateOnly> { new(2026, 6, 1), new(2026, 6, 2) },
                Notes: new HashSet<DateOnly> { new(2026, 6, 1), new(2026, 6, 9) }));
        var sut = NewSut(out _, provider);

        await sut.LoadAsync(2026, 6, selected: null);

        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 1)).Completed.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 1)).HasNote.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 2)).Completed.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 2)).HasNote.Should().BeFalse();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 9)).HasNote.Should().BeTrue();
    }

    [Fact]
    public async Task NextMonth_then_PrevMonth_returns_to_start_and_rolls_year()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 12, selected: null);

        await sut.NextMonthCommand.ExecuteAsync(null);
        sut.DisplayYear.Should().Be(2027);
        sut.DisplayMonth.Should().Be(1);

        await sut.PrevMonthCommand.ExecuteAsync(null);
        sut.DisplayYear.Should().Be(2026);
        sut.DisplayMonth.Should().Be(12);
    }

    [Fact]
    public async Task SelectDate_on_past_cell_sets_selection_and_raises_event()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null);
        DateOnly? raised = null;
        sut.SelectedDateChanged += (_, d) => raised = d;

        var cell = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 10));
        sut.SelectDateCommand.Execute(cell);

        sut.SelectedDate.Should().Be(new DateOnly(2026, 6, 10));
        raised.Should().Be(new DateOnly(2026, 6, 10));
        cell.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task SelectDate_on_future_cell_is_ignored()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null);
        var future = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 20));

        sut.SelectDateCommand.Execute(future);

        sut.SelectedDate.Should().BeNull();
        future.IsSelected.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run to verify FAIL** (types don't exist)

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~CalendarNavigatorViewModelTests" --nologo`
Expected: FAIL (compile — `CalendarNavigatorViewModel`/`CalendarDayCellViewModel`/`CalendarMarkerProvider`/`CalendarMonthMarkers` missing).

> Before Step 3, open `tests/Recordum.WPF.Tests/Fixtures/FakeClock.cs` and confirm it has a constructor taking a `DateTimeOffset` (the test uses `new FakeClock(new DateTimeOffset(...))`). If the ctor differs, adapt the test's clock construction to the real FakeClock API — do NOT change FakeClock.

- [ ] **Step 3: Create the day-cell view-model**

Create `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarDayCellViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.WPF.ViewModels.HabitScreen.Calendar;

/// <summary>One day cell in the <see cref="CalendarNavigatorViewModel"/> month grid.</summary>
public sealed partial class CalendarDayCellViewModel : ObservableObject
{
    public DateOnly Date { get; }
    public int DayNumber => Date.Day;
    public bool InCurrentMonth { get; }
    public bool IsToday { get; }
    public bool IsFuture { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _completed;
    [ObservableProperty] private bool _hasNote;

    public CalendarDayCellViewModel(DateOnly date, bool inCurrentMonth, bool isToday, bool isFuture)
    {
        Date = date;
        InCurrentMonth = inCurrentMonth;
        IsToday = isToday;
        IsFuture = isFuture;
    }
}
```

- [ ] **Step 4: Create the navigator view-model**

Create `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;

namespace Recordum.WPF.ViewModels.HabitScreen.Calendar;

/// <summary>Completed/note day-markers for one month, supplied by the host.</summary>
public sealed record CalendarMonthMarkers(IReadOnlySet<DateOnly> Completed, IReadOnlySet<DateOnly> Notes);

/// <summary>Host-supplied source of month markers (decouples the calendar from the domain).</summary>
public delegate Task<CalendarMonthMarkers> CalendarMarkerProvider(int year, int month, CancellationToken ct);

/// <summary>
/// Reusable month-grid calendar: navigates months/years, renders a 6×7 grid with today/selected
/// highlighting and completed/note markers, and raises <see cref="SelectedDateChanged"/> when a
/// non-future day is picked. Markers come from <see cref="MarkerProvider"/> (set by the host).
/// </summary>
public sealed partial class CalendarNavigatorViewModel : ObservableObject
{
    private readonly DateOnly _today;

    public CalendarNavigatorViewModel(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow.UtcDateTime, clock.LocalTimeZone);
        _today = DateOnly.FromDateTime(localNow);
        WeekdayHeaders = BuildWeekdayHeaders();
        SelectableYears = Enumerable.Range(_today.Year - 5, 8).ToList(); // -5..+2
    }

    /// <summary>Optional source of markers. If null, cells render with no markers.</summary>
    public CalendarMarkerProvider? MarkerProvider { get; set; }

    /// <summary>Hex color (#RRGGBB) for the "completed" dot — set by the host to the habit color.</summary>
    [ObservableProperty] private string _completedColorHex = "#808080";

    /// <summary>Hex color (#RRGGBB) for the "note" dot — set by the host (HabitConfig.NoteMarkerColor or default).</summary>
    [ObservableProperty] private string _noteColorHex = "#e0b341";

    [ObservableProperty] private int _displayYear;
    [ObservableProperty] private int _displayMonth;
    [ObservableProperty] private string _monthLabel = string.Empty;
    [ObservableProperty] private DateOnly? _selectedDate;

    public ObservableCollection<CalendarDayCellViewModel> Cells { get; } = [];
    public IReadOnlyList<string> WeekdayHeaders { get; }
    public IReadOnlyList<int> SelectableYears { get; }
    public IReadOnlyList<int> SelectableMonths { get; } = Enumerable.Range(1, 12).ToList();

    /// <summary>Raised when the user picks a non-future day.</summary>
    public event EventHandler<DateOnly>? SelectedDateChanged;

    /// <summary>Loads a month into the grid, applies markers, and sets the selection (without raising the event).</summary>
    public async Task LoadAsync(int year, int month, DateOnly? selected, CancellationToken ct = default)
    {
        DisplayYear = year;
        DisplayMonth = month;
        SelectedDate = selected;
        MonthLabel = new DateOnly(year, month, 1)
            .ToString("MMMM yyyy", CultureInfo.CurrentCulture);

        BuildGrid(year, month);
        await ApplyMarkersAsync(year, month, ct).ConfigureAwait(true);
        RefreshSelection();
    }

    private void BuildGrid(int year, int month)
    {
        Cells.Clear();
        var first = new DateOnly(year, month, 1);
        int firstDow = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int leading = ((int)first.DayOfWeek - firstDow + 7) % 7;
        var start = first.AddDays(-leading);
        for (int i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            Cells.Add(new CalendarDayCellViewModel(
                date,
                inCurrentMonth: date.Month == month && date.Year == year,
                isToday: date == _today,
                isFuture: date > _today));
        }
    }

    private async Task ApplyMarkersAsync(int year, int month, CancellationToken ct)
    {
        if (MarkerProvider is null) return;
        var markers = await MarkerProvider(year, month, ct).ConfigureAwait(true);
        foreach (var cell in Cells)
        {
            cell.Completed = markers.Completed.Contains(cell.Date);
            cell.HasNote = markers.Notes.Contains(cell.Date);
        }
    }

    private void RefreshSelection()
    {
        foreach (var cell in Cells)
            cell.IsSelected = SelectedDate is { } d && cell.Date == d;
    }

    [RelayCommand]
    private Task PrevMonthAsync(CancellationToken ct)
    {
        var prev = new DateOnly(DisplayYear, DisplayMonth, 1).AddMonths(-1);
        return LoadAsync(prev.Year, prev.Month, SelectedDate, ct);
    }

    [RelayCommand]
    private Task NextMonthAsync(CancellationToken ct)
    {
        var next = new DateOnly(DisplayYear, DisplayMonth, 1).AddMonths(1);
        return LoadAsync(next.Year, next.Month, SelectedDate, ct);
    }

    /// <summary>Reloads the currently selected year/month (bound to the jump dropdowns).</summary>
    public Task JumpAsync(CancellationToken ct = default) => LoadAsync(DisplayYear, DisplayMonth, SelectedDate, ct);

    [RelayCommand]
    private void SelectDate(CalendarDayCellViewModel? cell)
    {
        if (cell is null || cell.IsFuture) return;
        SelectedDate = cell.Date;
        RefreshSelection();
        SelectedDateChanged?.Invoke(this, cell.Date);
    }

    private static IReadOnlyList<string> BuildWeekdayHeaders()
    {
        var dtf = CultureInfo.CurrentCulture.DateTimeFormat;
        int firstDow = (int)dtf.FirstDayOfWeek;
        var names = new List<string>(7);
        for (int i = 0; i < 7; i++)
            names.Add(dtf.ShortestDayNames[(firstDow + i) % 7]);
        return names;
    }
}
```

- [ ] **Step 5: Run to verify PASS**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~CalendarNavigatorViewModelTests" --nologo`
Expected: PASS (6 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Calendar/ tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/
git commit -m "feat(wpf): CalendarNavigatorViewModel — month grid + markers + selection (Abitudine)"
```

---

## Task 2: `HexToBrushConverter` + `CalendarNavigator` control

**Files:**
- Create: `src/Recordum.WPF/Controls/HexToBrushConverter.cs`
- Create: `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml` (+ `.xaml.cs`)

- [ ] **Step 1: Create the hex→brush converter**

Create `src/Recordum.WPF/Controls/HexToBrushConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Controls;

/// <summary>Converts a #RRGGBB hex string to a frozen <see cref="SolidColorBrush"/>. Invalid/null → Transparent.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(s)!;
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch (FormatException) { /* fall through */ }
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 2: Create the control XAML**

Create `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml`:

```xml
<UserControl x:Class="Recordum.WPF.Views.HabitScreen.Calendar.CalendarNavigator"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Recordum.WPF.ViewModels.HabitScreen.Calendar"
             xmlns:ctrl="clr-namespace:Recordum.WPF.Controls"
             d:DataContext="{d:DesignInstance vm:CalendarNavigatorViewModel}"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d">
    <UserControl.Resources>
        <ctrl:HexToBrushConverter x:Key="HexToBrush"/>
    </UserControl.Resources>
    <Border Padding="12" BorderThickness="0,0,0,1" BorderBrush="{DynamicResource Brush.Border}">
        <StackPanel>
            <!-- Jump bar: prev / month+year dropdowns / next -->
            <Grid Margin="0,0,0,8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <Button Grid.Column="0" Content="‹" Width="28" Command="{Binding PrevMonthCommand}"/>
                <TextBlock Grid.Column="1" Text="{Binding MonthLabel}" VerticalAlignment="Center"
                           Margin="8,0" FontWeight="SemiBold" Foreground="{DynamicResource Brush.Text}"/>
                <ComboBox Grid.Column="2" Width="64" Margin="0,0,4,0"
                          ItemsSource="{Binding SelectableMonths}"
                          SelectedItem="{Binding DisplayMonth}"
                          SelectionChanged="OnJumpChanged"/>
                <ComboBox Grid.Column="3" Width="74"
                          ItemsSource="{Binding SelectableYears}"
                          SelectedItem="{Binding DisplayYear}"
                          SelectionChanged="OnJumpChanged"/>
                <Button Grid.Column="4" Content="›" Width="28" Margin="4,0,0,0" Command="{Binding NextMonthCommand}"/>
            </Grid>

            <!-- Weekday headers -->
            <ItemsControl ItemsSource="{Binding WeekdayHeaders}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate><UniformGrid Columns="7"/></ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <TextBlock Text="{Binding}" HorizontalAlignment="Center"
                                   FontSize="{StaticResource FontSize.Small}"
                                   Foreground="{DynamicResource Brush.TextMuted}"/>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

            <!-- Day grid -->
            <ItemsControl ItemsSource="{Binding Cells}" Margin="0,4,0,0">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate><UniformGrid Columns="7" Rows="6"/></ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate DataType="{x:Type vm:CalendarDayCellViewModel}">
                        <Button Margin="2" Padding="0" Height="34"
                                BorderThickness="1"
                                Command="{Binding DataContext.SelectDateCommand,
                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                CommandParameter="{Binding}"
                                IsEnabled="{Binding IsFuture, Converter={StaticResource InvBoolConverter}}">
                            <Button.Style>
                                <Style TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
                                    <Setter Property="Background" Value="Transparent"/>
                                    <Setter Property="BorderBrush" Value="Transparent"/>
                                    <Setter Property="Foreground" Value="{DynamicResource Brush.Text}"/>
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding InCurrentMonth}" Value="False">
                                            <Setter Property="Foreground" Value="{DynamicResource Brush.TextMuted}"/>
                                            <Setter Property="Opacity" Value="0.45"/>
                                        </DataTrigger>
                                        <DataTrigger Binding="{Binding IsToday}" Value="True">
                                            <Setter Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
                                        </DataTrigger>
                                        <DataTrigger Binding="{Binding IsSelected}" Value="True">
                                            <Setter Property="Background" Value="{DynamicResource Brush.Accent}"/>
                                            <Setter Property="Foreground" Value="White"/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </Button.Style>
                            <Grid>
                                <TextBlock Text="{Binding DayNumber}" HorizontalAlignment="Right"
                                           VerticalAlignment="Top" Margin="0,2,4,0"
                                           FontSize="{StaticResource FontSize.Small}"/>
                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Left"
                                            VerticalAlignment="Bottom" Margin="4,0,0,3">
                                    <Ellipse Width="5" Height="5" Margin="0,0,2,0"
                                             Visibility="{Binding Completed, Converter={StaticResource BoolToVisible}}"
                                             Fill="{Binding DataContext.CompletedColorHex,
                                                    RelativeSource={RelativeSource AncestorType=ItemsControl},
                                                    Converter={StaticResource HexToBrush}}"/>
                                    <Ellipse Width="5" Height="5"
                                             Visibility="{Binding HasNote, Converter={StaticResource BoolToVisible}}"
                                             Fill="{Binding DataContext.NoteColorHex,
                                                    RelativeSource={RelativeSource AncestorType=ItemsControl},
                                                    Converter={StaticResource HexToBrush}}"/>
                                </StackPanel>
                            </Grid>
                        </Button>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </StackPanel>
    </Border>
</UserControl>
```

> The converters `BoolToVisible` and `InvBoolConverter` are referenced by other views (e.g. `HabitScreen.xaml` uses `BoolToVisible`/`InvBoolToVisible`). Before writing, grep `App.xaml` for the exact keys: `BoolToVisible`, and an inverse-bool converter for `IsEnabled`. If the project's inverse-bool key is named differently (e.g. `InvBoolToVisible` is visibility, not bool), find the bool-inverting converter key actually registered; if NONE exists, bind `IsEnabled="{Binding IsFuture}"` plus a `Style` trigger that disables on `IsFuture=True` instead of a converter. Use what the codebase already provides.

- [ ] **Step 3: Create the code-behind (jump dropdown handler)**

Create `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml.cs`:

```csharp
using System.Windows.Controls;
using Recordum.WPF.ViewModels.HabitScreen.Calendar;

namespace Recordum.WPF.Views.HabitScreen.Calendar;

public partial class CalendarNavigator : UserControl
{
    public CalendarNavigator() => InitializeComponent();

    private void OnJumpChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CalendarNavigatorViewModel vm)
            _ = vm.JumpAsync();
    }
}
```

- [ ] **Step 4: Build (no unit test for XAML; visual verification happens at the end)**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj --nologo`
Expected: build OK. If a converter key referenced in the XAML does not exist, fix per the note in Step 2 (use an existing key or a Style trigger).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Controls/HexToBrushConverter.cs src/Recordum.WPF/Views/HabitScreen/Calendar/
git commit -m "feat(wpf): CalendarNavigator control + HexToBrush converter"
```

---

## Task 3: Integrate into `HabitScreen` (first tab → Abitudine)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenCalendarWiringTests.cs` (new)

- [ ] **Step 1: Write the failing wiring test**

Create `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenCalendarWiringTests.cs`. It verifies that (a) the screen exposes a `Calendar` navigator, (b) picking a day routes into `QuickAdd.CompletionDate`, and (c) the marker provider unions completions (from the heatmap query) and note-days.

```csharp
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.HabitScreen.Calendar;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HabitScreenCalendarWiringTests
{
    [Fact]
    public async Task Selecting_a_calendar_day_sets_quickadd_completion_date()
    {
        var fx = HabitScreenViewModelFactory.Create(out var nodes); // see note below
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };

        await fx.LoadAsync(id);
        await fx.Calendar.LoadAsync(2026, 6, selected: null);

        var cell = fx.Calendar.Cells.First(c => c.InCurrentMonth && !c.IsFuture);
        fx.Calendar.SelectDateCommand.Execute(cell);

        fx.QuickAdd!.CompletionDate.Date.Should().Be(cell.Date.ToDateTime(TimeOnly.MinValue).Date);
    }
}
```

> **Harness note:** `HabitScreenViewModel` has a 14-dependency constructor. Check `tests/Recordum.WPF.Tests/Fixtures/` for an existing factory (e.g. `MainWindowViewModelFactory`) or how other `HabitScreenViewModel` tests build it. If a `HabitScreenViewModelFactory` does not exist, create a tiny one in `tests/Recordum.WPF.Tests/Fixtures/HabitScreenViewModelFactory.cs` that news up `HabitScreenViewModel` with the existing fakes (`FakeHabitNodeService`, `FakeCompletionService`, `FakeActiveTimerService`, `FakeMetricDefinitionService`, `FakeHabitConfigService`, `FakeNoteService`, `FakeGoalService`, a ramp resolver, `FakeStreakAggregator`, `FakeTrendQueryService`, `FakeHourlyDistributionQueryService`, `FakeClock`, `FakeEventBus`, a WebView2 availability fake) and returns the VM plus the `FakeHabitNodeService`. Mirror however existing HabitScreen tests construct the VM. Keep the clock at a date where June 2026 days are in the past/today if needed, or load a month in the past.

- [ ] **Step 2: Run to verify FAIL** (`Calendar` property missing)

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~HabitScreenCalendarWiringTests" --nologo`
Expected: FAIL (no `Calendar` on `HabitScreenViewModel`).

- [ ] **Step 3: Add the Calendar navigator to `HabitScreenViewModel`**

In `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`:

Add the using at the top:
```csharp
using Recordum.WPF.ViewModels.HabitScreen.Calendar;
```

Add the property near the other sub-VMs (after `public DetailViewModel Detail { get; }`):
```csharp
    public CalendarNavigatorViewModel Calendar { get; }
```

In the constructor, after `Detail = new DetailViewModel(...);` and the existing `Heatmap.DateClicked += OnHeatmapDateClicked;`, construct and wire the calendar:
```csharp
        Calendar = new CalendarNavigatorViewModel(clock) { MarkerProvider = LoadMonthMarkersAsync };
        Calendar.SelectedDateChanged += OnCalendarDateSelected;
```

Add the marker provider + the selection handler as private methods (anywhere in the class):
```csharp
    private async Task<CalendarMonthMarkers> LoadMonthMarkersAsync(int year, int month, CancellationToken ct)
    {
        if (Node is null)
            return new CalendarMonthMarkers(new HashSet<DateOnly>(), new HashSet<DateOnly>());

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var heatmap = await _completions.GetHeatmapAsync(Node.Id, from, to, ct).ConfigureAwait(true);
        var completed = heatmap.Where(c => c.Count > 0).Select(c => c.Date).ToHashSet();

        var noteDays = await _noteService.ListNoteDaysInMonthAsync(Node.Id, year, month, ct).ConfigureAwait(true);
        return new CalendarMonthMarkers(completed, noteDays.ToHashSet());
    }

    private void OnCalendarDateSelected(object? sender, DateOnly date)
    {
        if (QuickAdd is not null)
            QuickAdd.CompletionDate = date.ToDateTime(TimeOnly.MinValue);
    }
```

> `_completions` already exists as a field. **Add a `_noteService` field**: the ctor receives `INoteService noteService` but currently only passes it to `Notes`. Add `private readonly INoteService _noteService;` next to the other fields and assign `_noteService = noteService;` in the ctor body (next to `_completions = completions;`).

In `LoadAsync`, after the node is loaded and its color is known, set the calendar colors and load the current month. Add to the parallel-load block (alongside `heatmapTask`, etc.):
```csharp
        Calendar.CompletedColorHex = Node.Color;
        Calendar.NoteColorHex = configTask.Result?.NoteMarkerColor ?? "#e0b341";
```
> Note `configTask` is awaited as part of `Task.WhenAll`; read `configTask.Result` AFTER the `await Task.WhenAll(...)` line. Place the two color assignments and the initial month load right after `await Task.WhenAll(...)` and before/after `TimerControl.SetContextAsync`. Then load the month:
```csharp
        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow.UtcDateTime, _clock.LocalTimeZone);
        await Calendar.LoadAsync(todayLocal.Year, todayLocal.Month, DateOnly.FromDateTime(todayLocal), ct).ConfigureAwait(true);
```

In `OnCompletionEvent` (which already reloads Heatmap + CompletionList), also refresh the calendar markers for its current month:
```csharp
        await Task.WhenAll(
            Heatmap.LoadAsync(Node.Id),
            CompletionList.LoadAsync(Node.Id),
            Calendar.JumpAsync())
            .ConfigureAwait(true);
```

- [ ] **Step 4: Run the wiring test to verify PASS**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~HabitScreenCalendarWiringTests" --nologo`
Expected: PASS.

- [ ] **Step 5: Swap the view — calendar replaces the heatmap strip in the first tab**

In `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`:

Add the calendar namespace to the `UserControl` opening tag:
```xml
             xmlns:cal="clr-namespace:Recordum.WPF.Views.HabitScreen.Calendar"
```

In the first `TabItem` (header `HabitScreen_Tab_Completion`), replace:
```xml
                    <ContentControl Grid.Row="0" Content="{Binding Heatmap}"/>
```
with:
```xml
                    <cal:CalendarNavigator Grid.Row="0" DataContext="{Binding Calendar}"/>
```

Change that tab's header from:
```xml
            <TabItem Header="{x:Static res:Strings.HabitScreen_Tab_Completion}">
```
to:
```xml
            <TabItem Header="{x:Static res:Strings.HabitScreen_Tab_Abitudine}">
```

> Leave the `HeatmapViewModel` DataTemplate in `HabitScreen.xaml` resources untouched — it may still be referenced; removing it is out of scope. (The `Heatmap` sub-VM also still feeds nothing visually here now, but stays wired for the Detail year-heatmap path and the `OnHeatmapDateClicked` handler; do not delete it in this task.)

- [ ] **Step 6: Build**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj --nologo`
Expected: build OK (the new `Strings.HabitScreen_Tab_Abitudine` is added in Task 4; if the build runs before Task 4, temporarily it will fail on the missing resource — so do Task 4 Step 1-2 first if building now, OR proceed to Task 4 then build).

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenCalendarWiringTests.cs tests/Recordum.WPF.Tests/Fixtures/
git commit -m "feat(wpf): Abitudine tab — CalendarNavigator drives day marking (replaces heatmap strip)"
```

---

## Task 4: Strings + final verification

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx`
- Modify: `src/Recordum.WPF/Resources/Strings.it.resx`

- [ ] **Step 1: Add the tab label resource**

Open `src/Recordum.WPF/Resources/Strings.resx`, find the existing `HabitScreen_Tab_Completion` entry, and add a sibling entry:
```xml
  <data name="HabitScreen_Tab_Abitudine" xml:space="preserve">
    <value>Habit</value>
  </data>
```
Then in `src/Recordum.WPF/Resources/Strings.it.resx`, add:
```xml
  <data name="HabitScreen_Tab_Abitudine" xml:space="preserve">
    <value>Abitudine</value>
  </data>
```
> The generated `Strings.Designer.cs` usually regenerates on build; if it does not, add a matching static property mirroring how `HabitScreen_Tab_Completion` is declared there.

- [ ] **Step 2: Build + full WPF + Data suites**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj --nologo` (expect OK)
Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --nologo` (expect ALL pass, including the new calendar + wiring tests)
Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --nologo` (expect unchanged green)

- [ ] **Step 3: Commit**

```bash
git add src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs
git commit -m "i18n(wpf): Abitudine tab label (EN Habit / IT Abitudine)"
```

- [ ] **Step 4: Visual verification (skill `run`)**

Launch the app (skill `run`), open a habit, confirm: the first tab is now **Abitudine** with a navigable month calendar; ‹ › and the month/year dropdowns change months; today is ringed; clicking a past/today day selects it (accent fill) and the quick-add below targets that date; days with completions show a dot in the habit color; days with notes show the note-color dot; future days are not clickable. Note any visual issues for follow-up (do not fix here unless trivial).

---

## Self-Review

- **Spec coverage:** §5 CalendarNavigator (grid, jump dropdown, ‹›, today/selected, markers completed=habit color / note=config color, weekday headers, future non-selectable) → Tasks 1-2 ✓. §6 day selection drives per-type marking (existing QuickAdd) → Task 3 (`OnCalendarDateSelected → QuickAdd.CompletionDate`) ✓. §4 Abitudine replaces Cronologia tab → Task 3 Step 5 + Task 4 ✓. Marker source composes existing `GetHeatmapAsync` + Piano-1 `ListNoteDaysInMonthAsync` → Task 3 ✓. Reusability via `CalendarMarkerProvider` delegate (for future Gruppo) → Task 1 ✓.
- **Deferred to Piano 2b (NOT gaps):** unified day-note editor inside Abitudine; Note column timeline with date badges. Deferred to Piano 3: the note-color **picker** in Configura (here `NoteMarkerColor` is only *consumed*, defaulting to amber when null).
- **Placeholder scan:** every code step has complete code. Two explicit "verify the real API" notes (FakeClock ctor in Task 1 Step 2; converter keys + HabitScreen VM test factory in Task 2/Task 3) — these are codebase-adaptation points, not placeholders; each says exactly what to check and the fallback.
- **Type consistency:** `CalendarNavigatorViewModel`, `CalendarDayCellViewModel`, `CalendarMarkerProvider`, `CalendarMonthMarkers`, `LoadAsync(year,month,selected,ct)`, `JumpAsync(ct)`, `SelectedDateChanged`, `CompletedColorHex`/`NoteColorHex`, `SelectDateCommand`/`PrevMonthCommand`/`NextMonthCommand` — used consistently across VM, control XAML, and integration. `INoteService.ListNoteDaysInMonthAsync` and `ICompletionService.GetHeatmapAsync` signatures match Piano 1 / existing code.
