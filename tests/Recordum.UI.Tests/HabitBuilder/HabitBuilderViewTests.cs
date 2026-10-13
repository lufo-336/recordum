using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Microsoft.EntityFrameworkCore;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.UI.Services;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitBuilder;
using Xunit;

namespace Recordum.UI.Tests.HabitBuilder;

/// <summary>Il builder su Avalonia (F2b Task 2): compone, crea, si annulla senza lasciare traccia, regge il doppio
/// "Lancia" e accende i ToggleButton nel colore d'accento (W2V-9). Dati su DB privato.</summary>
public class HabitBuilderViewTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-hb");
    private static void Pump() => PrivateDbHost.Pump();

    private sealed record Opened(IServiceScope Scope, HabitBuilderViewModel Vm, HabitBuilderView View, Window Window);

    private static async Task<Opened> OpenAsync()
    {
        var scope = Host.Provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<HabitBuilderViewModel>();
        await vm.InitializeAsync();
        var view = new HabitBuilderView { DataContext = vm };
        var w = new Window { Width = 1100, Height = 1400, Content = view };
        w.Show();
        Pump();
        return new Opened(scope, vm, view, w);
    }

    private static T Named<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static T VisibleNamed<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name && c.IsEffectivelyVisible);

    private static void Click(Window w, Control c)
    {
        c.BringIntoView();   // la colonna scorre: un pulsante sotto la piega sta dietro la barra delle azioni
        Pump();
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 150 && !done(); i++) { await Task.Delay(20); Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    /// <summary>Il testo che la casella chiusa mostra davvero (il TextBlock della selezione), non l'oggetto scelto.</summary>
    private static string? Shown(ComboBox box) =>
        box.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrEmpty(t));

    private static string Unique(string stem) => $"{stem}-{Guid.NewGuid():N}";

    private static async Task<(HabitNode? Node, IReadOnlyList<MetricDefinition> Metrics)> LoadAsync(IServiceProvider sp, Guid id)
    {
        var node = await sp.GetRequiredService<IHabitNodeService>().GetAsync(id);
        var metrics = await sp.GetRequiredService<IMetricDefinitionService>().ListByHabitAsync(id);
        return (node, metrics);
    }

    // ---- 2.1 ----------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task A_simple_action_is_launched_through_the_launcher_which_returns_its_id()
    {
        var launcher = new AvaloniaHabitBuilderLauncher(Host.Provider, () => null);
        Window? window = null;
        launcher.WindowCreatedForTests += created => window = created;
        var task = launcher.LaunchAsync();
        try
        {
            await UntilAsync(() => window is { IsVisible: true } && window.Content is HabitBuilderView);
            var view = (HabitBuilderView)window!.Content!;
            var name = Unique("Stretch");
            Named<TextBox>(view, "NameBox").Text = name;
            Pump();
            Click(window, Named<Button>(view, "LaunchButton"));

            var id = await task.WaitAsync(TimeSpan.FromSeconds(5));
            id.Should().NotBeNull();
            using var scope = Host.Provider.CreateScope();
            var (node, _) = await LoadAsync(scope.ServiceProvider, id!.Value);
            node!.Name.Should().Be(name);
            node.HabitType.Should().Be(HabitType.Action);
        }
        finally { window?.Close(); }
    }

    [AvaloniaFact]
    public async Task A_counter_with_a_quantity_field_and_a_unit_is_created()
    {
        var o = await OpenAsync();
        try
        {
            var name = Unique("Water");
            Named<TextBox>(o.View, "NameBox").Text = name;
            Click(o.Window, Named<Button>(o.View, "AddQuantityButton"));
            var picker = VisibleNamed<ComboBox>(o.View, "UnitPicker");
            picker.SelectedIndex = 0;
            Pump();
            o.Vm.DerivedType.Should().Be(HabitType.Counter);
            Click(o.Window, Named<Button>(o.View, "LaunchButton"));
            await UntilAsync(() => o.Vm.CreatedHabitNodeId is not null);

            var (node, metrics) = await LoadAsync(o.Scope.ServiceProvider, o.Vm.CreatedHabitNodeId!.Value);
            node!.HabitType.Should().Be(HabitType.Counter);
            metrics.Should().ContainSingle().Which.UnitOfMeasureId.Should().NotBeNull();
        }
        finally { o.Window.Close(); o.Scope.Dispose(); }
    }

    [AvaloniaFact]
    public async Task A_state_on_a_scale_is_created()
    {
        var o = await OpenAsync();
        try
        {
            Named<TextBox>(o.View, "NameBox").Text = Unique("Mood");
            Click(o.Window, Named<Button>(o.View, "AddScaleButton"));
            VisibleNamed<ComboBox>(o.View, "ScalePicker").SelectedIndex = 0;
            Pump();
            Click(o.Window, Named<Button>(o.View, "LaunchButton"));
            await UntilAsync(() => o.Vm.CreatedHabitNodeId is not null);

            var (node, metrics) = await LoadAsync(o.Scope.ServiceProvider, o.Vm.CreatedHabitNodeId!.Value);
            node!.HabitType.Should().Be(HabitType.State);
            metrics.Should().ContainSingle().Which.ScaleTemplateId.Should().NotBeNull();
        }
        finally { o.Window.Close(); o.Scope.Dispose(); }
    }

    [AvaloniaFact]
    public async Task An_abstinence_is_created()
    {
        var o = await OpenAsync();
        try
        {
            Named<TextBox>(o.View, "NameBox").Text = Unique("NoSugar");
            Named<CheckBox>(o.View, "AbstinenceCheck").IsChecked = true;
            Pump();
            Click(o.Window, Named<Button>(o.View, "LaunchButton"));
            await UntilAsync(() => o.Vm.CreatedHabitNodeId is not null);

            var (node, _) = await LoadAsync(o.Scope.ServiceProvider, o.Vm.CreatedHabitNodeId!.Value);
            node!.HabitType.Should().Be(HabitType.Resistance);
        }
        finally { o.Window.Close(); o.Scope.Dispose(); }
    }

    [AvaloniaFact]
    public async Task The_preview_host_exists_and_is_empty()
    {
        var o = await OpenAsync();
        try { Named<Border>(o.View, "PreviewHost").Child.Should().BeNull("lo riempie il Task 3"); }
        finally { o.Window.Close(); o.Scope.Dispose(); }
    }

    // ---- 2.2 ----------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task Cancel_closes_the_window_and_returns_null()
    {
        var launcher = new AvaloniaHabitBuilderLauncher(Host.Provider, () => null);
        Window? window = null;
        launcher.WindowCreatedForTests += created => window = created;
        var task = launcher.LaunchAsync();
        try
        {
            await UntilAsync(() => window is { IsVisible: true } && window.Content is HabitBuilderView);
            var view = (HabitBuilderView)window!.Content!;
            Named<TextBox>(view, "NameBox").Text = Unique("Never");
            Pump();
            Click(window, Named<Button>(view, "CancelButton"));
            (await task.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeNull();
            window.IsVisible.Should().BeFalse();
        }
        finally { window?.Close(); }
    }

    [AvaloniaFact]
    public async Task Closing_with_the_system_button_returns_null_too()
    {
        var launcher = new AvaloniaHabitBuilderLauncher(Host.Provider, () => null);
        Window? window = null;
        launcher.WindowCreatedForTests += created => window = created;
        var task = launcher.LaunchAsync();
        await UntilAsync(() => window is { IsVisible: true });
        window!.Close();
        (await task.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeNull();
    }

    // ---- 2.3 (Review Focus 3) -----------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task Launching_twice_creates_a_single_habit()
    {
        var o = await OpenAsync();
        try
        {
            var name = Unique("Once");
            o.Vm.Name = name;
            var a = o.Vm.LaunchCommand.ExecuteAsync(null);
            var b = o.Vm.LaunchCommand.ExecuteAsync(null);
            await Task.WhenAll(a, b);
            await o.Vm.LaunchCommand.ExecuteAsync(null);   // e un terzo, a cosa fatta

            o.Vm.CreatedHabitNodeId.Should().NotBeNull();
            using var scope = Host.Provider.CreateScope();
            var count = await CountByNameAsync(scope.ServiceProvider, name);
            count.Should().Be(1);
        }
        finally { o.Window.Close(); o.Scope.Dispose(); }
    }

    private static async Task<int> CountByNameAsync(IServiceProvider sp, string name)
    {
        var db = sp.GetRequiredService<Recordum.Data.RecordumDbContext>();
        return await db.HabitNodes.IgnoreQueryFilters().CountAsync(n => n.Name == name);
    }

    // ---- 2.4 (Review Focus 2) -----------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task A_custom_unit_and_scale_staged_and_then_cancelled_leave_no_rows()
    {
        var launcher = new AvaloniaHabitBuilderLauncher(Host.Provider, () => null);
        Window? window = null;
        launcher.WindowCreatedForTests += created => window = created;
        var task = launcher.LaunchAsync();
        var unit = Unique("Glass");
        var scale = Unique("Intensity");
        try
        {
            await UntilAsync(() => window is { IsVisible: true } && window.Content is HabitBuilderView);
            var view = (HabitBuilderView)window!.Content!;
            var vm = (HabitBuilderViewModel)view.DataContext!;
            Named<TextBox>(view, "NameBox").Text = Unique("Staged");

            Click(window, Named<Button>(view, "AddQuantityButton"));
            Click(window, VisibleNamed<Button>(view, "NewUnitButton"));
            Named<TextBox>(view, "UnitFormName").Text = unit;
            Pump();
            Click(window, Named<Button>(view, "UnitFormConfirm"));
            vm.StagedUnits.Should().ContainSingle(u => u.Name == unit);

            Click(window, Named<Button>(view, "AddScaleButton"));
            Click(window, VisibleNamed<Button>(view, "NewScaleButton"));
            Named<TextBox>(view, "ScaleFormName").Text = scale;
            Named<TextBox>(view, "ScaleFormLabels").Text = "low, high";
            Pump();
                        Click(window, Named<Button>(view, "ScaleFormConfirm"));
            vm.StagedScales.Should().ContainSingle(s => s.Name == scale);

            Click(window, Named<Button>(view, "CancelButton"));
            (await task.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeNull();

            using var scope = Host.Provider.CreateScope();
            var refData = scope.ServiceProvider.GetRequiredService<IReferenceDataQueryService>();
            (await refData.ListUnitsAsync()).Should().NotContain(u => u.Name == unit);
            (await refData.ListScalesAsync()).Should().NotContain(s => s.Name == scale);
        }
        finally { window?.Close(); }
    }

    // ---- 2.5 (W2V-9) --------------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task Checked_toggle_buttons_are_painted_in_the_accent_colour()
    {
        var o = await OpenAsync();
        try
        {
            o.Vm.Name = Unique("Toggles");
            Click(o.Window, Named<Button>(o.View, "AddQuantityButton"));
            o.Vm.Schedule.IsExpanded = true;
            o.Vm.Schedule.ScheduleType = ScheduleType.SpecificDays;
            o.Vm.Schedule.Days[0].IsSelected = true;
            Pump();

            o.Window.TryFindResource("Brush.Accent", o.Window.ActualThemeVariant, out var res).Should().BeTrue();
            var accent = ((ISolidColorBrush)res!).Color;
            var goal = Named<ToggleButton>(o.View, "GoalToggle");
            goal.IsChecked = true;
            var day = o.View.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "DayToggle");
            Pump();
            var frame = o.Window.CaptureRenderedFrame()!;
            foreach (var t in new[] { goal, day })
            {
                t.IsChecked.Should().BeTrue();
                var at = t.TranslatePoint(new Point(3, t.Bounds.Height / 2), o.Window)!.Value;
                Pixel(frame, at).Should().Match<Color>(c => Near(c, accent), $"{t.Name} acceso nel colore d'accento, non nel blu di sistema");
            }
        }
        finally { o.Window.Close(); o.Scope.Dispose(); }
    }

    private static bool Near(Color a, Color b) =>
        Math.Abs(a.R - b.R) <= 4 && Math.Abs(a.G - b.G) <= 4 && Math.Abs(a.B - b.B) <= 4;

    private static Color Pixel(Bitmap frame, Point at)
    {
        var buffer = new byte[4];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try { frame.CopyPixels(new PixelRect((int)at.X, (int)at.Y, 1, 1), handle.AddrOfPinnedObject(), 4, 4); }
        finally { handle.Free(); }
        return frame.Format == Avalonia.Platform.PixelFormats.Bgra8888
            ? Color.FromRgb(buffer[2], buffer[1], buffer[0])
            : Color.FromRgb(buffer[0], buffer[1], buffer[2]);
    }

    // ---- picker bindings (compiled): displayed text and SelectedValue path ---------------------------------

    [AvaloniaFact]
    public async Task Pickers_show_the_item_name_and_write_the_selected_id_to_the_field()
    {
        var o = await OpenAsync();
        try
        {
            Click(o.Window, Named<Button>(o.View, "AddQuantityButton"));
            var unit = VisibleNamed<ComboBox>(o.View, "UnitPicker");
            unit.SelectedIndex = 1;
            Pump();
            Shown(unit).Should().Be(o.Vm.AvailableUnits[1].Name);
            o.Vm.Fields[0].UnitOfMeasureId.Should().Be(o.Vm.AvailableUnits[1].Id);

            Click(o.Window, Named<Button>(o.View, "AddTimeButton"));
            var time = VisibleNamed<ComboBox>(o.View, "TimeUnitPicker");
            time.SelectedIndex = 1;
            Pump();
            Shown(time).Should().Be(o.Vm.AvailableTimeUnits[1].Name);
            o.Vm.Fields[1].UnitOfMeasureId.Should().Be(o.Vm.AvailableTimeUnits[1].Id);
        }
        finally { o.Window.Close(); o.Scope.Dispose(); }
    }

    [AvaloniaFact]
    public async Task The_journal_gesture_picker_shows_the_label_and_writes_the_gesture()
    {
        var o = await OpenAsync();
        try
        {
            Click(o.Window, Named<Button>(o.View, "AddTextButton"));
            o.Vm.ShowJournalGesture.Should().BeTrue();
            var box = VisibleNamed<ComboBox>(o.View, "JournalGestureBox");
            var other = o.Vm.JournalGestureOptions.First(g => g.Gesture != o.Vm.JournalGesture);
            box.SelectedIndex = o.Vm.JournalGestureOptions.ToList().IndexOf(other);
            Pump();
            Shown(box).Should().Be(other.Label);
            o.Vm.JournalGesture.Should().Be(other.Gesture);
        }
        finally { o.Window.Close(); o.Scope.Dispose(); }
    }

    // ---- window tokens (font and background), light and dark ------------------------------------------------

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Code_built_windows_take_the_font_and_background_tokens(string variant)
    {
        var theme = variant == "Dark" ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        var launcher = new AvaloniaHabitBuilderLauncher(Host.Provider, () => null);
        var windows = new List<Window> { new Recordum.UI.Views.Startup.ConfirmationWindow("t", "m") };
        launcher.WindowCreatedForTests += windows.Add;
        var task = launcher.LaunchAsync();
        await UntilAsync(() => windows.Count == 2);
        foreach (var w in windows)
        {
            w.RequestedThemeVariant = theme;
            w.Show();
            Pump();
            try
            {
                w.TryFindResource("Brush.Background", theme, out var bg).Should().BeTrue();
                w.TryFindResource("Font.Default", theme, out var font).Should().BeTrue();
                ((ISolidColorBrush)w.Background!).Color.Should().Be(((ISolidColorBrush)bg!).Color, $"{w.GetType().Name} {variant}");
                w.FontFamily.Should().Be((FontFamily)font!, w.GetType().Name);
            }
            finally { w.Close(); }
        }
        (await task.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeNull();
    }

    // ---- numeric inputs: what is saved is what is shown --------------------------------------------------

    [AvaloniaTheory]
    [InlineData("it-IT", "2,5", 2.5, false)]
    [InlineData("it-IT", "2.5", null, true)]
    [InlineData("it-IT", "1.500", null, true)]
    [InlineData("en-US", "2.5", 2.5, false)]
    [InlineData("en-US", "2,5", null, true)]
    [InlineData("en-US", "1,500", null, true)]
    public async Task The_goal_target_rejects_the_other_separator_instead_of_multiplying(string culture, string text, double? expected, bool rejected)
    {
        var old = (System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CultureInfo.CurrentUICulture);
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(culture);
        var o = await OpenAsync();
        try
        {
            Click(o.Window, Named<Button>(o.View, "AddQuantityButton"));
            o.Vm.GoalBrick.HasGoal = true;
            o.Vm.GoalBrick.Target = 7m;
            Pump();
            var box = Named<TextBox>(o.View, "GoalTargetBox");
            box.Text = text;
            Pump();
            if (rejected)
            {
                DataValidationErrors.GetHasErrors(box).Should().BeTrue();
                o.Vm.GoalBrick.Target.Should().BeNull("un rifiuto svuota il valore: Lancia non salva il vecchio mentre la casella mostra un altro testo");
                box.Text.Should().Be(text);
            }
            else o.Vm.GoalBrick.Target.Should().Be((decimal)expected!.Value);
            o.Vm.GoalBrick.Target.Should().NotBe(7m).And.NotBe(25m).And.NotBe(1500m);
        }
        finally
        {
            o.Window.Close(); o.Scope.Dispose();
            System.Globalization.CultureInfo.CurrentCulture = old.CurrentCulture;
            System.Globalization.CultureInfo.CurrentUICulture = old.CurrentUICulture;
        }
    }

    [AvaloniaTheory]
    [InlineData("it-IT", "2.5")]
    [InlineData("en-US", "2,5")]
    public async Task The_weekly_target_rejects_what_is_not_an_integer(string culture, string text)
    {
        var old = (System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CultureInfo.CurrentUICulture);
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(culture);
        var o = await OpenAsync();
        try
        {
            o.Vm.Schedule.IsExpanded = true;
            o.Vm.Schedule.ScheduleType = ScheduleType.WeeklyX;
            o.Vm.Schedule.WeeklyTarget = 3;
            Pump();
            var box = Named<TextBox>(o.View, "WeeklyTargetBox");
            box.Text = text;
            Pump();
            DataValidationErrors.GetHasErrors(box).Should().BeTrue();
            o.Vm.Schedule.WeeklyTarget.Should().BeNull();
        }
        finally
        {
            o.Window.Close(); o.Scope.Dispose();
            System.Globalization.CultureInfo.CurrentCulture = old.CurrentCulture;
            System.Globalization.CultureInfo.CurrentUICulture = old.CurrentUICulture;
        }
    }
}
