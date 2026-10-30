using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitEditor;
using Recordum.UI.Views.HabitEditor.Sections;
using Xunit;
using static Recordum.UI.Tests.HabitEditor.EditorPaneTests;

namespace Recordum.UI.Tests.HabitEditor;

/// <summary>F2b Task 8: le sei sezioni del contenuto di Configura (Metriche, Scale, Cronometro, Tag, Note, Condivisione AI)
/// su DB privato, e la prova A8 con tutte le sezioni aperte.</summary>
public class ContentSectionsTests
{
    private static async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> f)
    {
        using var scope = Host.Provider.CreateScope();
        return await f(scope.ServiceProvider);
    }

    private static Task<HabitConfig?> ConfigAsync(Guid id) =>
        InScope(sp => sp.GetRequiredService<IHabitConfigService>().GetAsync(id).AsTask());

    private static Task<IReadOnlyList<MetricDefinition>> MetricsAsync(Guid id) =>
        InScope(sp => sp.GetRequiredService<IMetricDefinitionService>().ListByHabitAsync(id).AsTask());

    private static Task<Guid> AddMetricAsync(Guid habitId, MetricType type = MetricType.Integer) =>
        InScope(sp => sp.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
        {
            Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "M", Type = type, SortOrder = 0,
        }).AsTask());

    private static async Task<(Opened O, EditorPane Pane, TSection Section, TView View)> OpenSectionAsync<TSection, TView>(
        Func<HabitEditorViewModel, TSection> pick, string name, HabitType type, Func<Guid, Task>? seed = null)
        where TSection : SectionViewModelBase where TView : Control
    {
        var id = await CreateAsync($"{name}-{Guid.NewGuid():N}", NodeType.Habit, type);
        if (seed is not null) await seed(id);
        var o = await OpenAsync(id);
        var pane = await OpenConfiguraAsync(o);
        var section = pick(o.Vm.Workspace.ActiveSubjectTab!.Screen.Editor);
        section.IsVisible.Should().BeTrue("la sezione si applica a questo tipo");
        Named<ToggleButton>(pane, "AdvancedToggle").IsChecked = true;   // la condivisione AI sta nel blocco Avanzate, chiuso
        section.IsExpanded = true;
        PrivateDbHost.Pump();
        return (o, pane, section, pane.GetVisualDescendants().OfType<TView>().Single());
    }

    [AvaloniaFact]
    public async Task Metrics_add_and_unit_reach_the_db()
    {
        var (o, _, s, v) = await OpenSectionAsync<MetricsSectionViewModel, MetricsSectionView>(e => e.Metrics, "Met", HabitType.Counter);
        try
        {
            Named<Button>(v, "AddMetricButton").Command!.Execute(null);
            await UntilAsync(() => s.Metrics.Count == 1);
            PrivateDbHost.Pump();
            var unit = s.AvailableUnits.First(u => u.Id != s.Metrics[0].UnitOfMeasureId);
            Named<ComboBox>(v, "MetricUnitBox").SelectedItem = unit;
            await UntilAsync(() => MetricsAsync(o.Id).GetAwaiter().GetResult() is [{ } m] && m.UnitOfMeasureId == unit.Id);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Scales_choice_reaches_the_db()
    {
        var (o, _, s, v) = await OpenSectionAsync<ScalesSectionViewModel, ScalesSectionView>(e => e.Scales, "Scl", HabitType.State,
            async id => await AddMetricAsync(id, MetricType.Integer));
        try
        {
            s.AvailableScales.Should().NotBeEmpty();
            var scale = s.AvailableScales.First();
            Named<ComboBox>(v, "ScaleBox").SelectedItem = scale;
            await UntilAsync(() => MetricsAsync(o.Id).GetAwaiter().GetResult() is [{ } m] && m.ScaleTemplateId == scale.Id);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Timer_mode_reaches_the_db()
    {
        var (o, _, _, v) = await OpenSectionAsync<TimerSectionViewModel, TimerSectionView>(e => e.Timer, "Tmr", HabitType.Counter);
        try
        {
            Named<CheckBox>(v, "IntegratedBox").IsChecked = true;
            Named<ComboBox>(v, "ModeBox").SelectedItem = TimerMode.Countdown;
            await UntilAsync(() => ConfigAsync(o.Id).GetAwaiter().GetResult() is { HasIntegratedTimer: true, TimerMode: TimerMode.Countdown });
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Changing_the_timer_mode_updates_the_Cronologia_stopwatch_on_the_same_screen()
    {
        var (o, _, _, v) = await OpenSectionAsync<TimerSectionViewModel, TimerSectionView>(e => e.Timer, "TmrLive", HabitType.Counter);
        try
        {
            var timer = o.Vm.Workspace.ActiveSubjectTab!.Screen.TimerControl;
            timer.IsApplicable.Should().BeFalse("nessun cronometro integrato all'inizio");
            Named<CheckBox>(v, "IntegratedBox").IsChecked = true;
            Named<ComboBox>(v, "ModeBox").SelectedItem = TimerMode.Stopwatch;
            await UntilAsync(() => timer.IsApplicable && timer.Mode == TimerMode.Stopwatch);
            Named<ComboBox>(v, "ModeBox").SelectedItem = TimerMode.Countdown;
            await UntilAsync(() => timer.Mode == TimerMode.Countdown);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Registration_repeatable_flip_rebuilds_the_open_quick_add()
    {
        var (o, _, _, v) = await OpenSectionAsync<RegistrationSectionViewModel, RegistrationSectionView>(e => e.Registration, "Rep", HabitType.Counter,
            async id => await AddMetricAsync(id));   // senza metriche il counter e' a tocco singolo: "ripetibile" riguarda le serie
        try
        {
            var screen = o.Vm.Workspace.ActiveSubjectTab!.Screen;
            screen.QuickAdd.Should().BeOfType<CounterSeriesQuickAddViewModel>().Which.IsRepeatable.Should().BeFalse();
            Named<CheckBox>(v, "RepeatableBox").IsChecked = true;
            await UntilAsync(() => screen.QuickAdd is CounterSeriesQuickAddViewModel { IsRepeatable: true });
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_tag_can_be_added_and_removed()
    {
        Guid tagId = default;
        var (o, _, _, v) = await OpenSectionAsync<TagsSectionViewModel, TagsSectionView>(e => e.Tags, "Tag", HabitType.Action,
            async _ => tagId = await InScope(sp => sp.GetRequiredService<ITagService>()
                .CreateAsync(new Tag { Id = Guid.NewGuid(), Name = $"t-{Guid.NewGuid():N}" }).AsTask()));
        try
        {
            var chip = v.GetVisualDescendants().OfType<ToggleButton>().Single(t => t.DataContext is TagChipViewModel c && c.Id == tagId);
            chip.Command!.Execute(chip.CommandParameter);
            await UntilAsync(() => InScope(sp => sp.GetRequiredService<ITagService>().ListByHabitAsync(o.Id).AsTask())
                .GetAwaiter().GetResult().Any(t => t.Id == tagId));
            chip.IsChecked.Should().BeTrue();
            chip.Command!.Execute(chip.CommandParameter);
            await UntilAsync(() => InScope(sp => sp.GetRequiredService<ITagService>().ListByHabitAsync(o.Id).AsTask())
                .GetAwaiter().GetResult().All(t => t.Id != tagId));
            chip.IsChecked.Should().BeFalse();
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Note_marker_colour_reaches_the_db()
    {
        var (o, _, _, v) = await OpenSectionAsync<NotesSectionViewModel, NotesSectionView>(e => e.Notes, "Nts", HabitType.Action);
        try
        {
            Named<ColorSwatchPicker>(v, "NoteColorPicker").Color = "#123456";
            await UntilAsync(() => ConfigAsync(o.Id).GetAwaiter().GetResult() is { NoteMarkerColor: "#123456" });
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Ai_sharing_stays_off_until_turned_on()
    {
        var (o, _, _, v) = await OpenSectionAsync<AiSharingSectionViewModel, AiSharingSectionView>(e => e.AiSharing, "Ai", HabitType.Action);
        try
        {
            var box = Named<ComboBox>(v, "AiBox");
            box.SelectedItem.Should().Be(AiDataSharing.None);
            await Task.Delay(100);
            PrivateDbHost.Pump();
            (await ReadAsync(o.Id)).AiDataSharing.Should().Be(AiDataSharing.None, "aprire la sezione non accende niente (CLAUDE.md 2.3)");

            box.SelectedItem = AiDataSharing.LocalOnly;
            await UntilAsync(() => ReadAsync(o.Id).GetAwaiter().GetResult().AiDataSharing == AiDataSharing.LocalOnly);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Enum_pickers_show_localized_labels_and_the_AI_options_say_what_happens_to_the_data()
    {
        using var _ = TestCulture.Pin();
        var (o, _, _, v) = await OpenSectionAsync<AiSharingSectionViewModel, AiSharingSectionView>(e => e.AiSharing, "AiLbl", HabitType.Action);
        try
        {
            var box = Named<ComboBox>(v, "AiBox");
            box.SelectedItem = AiDataSharing.ApiAllowed;
            PrivateDbHost.Pump();
            var shown = box.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
            shown.Should().Contain("Può essere inviato al servizio AI").And.NotContain("ApiAllowed");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Adding_the_first_metric_turns_the_tap_quick_add_into_the_series_one_on_the_open_screen()
    {
        var (o, _, s, v) = await OpenSectionAsync<MetricsSectionViewModel, MetricsSectionView>(e => e.Metrics, "MetQa", HabitType.Counter);
        try
        {
            var screen = o.Vm.Workspace.ActiveSubjectTab!.Screen;
            screen.QuickAdd.Should().BeOfType<CounterTapQuickAddViewModel>();
            Named<Button>(v, "AddMetricButton").Command!.Execute(null);
            await UntilAsync(() => screen.QuickAdd is CounterSeriesQuickAddViewModel);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_scale_choice_on_a_state_habit_rebuilds_the_open_quick_add()
    {
        var (o, _, s, v) = await OpenSectionAsync<ScalesSectionViewModel, ScalesSectionView>(e => e.Scales, "SclQa", HabitType.State,
            async id => await AddMetricAsync(id));
        try
        {
            var screen = o.Vm.Workspace.ActiveSubjectTab!.Screen;
            var before = screen.QuickAdd;
            Named<ComboBox>(v, "ScaleBox").SelectedItem = s.AvailableScales.First();
            await UntilAsync(() => !ReferenceEquals(screen.QuickAdd, before));
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Adding_a_segment_in_session_mode_updates_the_Cronologia_timer()
    {
        var (o, _, s, v) = await OpenSectionAsync<TimerSectionViewModel, TimerSectionView>(e => e.Timer, "SegQa", HabitType.Counter);
        try
        {
            var timer = o.Vm.Workspace.ActiveSubjectTab!.Screen.TimerControl;
            await s.AddSegmentCommand.ExecuteAsync(null);   // prima: nessun segmento, la modalita' Sessione sarebbe rifiutata
            Named<CheckBox>(v, "IntegratedBox").IsChecked = true;
            Named<ComboBox>(v, "ModeBox").SelectedItem = TimerMode.Session;
            await UntilAsync(() => timer.Mode == TimerMode.Session && timer.Segments.Count == 1);
            await s.AddSegmentCommand.ExecuteAsync(null);
            await UntilAsync(() => timer.Segments.Count == 2);
            await s.RemoveSegmentCommand.ExecuteAsync(s.Segments[0]);
            await UntilAsync(() => timer.Segments.Count == 1);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task With_every_section_open_at_320_px_nothing_extends_past_the_column_and_the_status_stays_visible()
    {
        foreach (var type in new[] { HabitType.Counter, HabitType.State })
        {
            var id = await CreateAsync($"A8-{Guid.NewGuid():N}", NodeType.Habit, type);
            await AddMetricAsync(id, MetricType.Seconds);
            await InScope(sp => sp.GetRequiredService<ITagService>()
                .CreateAsync(new Tag { Id = Guid.NewGuid(), Name = $"{Guid.NewGuid():N}-tag-con-un-nome-molto-lungo-" + new string('x', 120) }).AsTask());
            var o = await OpenAsync(id);
            try
            {
                o.Window.Width = 600;   // due colonne da 320 non entrano: Configura resta a 320 (decisione 6, A8)
                var pane = await OpenConfiguraAsync(o);
                var editor = o.Vm.Workspace.ActiveSubjectTab!.Screen.Editor;
                var sections = editor.Sections.Where(sec => sec.IsVisible).ToList();
                sections.Should().HaveCountGreaterThan(8);
                foreach (var sec in sections) sec.IsExpanded = true;
                Named<ToggleButton>(pane, "AdvancedToggle").IsChecked = true;   // e le parti avanzate (onda 2b)
                foreach (var row in pane.GetLogicalDescendants().OfType<ConfigRow>().Where(r => r.Part == SectionPart.Advanced)) row.IsOpen = true;
                if (type == HabitType.Counter) await editor.Timer.AddSegmentCommand.ExecuteAsync(null);
                PrivateDbHost.Pump();
                o.Window.UpdateLayout();
                PrivateDbHost.Pump();

                pane.Bounds.Width.Should().BeApproximately(320, 1, "la colonna Configura e' al suo minimo");
                var edge = pane.Bounds.Width + 0.5;
                var offenders = pane.GetVisualDescendants().OfType<Control>()
                    .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0)
                    .Where(c => !c.GetVisualAncestors().Any(a => a is TextBox) && c is not ScrollViewer)
                    .Where(c => c.TranslatePoint(new Point(c.Bounds.Width, 0), pane)!.Value.X > edge)
                    .Select(c => $"{c.GetType().Name}#{c.Name} right={c.TranslatePoint(new Point(c.Bounds.Width, 0), pane)!.Value.X:F0}")
                    .ToList();
                offenders.Should().BeEmpty($"A8: con {sections.Count} sezioni aperte ({type}) nessun controllo esce dal bordo destro");

                // A12: con le sezioni vere aperte, lo stato resta fuori dallo scorrimento e in vista.
                var scroll = Named<ScrollViewer>(pane, "SectionsScroll");
                scroll.Extent.Height.Should().BeGreaterThan(scroll.Viewport.Height, "le sezioni aperte scorrono");
                scroll.ScrollToEnd();
                PrivateDbHost.Pump();
                var status = Named<TextBlock>(pane, "StatusText");
                status.GetVisualAncestors().Should().NotContain(scroll);
                status.IsEffectivelyVisible.Should().BeTrue();
                var bottom = status.TranslatePoint(new Point(0, status.Bounds.Height), pane)!.Value.Y;
                bottom.Should().BeLessThanOrEqualTo(pane.Bounds.Height + 0.5).And.BeGreaterThan(pane.Bounds.Height - 40);
            }
            finally { o.Window.Close(); }
        }
    }
}
