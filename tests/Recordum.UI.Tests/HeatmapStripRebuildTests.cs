using System.Collections.Immutable;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>La striscia della riga si costruisce una volta, non una per proprieta' che i binding impostano (fino a 4).</summary>
public class HeatmapStripRebuildTests
{
    [AvaloniaFact]
    public void A_row_strip_is_built_once()
    {
        var row = new HomeTreeRow(Guid.NewGuid(), null, "Acqua", "#2F7FC1", HabitType.Counter, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 1, 0, 1, 0, 1, 0), ImmutableArray<Guid>.Empty,
            CompletionsLast14Days: ImmutableArray.Create(0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0),
            ValuesLast14Days: ImmutableArray.Create<decimal?>(null, 2, null, 3, null, 1, null, null, 2, null, 4, null, 1, null),
            TodayCounterTotal: 3m);
        var vm = new HomeRowViewModel(row, 0, new FakeHabitNodeService(), new FakePurgeService());
        var view = new HabitRow { DataContext = vm };
        var w = new Window { Width = 700, Height = 120, Content = view };
        w.Show();
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }

        var strip = view.GetVisualDescendants().OfType<HeatmapStrip>().Single();
        strip.GetVisualDescendants().OfType<TextBlock>().Should().NotBeEmpty("la striscia e' disegnata");
        strip.RebuildCount.Should().Be(1);
        w.Close();
    }
}
