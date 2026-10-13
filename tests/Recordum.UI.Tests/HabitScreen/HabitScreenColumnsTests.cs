using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>F2b Task 5: la schermata a due colonne (Cronologia + Configura), fill-then-scroll, maniglia, focus.</summary>
public class HabitScreenColumnsTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-columns");

    private static async Task<(HabitScreenViewModel Vm, HabitScreenView View, Window Window, IServiceScope Scope)> OpenAsync(double width)
    {
        var scope = Host.Provider.CreateScope();
        var id = await scope.ServiceProvider.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Colonne", Color = "#E11D48",
        });
        var vm = scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        vm.IsCronologiaOpen = true;
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = width, Height = 800, Content = view };
        w.Show();
        PrivateDbHost.Pump();
        return (vm, view, w, scope);
    }

    private static T Named<T>(HabitScreenView v, string name) where T : Control =>
        v.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaFact]
    public async Task The_configura_chip_opens_and_closes_the_column_and_a_closed_column_builds_nothing()
    {
        var (vm, view, w, scope) = await OpenAsync(1200);
        try
        {
            var chip = Named<CheckBox>(view, "ChipConfigura");
            chip.IsVisible.Should().BeTrue("il chip Configura e' acceso in F2b");
            Named<Border>(view, "ConfiguraColumn").IsVisible.Should().BeFalse();
            Named<Border>(view, "ConfiguraHost").Child.Should().BeNull();
            view.GetVisualDescendants().Where(d => d.GetVisualAncestors().Any(a => (a as Control)?.Name == "ConfiguraHost"))
                .Should().BeEmpty("chiusa non costruisce niente, in nessun punto della view");

            chip.IsChecked = true;
            PrivateDbHost.Pump();
            vm.IsConfiguraOpen.Should().BeTrue();
            Named<Border>(view, "ConfiguraColumn").IsVisible.Should().BeTrue();
            Named<Border>(view, "ConfiguraColumn").Bounds.Width.Should().BeGreaterThan(0);

            chip.IsChecked = false;
            PrivateDbHost.Pump();
            vm.IsConfiguraOpen.Should().BeFalse();
            Named<Border>(view, "ConfiguraColumn").IsVisible.Should().BeFalse();
        }
        finally { w.Close(); scope.Dispose(); }
    }

    [AvaloniaFact]
    public async Task In_a_narrow_window_no_column_is_below_320_and_the_columns_scroll_horizontally()
    {
        var (vm, view, w, scope) = await OpenAsync(600);
        try
        {
            vm.IsConfiguraOpen = true;
            PrivateDbHost.Pump();
            var scroll = Named<ScrollViewer>(view, "ColumnsScroll");
            Named<Border>(view, "CronologiaColumn").Bounds.Width.Should().BeGreaterThanOrEqualTo(320);
            Named<Border>(view, "ConfiguraColumn").Bounds.Width.Should().BeGreaterThanOrEqualTo(320);
            scroll.Extent.Width.Should().BeGreaterThan(scroll.Viewport.Width, "due colonne da 320 non entrano in 600");
            scroll.Offset = new Vector(10_000, 0);
            PrivateDbHost.Pump();
            scroll.Offset.X.Should().BeGreaterThan(0, "lo scorrimento orizzontale c'e'");
        }
        finally { w.Close(); scope.Dispose(); }
    }

    [AvaloniaFact]
    public async Task In_a_wide_window_the_two_columns_fill_the_width_without_scrolling()
    {
        var (vm, view, w, scope) = await OpenAsync(1200);
        try
        {
            vm.IsConfiguraOpen = true;
            PrivateDbHost.Pump();
            var scroll = Named<ScrollViewer>(view, "ColumnsScroll");
            scroll.Extent.Width.Should().BeApproximately(scroll.Viewport.Width, 0.5);
            var sum = Named<Border>(view, "CronologiaColumn").Bounds.Width + Named<Border>(view, "ConfiguraColumn").Bounds.Width;
            sum.Should().BeApproximately(scroll.Viewport.Width, 1);
        }
        finally { w.Close(); scope.Dispose(); }
    }

    private static async Task<string?> WaitSettingAsync(IAppSettingService s, Func<string?, bool> ok)
    {
        string? v = null;
        for (var i = 0; i < 200; i++)
        {
            v = await s.GetAsync("ui.column_widths");
            if (ok(v)) break;
            await Task.Delay(10);
        }
        return v;
    }

    [AvaloniaFact]
    public async Task Dragging_the_handle_resizes_and_persists_on_release_and_double_click_resets()
    {
        var (vm, view, w, scope) = await OpenAsync(1200);
        try
        {
            vm.IsConfiguraOpen = true;
            PrivateDbHost.Pump();
            var settings = scope.ServiceProvider.GetRequiredService<IAppSettingService>();
            var thumb = Named<Thumb>(view, "CronologiaResizeThumb");
            var before = Named<Border>(view, "CronologiaColumn").Bounds.Width;
            var start = vm.CronologiaWidth;

            thumb.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = new Vector(40, 0) });
            PrivateDbHost.Pump();
            vm.CronologiaWidth.Should().Be(start + 40);
            Named<Border>(view, "CronologiaColumn").Bounds.Width.Should().BeGreaterThan(before);
            (await settings.GetAsync("ui.column_widths")).Should().BeNull("durante il trascinamento non si persiste");

            thumb.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent, Vector = new Vector(40, 0) });
            var saved = await WaitSettingAsync(settings, v => v is not null);
            saved.Should().Contain($"Cronologia={(int)(start + 40)}");

            var p = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, 20), w)!.Value;
            w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left);
            w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left);
            PrivateDbHost.Pump();
            vm.CronologiaWidth.Should().Be(start, "il doppio clic riporta al default");
            (await WaitSettingAsync(settings, v => v!.Contains($"Cronologia={(int)start}"))).Should().Contain($"Cronologia={(int)start}");
        }
        finally { w.Close(); scope.Dispose(); }
    }

    [AvaloniaFact]
    public async Task Focus_mode_shows_a_single_column_and_the_return_button()
    {
        var (vm, view, w, scope) = await OpenAsync(1200);
        try
        {
            vm.IsConfiguraOpen = true;
            PrivateDbHost.Pump();
            Named<Button>(view, "ExitFocusButton").IsVisible.Should().BeFalse();

            var focus = Named<Button>(view, "ConfiguraFocusButton");
            focus.Command!.Execute(focus.CommandParameter);
            PrivateDbHost.Pump();

            Named<Border>(view, "CronologiaColumn").IsVisible.Should().BeFalse();
            Named<Border>(view, "ConfiguraColumn").IsVisible.Should().BeTrue();
            Named<StackPanel>(view, "ChipBar").IsVisible.Should().BeFalse();
            var exit = Named<Button>(view, "ExitFocusButton");
            exit.IsVisible.Should().BeTrue();
            var scroll = Named<ScrollViewer>(view, "ColumnsScroll");
            Named<Border>(view, "ConfiguraColumn").Bounds.Width.Should().BeApproximately(scroll.Viewport.Width, 1);

            exit.Command!.Execute(exit.CommandParameter);
            PrivateDbHost.Pump();
            Named<Border>(view, "CronologiaColumn").IsVisible.Should().BeTrue();
            Named<StackPanel>(view, "ChipBar").IsVisible.Should().BeTrue();
        }
        finally { w.Close(); scope.Dispose(); }
    }
}
