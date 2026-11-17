using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Quick;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>GIO-22: la voce "Tag" del menu ⋯ e la pastiglia del tag scelto, che toglie il filtro.</summary>
public class HomeTagFilterViewTests
{
    private static readonly Guid Health = Guid.NewGuid();

    private static void Pump()
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs();
    }

    private static (Window W, HomeView V, HomeViewModel Vm) Show()
    {
        var query = new FakeHabitTreeQueryService();
        query.Rows.Add(Row("Corsa", Health));
        query.Rows.Add(Row("Lettura"));
        var vm = new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService(),
            quickActions: new QuickActionHandlerSelector([]), tags: new Tags());
        var view = new HomeView { DataContext = vm };
        var w = new Window { Width = 900, Height = 600, Content = view };
        w.Show();
        vm.LoadCommand.Execute(null);
        Pump();
        return (w, view, vm);
    }

    [AvaloniaFact]
    public void The_more_menu_lists_the_tags()
    {
        using var culture = TestCulture.Pin();
        var (w, view, _) = Show();
        var more = view.FindControl<Button>("MoreButton")!;
        var flyout = (MenuFlyout)more.Flyout!;
        flyout.ShowAt(more);
        Pump();

        var tagMenu = flyout.Items.OfType<MenuItem>().Single(i => Equals(i.Header, Strings.Home_TagMenu));
        tagMenu.IsVisible.Should().BeTrue();
        tagMenu.ItemCount.Should().Be(2, "\"tutti i tag\" e Salute");
        tagMenu.IsSubMenuOpen = true;
        Pump();
        ScreenshotProbeTests.Capture(w, "home-tag-menu-light");
        flyout.Hide();
        w.Close();
    }

    [AvaloniaFact]
    public void The_chip_names_the_tag_and_clears_the_filter()
    {
        using var culture = TestCulture.Pin();
        var (w, view, vm) = Show();
        var chip = view.FindControl<ToggleButton>("TagChip")!;
        chip.IsVisible.Should().BeFalse("senza filtro non c'è la pastiglia");

        vm.FilterByTagCommand.Execute(Health);
        Pump();

        chip.IsVisible.Should().BeTrue();
        chip.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Text == "Salute");
        ScreenshotProbeTests.Capture(w, "home-tag-chip-light");
        var p = chip.TranslatePoint(new Point(chip.Bounds.Width / 2, chip.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();

        vm.IsTagFilterOn.Should().BeFalse();
        chip.IsVisible.Should().BeFalse();
        vm.Rows.Should().HaveCount(2);
        w.Close();
    }

    private static HomeTreeRow Row(string name, params Guid[] tags) =>
        new(Guid.NewGuid(), null, name, "#2F7FC1", HabitType.Action, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray.Create(tags));

    private sealed class Tags : ITagService
    {
        public ValueTask<IReadOnlyList<Tag>> ListAsync(CancellationToken ct = default) =>
            new([new Tag { Id = Health, Name = "Salute" }]);

        public ValueTask<Guid> CreateAsync(Tag draft, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask UpdateAsync(Guid id, Action<Tag> mutate, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask AttachAsync(Guid habitNodeId, Guid tagId, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask DetachAsync(Guid habitNodeId, Guid tagId, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<Tag>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask SetTagsAsync(Guid habitNodeId, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
