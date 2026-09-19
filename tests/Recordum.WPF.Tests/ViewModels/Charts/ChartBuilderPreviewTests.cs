using System.Collections.Immutable;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services.Queries;
using Recordum.Plugins.Contracts.Charts;
using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.ExtensionPoints;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Charts;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Charts;

/// <summary>
/// Task 12 — the builder's live preview. Tests drive <see cref="ChartBuilderViewModel.RefreshPreviewAsync"/>
/// directly (no <see cref="Recordum.Core.Abstractions.ITickSourceFactory"/> is supplied here, so the
/// debounce tick source is never created — which is exactly why the guard exists).
/// </summary>
public sealed class ChartBuilderPreviewTests
{
    private static HomeTreeRow Habit(Guid id, string name, string color = "#123456", HabitType type = HabitType.Action) =>
        new(id, null, name, color, type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray<int>.Empty, ImmutableArray<Guid>.Empty);

    private static ExtensionPointRegistry RegistryWith(FakeChartRenderer renderer)
    {
        var registry = new ExtensionPointRegistry();
        registry.Register(new ExtensionRegistration(renderer.PluginId, renderer.PointId, renderer));
        return registry;
    }

    private static ChartBuilderViewModel BuildVm(IChartQueryService query, IExtensionPointRegistry registry) =>
        new(new FakeChartService(), new FakeHabitTreeQueryService(), new FakeMetricDefinitionService(), query, registry, null);

    [Fact]
    public async Task RefreshPreview_with_a_selected_habit_renders_a_non_null_view()
    {
        var renderer = new FakeChartRenderer();
        var vm = BuildVm(new FakeChartQueryService(), RegistryWith(renderer));
        vm.LoadNew();
        vm.AddSeriesCommand.Execute(null);
        vm.Series[0].SelectedHabit = Habit(Guid.NewGuid(), "Reading");

        await vm.RefreshPreviewAsync();

        Assert.False(vm.PreviewEmpty);
        Assert.NotNull(vm.PreviewView);
        Assert.True(renderer.CreateViewCalled);
    }

    [Fact]
    public async Task RefreshPreview_with_no_valid_rows_clears_the_view_and_flags_empty()
    {
        var vm = BuildVm(new FakeChartQueryService(), RegistryWith(new FakeChartRenderer()));
        vm.LoadNew();
        vm.AddSeriesCommand.Execute(null);   // a row with NO habit -> ToChartSeries == null

        await vm.RefreshPreviewAsync();

        Assert.Null(vm.PreviewView);
        Assert.True(vm.PreviewEmpty);
    }

    [Fact]
    public async Task RefreshPreview_transitions_from_rendered_back_to_empty()
    {
        var vm = BuildVm(new FakeChartQueryService(), RegistryWith(new FakeChartRenderer()));
        vm.LoadNew();
        vm.AddSeriesCommand.Execute(null);
        vm.Series[0].SelectedHabit = Habit(Guid.NewGuid(), "Reading");
        await vm.RefreshPreviewAsync();
        Assert.NotNull(vm.PreviewView);

        vm.RemoveSeriesCommand.Execute(vm.Series[0]);
        await vm.RefreshPreviewAsync();

        Assert.Null(vm.PreviewView);
        Assert.True(vm.PreviewEmpty);
    }

    [Fact]
    public async Task RefreshPreview_never_throws_when_the_query_service_fails()
    {
        var vm = BuildVm(new ThrowingChartQueryService(), RegistryWith(new FakeChartRenderer()));
        vm.LoadNew();
        vm.AddSeriesCommand.Execute(null);
        vm.Series[0].SelectedHabit = Habit(Guid.NewGuid(), "Reading");

        // Must not throw even though BuildModelAsync throws — the preview never crashes the builder.
        await vm.RefreshPreviewAsync();

        Assert.Null(vm.PreviewView);
        Assert.False(vm.PreviewEmpty);   // there WERE rows; the render itself just failed
    }

    private sealed class ThrowingChartQueryService : IChartQueryService
    {
        public ValueTask<ChartRenderModel> BuildModelAsync(Guid chartId, bool isDark, CancellationToken ct = default) =>
            throw new InvalidOperationException("boom");

        public ValueTask<ChartRenderModel> BuildModelAsync(Chart chart, IReadOnlyList<ChartSeries> series, bool isDark, CancellationToken ct = default) =>
            throw new InvalidOperationException("boom");
    }
}
