using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.Presentation.ViewModels.HabitScreen.Today;
using Recordum.Presentation.ViewModels.Workspace;
using Recordum.UI.Services;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Tests.HabitScreen;
using Recordum.UI.Views;
using Recordum.UI.Views.HabitBuilder;
using Recordum.UI.Views.HabitEditor.Sections;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.QuickAdd;
using Xunit;
using static Recordum.UI.Tests.HabitEditor.EditorPaneTests;

namespace Recordum.UI.Tests.HabitEditor;

/// <summary>F2b task 9: creare e configurare end to end dalla finestra principale, sul DB SQLCipher vero (privato).</summary>
public class CreateAndConfigureIntegrationTests
{
    private static async Task<(MainWindow Window, MainWindowViewModel Vm)> OpenMainAsync()
    {
        using (var scope = Host.Provider.CreateScope())
        {
            await HabitScreenColumnsTests.ResetColumnStateAsync(scope);
            await scope.ServiceProvider.GetRequiredService<IAppSettingService>()
                .SetAsync("ui.workspace", new WorkspaceState().ToJson());
        }
        return await ShellFixture.OpenMainWindowAsync(Host.Provider.GetRequiredService<MainWindowViewModel>());
    }

    private static void Click(Window w, Control c)
    {
        c.BringIntoView();
        PrivateDbHost.Pump();
        var p = c.TranslatePoint(new Avalonia.Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, Avalonia.Input.MouseButton.Left);
        w.MouseUp(p, Avalonia.Input.MouseButton.Left);
        PrivateDbHost.Pump();
    }

    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaFact]
    public async Task Picking_a_counter_template_then_changing_its_goal_in_Configura_reaches_the_Oggi_strip_and_the_history()
    {
        using var culture = TestCulture.Pin();
        var (window, vm) = await OpenMainAsync();
        try
        {
            var before = vm.Home.Rows.Select(r => r.HabitNodeId).ToHashSet();
            AvaloniaHabitTemplatePickerLauncher launcher = (AvaloniaHabitTemplatePickerLauncher)Host.Provider.GetRequiredService<IHabitTemplatePickerLauncher>();
            Window? picker = null;
            launcher.WindowCreatedForTests += w => picker = w;
            _ = vm.Home.OpenNewHabitCommand.ExecuteAsync(null);
            await UntilAsync(() => picker is { IsVisible: true });
            PrivateDbHost.Pump();
            var option = picker!.GetVisualDescendants().OfType<Button>()
                .First(b => b.Name == "OptionButton" && b.DataContext is PickerOption { Name: "Water intake" });
            option.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await UntilAsync(() => vm.Home.SelectedRow is not null && !before.Contains(vm.Home.SelectedRow.HabitNodeId));
            var id = vm.Home.SelectedRow!.HabitNodeId;
            await UntilAsync(() => window.GetVisualDescendants().OfType<HabitScreenView>().Any());
            var screen = vm.Workspace.ActiveSubjectTab!.Screen;
            await UntilAsync(() => screen.TodayStrip.Content is CounterTodayContent { HasGoal: true });

            // Un litro su un obiettivo di due: meta' strada.
            using (var s = Host.Provider.CreateScope())
            {
                var metric = (await s.ServiceProvider.GetRequiredService<IMetricDefinitionService>().ListByHabitAsync(id)).Single();
                await s.ServiceProvider.GetRequiredService<ICompletionService>().RecordAsync(
                    new Completion { HabitNodeId = id, CompletedAt = Host.Clock.UtcNow.UtcDateTime, CompletedAtMs = Host.Clock.UtcNowMs },
                    [new CompletionValue { MetricDefinitionId = metric.Id, ValueDecimal = 1m }]);
            }
            await UntilAsync(() => screen.TodayStrip.Content is CounterTodayContent { GoalFraction: 0.5 });
            var selected = screen.Calendar.SelectedDate;

            // Configura: l'obiettivo passa a quattro litri; la fascia Oggi lo riflette, la schermata resta dov'era.
            var pane = await OpenConfiguraAsync(new Opened(window, vm, id));
            screen.Editor.Goal.IsExpanded = true;
            PrivateDbHost.Pump();
            var box = pane.GetVisualDescendants().OfType<GoalSectionView>().Single().GetVisualDescendants()
                .OfType<TextBox>().Single(t => t.Name == "TargetBox");
            box.Focus();
            PrivateDbHost.Pump();
            box.SelectAll();
            window.KeyTextInput("4");
            Find<TextBox>(pane, "NameBox").Focus();
            PrivateDbHost.Pump();

            using var scope = Host.Provider.CreateScope();
            await UntilAsync(() => scope.ServiceProvider.GetRequiredService<IGoalService>()
                .GetActiveAsync(id).AsTask().GetAwaiter().GetResult() is { TargetValue: 4m });
            await UntilAsync(() => screen.TodayStrip.Content is CounterTodayContent { GoalFraction: 0.25 });
            screen.Records.Records.Rows.Should().HaveCount(1, "la Cronologia riflette ancora la registrazione");
            screen.Calendar.SelectedDate.Should().Be(selected, "una modifica di Configura non sposta il giorno");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task The_builder_creates_a_counter_with_a_custom_unit_and_the_quick_add_shows_it()
    {
        using var culture = TestCulture.Pin();
        var (window, vm) = await OpenMainAsync();
        var unit = $"Glass{Guid.NewGuid():N}"[..12];
        var name = $"Water-{Guid.NewGuid():N}";
        var pickers = (AvaloniaHabitTemplatePickerLauncher)Host.Provider.GetRequiredService<IHabitTemplatePickerLauncher>();
        var builders = (AvaloniaHabitBuilderLauncher)Host.Provider.GetRequiredService<IHabitBuilderLauncher>();
        Window? picker = null, builder = null;
        pickers.WindowCreatedForTests += w => picker = w;
        builders.WindowCreatedForTests += w => builder = w;
        var flow = vm.Home.OpenNewHabitCommand.ExecuteAsync(null);   // Home -> selettore -> builder -> riga selezionata
        try
        {
            await UntilAsync(() => picker is { IsVisible: true });
            PrivateDbHost.Pump();
            picker!.GetVisualDescendants().OfType<Button>()
                .First(b => b.Name == "OptionButton" && b.DataContext is PickerOption { Name: var n } && n == Strings.HabitTemplatePicker_FromScratch)
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(() => builder is { IsVisible: true } && builder.Content is HabitBuilderView);
            var view = (HabitBuilderView)builder!.Content!;
            var bvm = (HabitBuilderViewModel)view.DataContext!;
            Find<TextBox>(view, "NameBox").Text = name;
            Click(builder, Find<Button>(view, "AddQuantityButton"));
            PrivateDbHost.Pump();
            Click(builder, view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NewUnitButton" && b.IsEffectivelyVisible));
            PrivateDbHost.Pump();
            Find<TextBox>(view, "UnitFormName").Text = unit;
            PrivateDbHost.Pump();
            Click(builder, Find<Button>(view, "UnitFormConfirm"));
            PrivateDbHost.Pump();
            bvm.StagedUnits.Should().ContainSingle(u => u.Name == unit);
            Click(builder, Find<Button>(view, "LaunchButton"));
            await flow.WaitAsync(TimeSpan.FromSeconds(5));
            var id = vm.Home.SelectedRow!.HabitNodeId;

            using var scope = Host.Provider.CreateScope();
            var units = await scope.ServiceProvider.GetRequiredService<IReferenceDataQueryService>().ListUnitsAsync();
            var created = units.Should().ContainSingle(u => u.Name == unit).Subject;
            (await scope.ServiceProvider.GetRequiredService<IMetricDefinitionService>().ListByHabitAsync(id))
                .Should().ContainSingle().Which.UnitOfMeasureId.Should().Be(created.Id);

            await UntilAsync(() => window.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "RecordButton"));
            Find<Button>(window, "RecordButton").Command!.Execute(null);   // "+ Registra" apre il modulo
            await UntilAsync(() => window.GetVisualDescendants().OfType<MetricCellView>().Any());
            window.GetVisualDescendants().OfType<MetricCellView>().Single()
                .GetVisualDescendants().OfType<TextBlock>()
                .Should().Contain(t => t.Text == created.Code && t.IsEffectivelyVisible, "il quick-add mostra l'unita' nuova");
        }
        finally { window.Close(); builder?.Close(); }
    }
}
