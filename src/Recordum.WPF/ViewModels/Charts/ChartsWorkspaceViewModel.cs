using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Data.Services.Queries;
using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.WPF.ViewModels.Charts;

public sealed partial class ChartsWorkspaceViewModel : ObservableObject
{
    private readonly IChartService _charts;
    private readonly IChartQueryService _query;
    private readonly IExtensionPointRegistry _registry;

    public ObservableCollection<ChartListItemViewModel> Charts { get; } = [];
    public ChartBuilderViewModel Builder { get; }

    [ObservableProperty] private ChartListItemViewModel? _selected;
    [ObservableProperty] private object? _renderedView;
    [ObservableProperty] private bool _isBuilderOpen;

    /// <summary>Theme flag pushed in by the host (MainWindow) so the renderer themes correctly.</summary>
    public bool IsDarkTheme { get; set; }

    public ChartsWorkspaceViewModel(
        IChartService charts, IChartQueryService query, IExtensionPointRegistry registry, ChartBuilderViewModel builder)
    {
        ArgumentNullException.ThrowIfNull(charts);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(builder);
        _charts = charts;
        _query = query;
        _registry = registry;
        Builder = builder;
        Builder.Saved += async (_, id) => await OnBuilderSavedAsync(id).ConfigureAwait(true);
    }

    public async ValueTask LoadAsync(CancellationToken ct = default)
    {
        var all = await _charts.ListAsync(ct).ConfigureAwait(true);
        Charts.Clear();
        foreach (var c in all) Charts.Add(new ChartListItemViewModel(c));
    }

    public async ValueTask SelectAsync(ChartListItemViewModel? item, CancellationToken ct = default)
    {
        Selected = item;
        IsBuilderOpen = false;
        if (item is null) { RenderedView = null; return; }
        await RenderAsync(item.ChartId, ct).ConfigureAwait(true);
    }

    private async ValueTask RenderAsync(Guid chartId, CancellationToken ct)
    {
        var model = await _query.BuildModelAsync(chartId, IsDarkTheme, ct).ConfigureAwait(true);
        var renderer = _registry.GetExtensions(WellKnownExtensionPoints.ChartRenderer)
            .OfType<IChartRenderer>()
            .FirstOrDefault(r => r.CanRender(model.Kind));
        RenderedView = renderer?.CreateView(model);
    }

    [RelayCommand]
    private void New()
    {
        Builder.LoadNew();
        IsBuilderOpen = true;
    }

    [RelayCommand]
    private async Task Edit(ChartListItemViewModel item)
    {
        if (item is null) return;
        await Builder.LoadAsync(item.ChartId).ConfigureAwait(true);
        IsBuilderOpen = true;
    }

    [RelayCommand]
    private async Task Delete(ChartListItemViewModel item, CancellationToken ct)
    {
        if (item is null) return;
        await _charts.DeleteAsync(item.ChartId, ct).ConfigureAwait(true);
        Charts.Remove(item);
        if (Selected == item) { Selected = null; RenderedView = null; }
    }

    private async ValueTask OnBuilderSavedAsync(Guid id, CancellationToken ct = default)
    {
        IsBuilderOpen = false;
        await LoadAsync(ct).ConfigureAwait(true);
        var item = Charts.FirstOrDefault(c => c.ChartId == id);
        await SelectAsync(item, ct).ConfigureAwait(true);
    }
}
