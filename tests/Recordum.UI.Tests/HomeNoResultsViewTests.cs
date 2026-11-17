using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Quick;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>GIO-5: in stato <see cref="HomeState.NoResults"/> la vista mostra "Togli i filtri" e lascia
/// visibile la lente; un clic sul pulsante ripristina le righe.</summary>
public class HomeNoResultsViewTests
{
    private static void Pump()
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs();
    }

    private static HomeTreeRow Row(string name, bool doneToday = false) =>
        new(Guid.NewGuid(), null, name, "#2F7FC1", HabitType.Action, null, 0, doneToday, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, doneToday ? 1 : 0), ImmutableArray<Guid>.Empty);

    [AvaloniaFact]
    public void NoResults_shows_clear_button_and_search_lens_and_clicking_clear_restores_rows()
    {
        using var culture = TestCulture.Pin();
        var query = new FakeHabitTreeQueryService();
        query.Rows.Add(Row("Corsa", doneToday: true));
        query.Rows.Add(Row("Lettura", doneToday: true));
        var vm = new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService(),
            quickActions: new QuickActionHandlerSelector([]));
        var view = new HomeView { DataContext = vm };
        var w = new Window { Width = 900, Height = 600, Content = view };
        w.Show();
        vm.LoadCommand.Execute(null);
        Pump();

        vm.HideCompletedToday = true;
        Pump();

        vm.State.Should().Be(HomeState.NoResults);
        var clear = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ClearFiltersButton");
        clear.IsEffectivelyVisible.Should().BeTrue();
        clear.Content.Should().Be(Strings.Home_NoResults_Clear);
        view.FindControl<Button>("SearchButton")!.IsEffectivelyVisible.Should().BeTrue();

        var p = clear.TranslatePoint(new Point(clear.Bounds.Width / 2, clear.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();

        vm.State.Should().Be(HomeState.Loaded);
        vm.Rows.Should().HaveCount(2);
        w.Close();
    }
}
