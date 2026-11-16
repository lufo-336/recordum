using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitBuilder;
using Recordum.UI.Views.HabitEditor;
using Recordum.UI.Views.HabitEditor.Sections;
using Xunit;
using static Recordum.UI.Tests.HabitEditor.EditorPaneTests;

namespace Recordum.UI.Tests.HabitEditor;

/// <summary>Onda 2b Task 6: Configura nei cinque blocchi di spec §6.4, le righe col riassunto che si aprono sul posto, le parti
/// "Avanzate" nel loro blocco, la riga della Home tinta e la Configura del gruppo. DB privato di EditorPaneTests.</summary>
public class ConfiguraBlocksTests(ITestOutputHelper output)
{
    /// <summary>L'inventario (guardia dell'anti-obiettivo "nessun campo sparisce"): ogni <c>x:Name</c> delle dodici view di
    /// sezione e della testata di Configura, preso PRIMA del refactor (6be01df8). Nessun nome e' cambiato; RollingDaysBox,
    /// ToleranceBox e RestBox sono passati nelle view *Advanced. Fuori dall'elenco, di proposito, SectionsHost: era
    /// l'ItemsControl che ospitava gli Expander, non un campo (lo sostituiscono i blocchi). ToleranceBox e' uscita
    /// dall'elenco il 2026-11-16 per scelta dell'autore (GIO-21: tolleranza e riposo non hanno effetto, restano nascosti
    /// finche' non funzionano); RestBox resta nell'albero, nascosta.</summary>
    private static readonly string[] FieldsBefore =
    [
        // testata
        "NameBox", "DescriptionBox", "TypeLabel", "ColorPicker", "IconPickerControl", "SectionsScroll", "SectionsStack", "StatusText",
        "AiBox",                                                            // AiSharingSectionView
        "GoalNameBox", "TargetBox", "PeriodBox", "RollingDaysBox",          // GoalSectionView
        "KeyFigureBox", "StripBox",                                         // HomeRowSectionView
        "MetricsList", "MetricNameBox", "MetricTypeBox", "MetricUnitBox", "AddMetricButton",   // MetricsSectionView
        "NoteColorPicker",                                                  // NotesSectionView
        "RepeatableBox", "GestureBox",                                      // RegistrationSectionView
        "ResetTimeBox", "ResetWarning",                                     // ResetTimeSectionView
        "ScaleBox", "LabelsEditor",                           // ScalesSectionView (solo uno stato)
        "ScheduleTypeBox", "WeeklyTargetBox", "EveryXDaysBox",              // SchedulingSectionView
        "StreakModeBox", "StreakRecoveryBox",                               // StreakSectionView
        "TagsList",                                                         // TagsSectionView
        "IntegratedBox", "ModeBox", "TargetBox", "SegmentsList", "RestBox", // TimerSectionView (TargetBox c'e' due volte)
    ];

    /// <summary>I controlli che prima non avevano nome (la guardia sopra non li vedeva): nominati in questo task.</summary>
    private static readonly string[] NamedNow = ["DaysList", "AddSegmentButton", "SegmentNameBox", "SegmentRestBox", "AutoPromptBox"];

    private static readonly string[] ScalesOnly = ["ScaleBox", "LabelsEditor"];

    private static readonly string[] BlocksWithBoxes = ["RecordBlock", "WhenBlock", "AdvancedRows"];

    private static async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> f)
    {
        using var scope = Host.Provider.CreateScope();
        return await f(scope.ServiceProvider);
    }

    private static Task<Guid> AddMetricAsync(Guid habitId) =>
        InScope(sp => sp.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
        {
            Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "M", Type = MetricType.Integer, SortOrder = 0,
        }).AsTask());

    private static async Task<(Opened O, EditorPane Pane, HabitEditorViewModel Editor)> OpenTypeAsync(HabitType type, Func<Guid, Task>? seed = null)
    {
        var id = await CreateAsync($"Blk-{Guid.NewGuid():N}", NodeType.Habit, type);
        if (seed is not null) await seed(id);
        var o = await OpenAsync(id);
        o.Window.Width = 1400;
        o.Window.Height = 1100;
        var pane = await OpenConfiguraAsync(o);
        PrivateDbHost.Pump();
        return (o, pane, o.Vm.Workspace.ActiveSubjectTab!.Screen.Editor);
    }

    private static List<ConfigRow> Rows(Control pane) => pane.GetVisualDescendants().OfType<ConfigRow>().ToList();

    private static ConfigRow Row(Control pane, SectionViewModelBase section, SectionPart part = SectionPart.Main) =>
        Rows(pane).Single(r => ReferenceEquals(r.Section, section) && r.Part == part);

    private static List<string?> BlockTitles(Control pane) => pane.GetVisualDescendants().OfType<TextBlock>()
        .Where(t => t.Classes.Contains("block") && t.IsEffectivelyVisible).Select(t => t.Text).ToList();

    private static void Click(Opened o, Control c)
    {
        c.BringIntoView();
        PrivateDbHost.Pump();
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), o.Window)!.Value;
        o.Window.MouseDown(p, MouseButton.Left);
        o.Window.MouseUp(p, MouseButton.Left);
        PrivateDbHost.Pump();
    }

    // Il PNG si prende a layout fermo: subito dopo un'apertura il frame mostrava ancora la misura di prima.
    private static async Task<string> CaptureAsync(Window window, string name)
    {
        PrivateDbHost.Pump();
        await Task.Delay(100);
        PrivateDbHost.Pump();
        return ScreenshotProbeTests.Capture(window, name);
    }

    private static void OpenEverything(EditorPane pane)
    {
        Named<ToggleButton>(pane, "AdvancedToggle").IsChecked = true;
        PrivateDbHost.Pump();
        foreach (var row in Rows(pane).Where(r => r.IsVisible)) row.IsOpen = true;
        PrivateDbHost.Pump();
    }

    [AvaloniaFact]
    public async Task A_closed_column_leaves_no_row_hooked_to_the_editor()
    {
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Action);
        try
        {
            var row = Row(pane, editor.Scheduling);
            o.Vm.Workspace.ActiveSubjectTab!.Screen.IsConfiguraOpen = false;
            PrivateDbHost.Pump();
            pane.IsAttachedToVisualTree().Should().BeFalse("la colonna e' chiusa");
            editor.Scheduling.IsExpanded = true;   // l'editor vive piu' della colonna
            row.IsOpen.Should().BeFalse("la riga staccata non ascolta piu' la sezione: niente righe appese al ViewModel");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_closed_column_leaves_the_pane_unhooked_from_the_editor()
    {
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Action);
        try
        {
            var label = Named<TextBlock>(pane, "TypeLabel");
            var before = label.Text;
            o.Vm.Workspace.ActiveSubjectTab!.Screen.IsConfiguraOpen = false;
            PrivateDbHost.Pump();
            pane.IsAttachedToVisualTree().Should().BeFalse();
            editor.SelectedHabitType = HabitType.Counter;   // l'editor vive piu' della colonna
            PrivateDbHost.Pump();
            label.Text.Should().Be(before, "il pane staccato non ascolta piu' l'editor: nessun pane appeso al ViewModel");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Five_blocks_in_order()
    {
        using var _ = TestCulture.Pin();
        var (o, pane, _) = await OpenTypeAsync(HabitType.Counter);
        try
        {
            BlockTitles(pane).Should().Equal("Chi è", "Cosa registri", "Quando conta", "Come appare in Home", "Avanzate");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Rows_show_title_and_summary()
    {
        using var _ = TestCulture.Pin();
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Action);
        try
        {
            var row = Row(pane, editor.Scheduling);
            Named<TextBlock>(row, "TitleText").Text.Should().Be("Programma");
            var summary = Named<TextBlock>(row, "SummaryText");
            summary.Text.Should().Be("Ogni giorno");
            summary.IsEffectivelyVisible.Should().BeTrue();
            Named<Button>(row, "Header").Bounds.Height.Should().BeApproximately(40, 0.5);
            Rows(pane).Should().OnlyContain(r => !r.IsOpen, "ogni riga parte chiusa");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_row_opens_in_place()
    {
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Action);
        try
        {
            var row = Row(pane, editor.Scheduling);
            var header = Named<Button>(row, "Header");
            Click(o, header);

            row.IsOpen.Should().BeTrue();
            editor.Scheduling.IsExpanded.Should().BeTrue("lo stato aperto e' quello della sezione");
            header.Should().BeOfType<ToggleButton>().Which.IsChecked.Should().Be(true, "lo stato aperto arriva alle tecnologie assistive");
            var host = Named<Border>(row, "ContentHost");
            var panel = (Panel)header.Parent!;
            panel.Children.IndexOf(host).Should().Be(panel.Children.IndexOf(header) + 1, "il contenuto sta subito sotto la sua riga");
            host.IsEffectivelyVisible.Should().BeTrue();
            var view = pane.GetVisualDescendants().OfType<SchedulingSectionView>().Single();
            view.GetVisualAncestors().Should().Contain(host);
            var top = host.TranslatePoint(default, pane)!.Value.Y;
            top.Should().BeApproximately(header.TranslatePoint(new Point(0, header.Bounds.Height), pane)!.Value.Y, 1);
            Rows(pane).Where(r => r != row).Should().OnlyContain(r => !r.IsOpen, "nessun'altra riga si apre");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Content_is_built_on_first_open()
    {
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Action);
        try
        {
            pane.GetVisualDescendants().OfType<SchedulingSectionView>().Should().BeEmpty("prima del clic non c'e' niente");
            pane.GetVisualDescendants().OfType<StreakSectionView>().Should().BeEmpty();
            var header = Named<Button>(Row(pane, editor.Scheduling), "Header");
            Click(o, header);
            var built = pane.GetVisualDescendants().OfType<SchedulingSectionView>().Single();
            Click(o, header);
            built.IsEffectivelyVisible.Should().BeFalse("chiusa");
            ((ToggleButton)header).IsChecked.Should().Be(false);
            pane.GetVisualDescendants().OfType<SchedulingSectionView>().Should().ContainSingle().Which.Should().BeSameAs(built, "tenuto fino alla chiusura della colonna");
            Click(o, header);
            built.IsEffectivelyVisible.Should().BeTrue();
            pane.GetVisualDescendants().OfType<StreakSectionView>().Should().BeEmpty("le altre righe restano da costruire");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Advanced_is_closed_and_holds_the_advanced_parts()
    {
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Counter);
        try
        {
            var block = Named<StackPanel>(pane, "AdvancedRows");
            block.IsEffectivelyVisible.Should().BeFalse("Avanzate e' chiuso di default");
            Click(o, Named<ToggleButton>(pane, "AdvancedToggle"));
            block.IsEffectivelyVisible.Should().BeTrue();

            var rows = new[] { Row(pane, editor.Goal, SectionPart.Advanced),
                Row(pane, editor.Timer, SectionPart.Advanced), Row(pane, editor.AiSharing) };
            rows.Should().OnlyContain(r => r.IsEffectivelyVisible && r.GetVisualAncestors().Contains(block));
            foreach (var r in rows) Click(o, Named<Button>(r, "Header"));
            editor.Streak.IsExpanded.Should().BeFalse("la parte avanzata ha uno stato suo, non apre la riga principale");
            foreach (var t in new[] { typeof(GoalAdvancedView), typeof(TimerAdvancedView), typeof(AiSharingSectionView) })
                block.GetVisualDescendants().Should().ContainSingle(v => v.GetType() == t, t.Name);
            // GIO-21: tolleranza e timer di riposo si salvano ma niente li legge: nascosti finche' non funzionano.
            block.GetVisualDescendants().Should().NotContain(v => v is StreakAdvancedView, "la serie non ha piu' una parte avanzata");
            Named<TextBox>(pane, "RestBox").IsEffectivelyVisible.Should().BeFalse("il timer di riposo e' nascosto");
            Named<CheckBox>(pane, "AutoPromptBox").IsEffectivelyVisible.Should().BeTrue();

            Row(pane, editor.Streak).IsOpen = true;
            PrivateDbHost.Pump();
            var streak = pane.GetVisualDescendants().OfType<StreakSectionView>().Single();
            streak.GetVisualDescendants().OfType<TextBox>().Should().NotContain(t => t.Name == "ToleranceBox", "la tolleranza e' uscita");
            streak.GetVisualDescendants().OfType<ToggleButton>().Should().BeEmpty("niente piu' interruttore 'Avanzate' dentro la sezione");
            pane.GetVisualDescendants().OfType<TextBox>().Where(t => t.Name == "ToleranceBox").Should().BeEmpty("GIO-21");
            // Spec 6.4 blocco 5: i segmenti del timer sono una cosa avanzata.
            Row(pane, editor.Timer).IsOpen = true;
            PrivateDbHost.Pump();
            pane.GetVisualDescendants().OfType<TimerSectionView>().Single().GetVisualDescendants().OfType<Control>()
                .Select(c => c.Name).Should().NotContain(["SegmentsList", "AddSegmentButton"]);
            pane.GetVisualDescendants().OfType<TimerAdvancedView>().Single().GetVisualDescendants().OfType<Control>()
                .Select(c => c.Name).Should().Contain(["SegmentsList", "AddSegmentButton"]);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task An_advanced_field_still_saves()
    {
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Counter);
        try
        {
            Click(o, Named<ToggleButton>(pane, "AdvancedToggle"));
            Click(o, Named<Button>(Row(pane, editor.Timer, SectionPart.Advanced), "Header"));
            Click(o, Named<CheckBox>(pane, "AutoPromptBox"));
            PrivateDbHost.Pump();
            await UntilAsync(() => InScope(sp => sp.GetRequiredService<IHabitConfigService>().GetAsync(o.Id).AsTask())
                .GetAwaiter().GetResult() is { TimerAutoPrompt: true });
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Every_field_of_today_is_reachable()
    {
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Counter, async id => await AddMetricAsync(id));
        try
        {
            await editor.Timer.AddSegmentCommand.ExecuteAsync(null);
            OpenEverything(pane);
            var names = pane.GetVisualDescendants().OfType<Control>().Select(c => c.Name).Where(n => n is not null).ToList();
            var missing = FieldsBefore.Except(ScalesOnly).Concat(NamedNow).Where(n => !names.Contains(n)).ToList();
            missing.Should().BeEmpty("un contatore con una metrica e un segmento mostra ogni campo di oggi tranne le scale");
            names.Count(n => n == "TargetBox").Should().Be(2, "obiettivo e cronometro");
        }
        finally { o.Window.Close(); }

        var (o2, pane2, _) = await OpenTypeAsync(HabitType.State, async id => await AddMetricAsync(id));
        try
        {
            OpenEverything(pane2);
            var names = pane2.GetVisualDescendants().OfType<Control>().Select(c => c.Name).ToList();
            ScalesOnly.Where(n => !names.Contains(n)).Should().BeEmpty("uno stato mostra le scale");
        }
        finally { o2.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Reset_warning_shows_on_the_closed_row()
    {
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Action);
        try
        {
            var row = Row(pane, editor.ResetTime);
            row.IsOpen.Should().BeFalse();
            var icon = Named<Avalonia.Controls.Shapes.Path>(row, "WarningIcon");
            icon.IsEffectivelyVisible.Should().BeTrue("l'avviso si vede anche a riga chiusa");
            ToolTip.GetTip(icon).Should().Be(Strings.Editor_ResetTime_Warning);
            icon.Height.Should().Be(13);
            Rows(pane).Where(r => r != row && r.IsEffectivelyVisible).Select(r => Named<Avalonia.Controls.Shapes.Path>(r, "WarningIcon"))
                .Should().OnlyContain(i => !i.IsVisible, "solo l'inizio della giornata ha l'avviso");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Home_preview_is_a_tinted_row()
    {
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Action);
        try
        {
            var preview = Named<StackPanel>(pane, "HomeBlock").GetVisualDescendants().OfType<RowPreview>().Single();
            preview.IsEffectivelyVisible.Should().BeTrue();
            preview.Model.Should().NotBeNull().And.BeSameAs(editor.HomeRow.Preview);
            Named<TextBlock>(preview, "NameText").Text.Should().Be(editor.Name);
            // Revisione: un'abitudine vera non mostra numeri inventati; resta la forma della striscia, a celle vuote.
            Named<TextBlock>(preview, "KeyFigureText").IsVisible.Should().BeFalse("nessun valore accanto al nome");
            var strip = Named<HeatmapStrip>(preview, "HomeStrip");
            strip.Fractions.Should().HaveCount(14).And.OnlyContain(f => f == 0);   // GIO-3: ogni tipo ha la striscia intera
            var tint = Named<Border>(preview, "Tint");
            tint.IsEffectivelyVisible.Should().BeTrue();
            tint.Background.Should().BeAssignableTo<ISolidColorBrush>().Which.Color.Should().Be(Color.Parse("#E11D48"));
            o.Window.TryFindResource("Opacity.RowTint", o.Window.ActualThemeVariant, out var opacity).Should().BeTrue();
            tint.Opacity.Should().Be((double)opacity!);
            tint.Bounds.Width.Should().BeApproximately(Named<Border>(preview, "Card").Bounds.Width - 2, 1, "la tinta copre la riga");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Every_checkbox_has_a_help_line()
    {
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Counter, async id => await AddMetricAsync(id));
        try
        {
            await editor.Timer.AddSegmentCommand.ExecuteAsync(null);
            OpenEverything(pane);
            var boxes = BlocksWithBoxes.SelectMany(b => Named<StackPanel>(pane, b).GetVisualDescendants())
                .OfType<ToggleButton>().Where(t => t is CheckBox or ToggleSwitch && t.IsEffectivelyVisible).ToList();
            boxes.Select(b => b.Name).Should().Contain(["RepeatableBox", "IntegratedBox", "SegmentRestBox", "AutoPromptBox"]);
            foreach (var box in boxes)
            {
                var parent = box.Parent.Should().BeAssignableTo<Panel>($"{box.Name} sta in un pannello").Subject;
                var next = parent.Children.ElementAtOrDefault(parent.Children.IndexOf(box) + 1);
                next.Should().BeOfType<TextBlock>($"{box.Name} ha una riga che spiega subito sotto");
                var help = (TextBlock)next!;
                help.Classes.Should().Contain("help");
                help.Text.Should().NotBeNullOrWhiteSpace();
                help.FontSize.Should().Be(12);
                help.IsEffectivelyVisible.Should().BeTrue();
            }
        }
        finally { o.Window.Close(); }
    }

    private static async Task<(Guid Group, Guid Child, Guid Sub, Guid Grandchild)> CreateGroupAsync()
    {
        var group = await CreateAsync($"Grp-{Guid.NewGuid():N}", NodeType.Group);
        using var scope = Host.Provider.CreateScope();
        var nodes = scope.ServiceProvider.GetRequiredService<IHabitNodeService>();
        async Task<Guid> Make(string name, NodeType nt, Guid parent) => await nodes.CreateAsync(new HabitNode
        {
            NodeType = nt, HabitType = nt == NodeType.Group ? null : HabitType.Action, Name = name, Color = "#2F8FB0", ParentId = parent,
        });
        var child = await Make($"Figlia-{Guid.NewGuid():N}"[..14], NodeType.Habit, group);
        var sub = await Make($"Sotto-{Guid.NewGuid():N}"[..13], NodeType.Group, group);
        var grand = await Make($"Nipote-{Guid.NewGuid():N}"[..14], NodeType.Habit, sub);
        return (group, child, sub, grand);
    }

    [AvaloniaFact]
    public async Task Group_configura_has_its_blocks()
    {
        using var _ = TestCulture.Pin();
        var (groupId, child, sub, grand) = await CreateGroupAsync();
        var o = await OpenAsync(groupId);
        try
        {
            o.Window.Height = 1100;
            var pane = await OpenConfiguraAsync(o);
            var editor = o.Vm.Workspace.ActiveSubjectTab!.Screen.Editor;
            var content = pane.GetVisualDescendants().OfType<GroupContentView>().Single();
            await UntilAsync(() => content.GetVisualDescendants().OfType<Button>().Count(b => b.IsEffectivelyVisible) == 2);

            BlockTitles(pane).Should().Equal("Chi è", "Per le figlie", "Contenuto", "Avanzate");
            Named<TextBlock>(pane, "TypeLabel").Text.Should().Be(Strings.Editor_NodeType_Group);
            foreach (var n in new[] { "NameBox", "DescriptionBox", "ColorPicker", "IconPickerControl" })
                Named<Control>(pane, n).IsEffectivelyVisible.Should().BeTrue(n);

            var forChildren = Named<StackPanel>(pane, "ForChildrenBlock");
            Rows(pane).Where(r => r.IsEffectivelyVisible && r.GetVisualAncestors().Contains(forChildren)).Select(r => r.Section)
                .Should().Equal(editor.ResetTime, editor.Goal);

            var shown = content.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible)
                .Select(b => b.GetVisualDescendants().OfType<TextBlock>().First().Text).ToList();
            shown.Should().HaveCount(2).And.Contain(s => s!.StartsWith("Figlia-")).And.Contain(s => s!.StartsWith("Sotto-"),
                "figlie dirette e sottogruppi; la nipote no");
            content.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Text == "Riordina e sposta le abitudini dalla Home." && t.IsEffectivelyVisible);

            var save = Named<Button>(pane, "SaveTemplateRow");
            save.IsEffectivelyVisible.Should().BeTrue();
            save.IsEnabled.Should().BeFalse("spenta finche' non c'e'");
            // Il PNG la mostrava incorniciata come un pulsante spento: e' una riga come le altre, senza bordo.
            save.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First().BorderBrush
                .Should().BeAssignableTo<ISolidColorBrush>().Which.Color.A.Should().Be(0);
            save.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Should().Contain(["Salva come modello", Strings.Editor_Summary_ComingSoon]);

            // Le sezioni che un gruppo vedeva e che non stanno nei suoi blocchi: in Avanzate, mai tolte.
            Named<ToggleButton>(pane, "AdvancedToggle").IsChecked = true;
            PrivateDbHost.Pump();
            var advanced = Named<StackPanel>(pane, "AdvancedRows");
            Rows(pane).Where(r => r.IsEffectivelyVisible && r.GetVisualAncestors().Contains(advanced)).Select(r => (r.Section, r.Part))
                .Should().Equal((editor.Goal, SectionPart.Advanced), (editor.AiSharing, SectionPart.Main), (editor.Tags, SectionPart.Main));
            Rows(pane).Where(r => r.IsEffectivelyVisible).Select(r => r.Section).Should().NotContain([editor.Notes, editor.Scheduling, editor.HomeRow]);

            var childButton = content.GetVisualDescendants().OfType<Button>()
                .Single(b => b.IsEffectivelyVisible && b.GetVisualDescendants().OfType<TextBlock>().First().Text!.StartsWith("Figlia-", StringComparison.Ordinal));
            Click(o, childButton);
            await UntilAsync(() => o.Vm.Workspace.ActiveSubjectTab?.SubjectId == child);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Builder_preview_still_shows_the_row()
    {
        using var scope = Host.Provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<Recordum.Presentation.ViewModels.HabitBuilder.HabitBuilderViewModel>();
        await vm.InitializeAsync();
        var w = new Window { Width = 1100, Height = 900, Content = new HabitBuilderView { DataContext = vm } };
        w.Show();
        PrivateDbHost.Pump();
        try
        {
            var pane = w.GetVisualDescendants().OfType<BuilderPreviewPane>().Single();
            var row = pane.GetVisualDescendants().OfType<RowPreview>().Single();
            row.Model.Should().BeSameAs(vm.Preview!.HomeRow);
            Named<Border>(row, "Tint").IsEffectivelyVisible.Should().BeTrue("la riga del builder ora ha la tinta");
            Named<HeatmapStrip>(pane, "HomeStrip").IsEffectivelyVisible.Should().BeTrue();
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Configura_blocks_render_in_both_themes(string variant)
    {
        using var _ = TestCulture.Pin();
        var theme = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var v = variant.ToLowerInvariant();
        var (o, pane, editor) = await OpenTypeAsync(HabitType.Counter, async id => await AddMetricAsync(id));
        try
        {
            o.Window.RequestedThemeVariant = theme;
            PrivateDbHost.Pump();
            output.WriteLine(await CaptureAsync(o.Window, $"configura-blocks-closed-{v}"));
            Rows(pane).Should().OnlyContain(r => !r.IsOpen);
            BlockTitles(pane).Should().HaveCount(5);

            Row(pane, editor.Scheduling).IsOpen = true;
            PrivateDbHost.Pump();
            output.WriteLine(await CaptureAsync(o.Window, $"configura-blocks-row-open-{v}"));
            pane.GetVisualDescendants().OfType<SchedulingSectionView>().Single().Bounds.Height.Should().BeGreaterThan(40);
            // Il PNG mostrava la casella del programma oltre il bordo della colonna: il contenuto aperto sta dentro la sua riga.
            var schedRow = Named<Button>(Row(pane, editor.Scheduling), "Header");
            var rowRight = schedRow.TranslatePoint(new Point(schedRow.Bounds.Width, 0), pane)!.Value.X;
            var box = Named<ComboBox>(pane, "ScheduleTypeBox");
            box.TranslatePoint(new Point(box.Bounds.Width, 0), pane)!.Value.X.Should().BeLessThanOrEqualTo(rowRight + 0.5);

            Row(pane, editor.Scheduling).IsOpen = false;
            Named<ToggleButton>(pane, "AdvancedToggle").IsChecked = true;
            foreach (var r in Rows(pane).Where(r => r.Part == SectionPart.Advanced && r.IsVisible)) r.IsOpen = true;
            PrivateDbHost.Pump();
            Named<ScrollViewer>(pane, "SectionsScroll").ScrollToEnd();
            PrivateDbHost.Pump();
            output.WriteLine(await CaptureAsync(o.Window, $"configura-blocks-advanced-{v}"));
            pane.GetVisualDescendants().OfType<TimerAdvancedView>().Single().IsEffectivelyVisible.Should().BeTrue();
        }
        finally { o.Window.Close(); }

        var (groupId, _, _, _) = await CreateGroupAsync();
        var g = await OpenAsync(groupId);
        try
        {
            g.Window.Width = 1400;
            g.Window.Height = 1100;
            g.Window.RequestedThemeVariant = theme;
            var gp = await OpenConfiguraAsync(g);
            var content = gp.GetVisualDescendants().OfType<GroupContentView>().Single();
            await UntilAsync(() => content.GetVisualDescendants().OfType<Button>().Count(b => b.IsEffectivelyVisible) == 2);
            output.WriteLine(await CaptureAsync(g.Window, $"configura-group-{v}"));
            BlockTitles(gp).Should().HaveCount(4);
        }
        finally { g.Window.Close(); }
    }

    // ---- Onda 3a Task 7: Configura con l'editor delle etichette di una scala ------------------------------------

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Configura_shows_the_scale_labels_as_a_list_of_fields(string variant)
    {
        using var _ = TestCulture.Pin();
        var v = variant.ToLowerInvariant();
        var (o, pane, editor) = await OpenTypeAsync(HabitType.State, async id => await AddMetricAsync(id));
        try
        {
            o.Window.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            Row(pane, editor.Scales).IsOpen = true;
            PrivateDbHost.Pump();
            var scale = editor.Scales.AvailableScales.First(t => t.Code == "mood-5");
            Named<ComboBox>(pane, "ScaleBox").SelectedItem = scale;
            PrivateDbHost.Pump();
            Named<ScaleLabelsEditor>(pane, "LabelsEditor").BringIntoView();
            output.WriteLine(await CaptureAsync(o.Window, $"configura-scale-labels-{v}"));

            var labels = Named<ScaleLabelsEditor>(pane, "LabelsEditor");
            labels.IsEffectivelyVisible.Should().BeTrue();
            var boxes = labels.GetVisualDescendants().OfType<TextBox>().ToList();
            boxes.Should().HaveCount(5, "un campo per gradino");
            boxes.Should().OnlyContain(b => b.IsEffectivelyVisible && b.Bounds.Height > 20 && b.Bounds.Width > 60, "ogni campo e' disegnato");
            boxes.Select(b => b.Text).Should().Equal("Molto basso", "Basso", "Neutro", "Buono", "Molto buono");
        }
        finally { o.Window.Close(); }
    }
}
