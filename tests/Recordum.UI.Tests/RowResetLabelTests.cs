using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Time;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>"reset 5:00" a sinistra della striscia mentre il giorno dell'abitudine e' indietro (Task 8, Step 1-bis).</summary>
public class RowResetLabelTests
{
    private static (Window W, HabitRow View, HomeRowViewModel Vm, PrivateDbHost.FakeClock Clock) Show(int hour)
    {
        var clock = new PrivateDbHost.FakeClock { Now = new DateTimeOffset(2026, 10, 28, hour, 0, 0, TimeSpan.Zero) };
        var row = new HomeTreeRow(Guid.NewGuid(), null, "Acqua", "#2F7FC1", HabitType.Action, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 1, 0, 1, 0, 1, 0), ImmutableArray<Guid>.Empty,
            HabitDay: new DayBoundary(TimeZoneInfo.Utc, new TimeOnly(5, 0)), AppDay: new DayBoundary(TimeZoneInfo.Utc, TimeOnly.MinValue));
        var vm = new HomeRowViewModel(row, 0, new FakeHabitNodeService(), new FakePurgeService(), clock: clock);
        var view = new HabitRow { DataContext = vm };
        var w = new Window { Width = 700, Height = 120, Content = view };
        w.Show();
        Pump();
        return (w, view, vm, clock);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
    }

    [AvaloniaFact]
    public void The_label_sits_left_of_the_strip_while_the_day_lags_and_goes_away_after_the_reset()
    {
        using var culture = TestCulture.Pin();
        var (w, view, vm, clock) = Show(hour: 2);
        var label = view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ResetPending");
        var strip = view.GetVisualDescendants().OfType<HeatmapStrip>().Single();

        label.IsEffectivelyVisible.Should().BeTrue();
        label.Text.Should().Be("reset 05:00");
        ToolTip.GetTip(label).Should().Be(vm.ResetPendingTip);
        label.TranslatePoint(new Point(label.Bounds.Width, 0), view)!.Value.X
            .Should().BeLessThanOrEqualTo(strip.TranslatePoint(new Point(0, 0), view)!.Value.X, "subito a sinistra della striscia");

        clock.Now = new DateTimeOffset(2026, 10, 28, 6, 0, 0, TimeSpan.Zero);
        vm.RefreshDayLag();
        Pump();

        label.IsEffectivelyVisible.Should().BeFalse();
        w.Close();
    }
}
