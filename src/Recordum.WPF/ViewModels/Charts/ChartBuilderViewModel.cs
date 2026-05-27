using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Charts;

namespace Recordum.WPF.ViewModels.Charts;

public sealed partial class ChartBuilderViewModel(IChartService charts) : ObservableObject
{
    private Guid? _editingId;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private ChartKind _selectedKind = ChartKind.Line;
    [ObservableProperty] private string _range = "90d";
    [ObservableProperty] private string _bucket = "daily";

    public ObservableCollection<ChartSeriesRowViewModel> Series { get; } = [];
    public IReadOnlyList<ChartKind> AvailableKinds { get; } = Enum.GetValues<ChartKind>();

    /// <summary>Raised after a successful save, carrying the persisted chart id.</summary>
    public event EventHandler<Guid>? Saved;

    public void LoadNew()
    {
        _editingId = null;
        Name = "";
        SelectedKind = ChartKind.Line;
        Range = "90d";
        Bucket = "daily";
        Series.Clear();
    }

    public async ValueTask LoadAsync(Guid chartId, CancellationToken ct = default)
    {
        var chart = await charts.GetAsync(chartId, ct).ConfigureAwait(true);
        if (chart is null) { LoadNew(); return; }
        _editingId = chart.Id;
        Name = chart.Name;
        SelectedKind = ChartKindParser.Parse(chart.ChartType);
        var cfg = ChartConfig.Parse(chart.ConfigJson);
        Range = cfg.Range;
        Bucket = cfg.Bucket;
        Series.Clear();
        foreach (var s in chart.Series.OrderBy(s => s.SortOrder))
            Series.Add(new ChartSeriesRowViewModel { HabitNodeId = s.HabitNodeId, MetricDefinitionId = s.MetricDefinitionId, Label = s.Label, Color = s.Color });
    }

    [RelayCommand]
    private void AddSeries() => Series.Add(new ChartSeriesRowViewModel());

    [RelayCommand]
    private void RemoveSeries(ChartSeriesRowViewModel row) => Series.Remove(row);

    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
        var configJson = new ChartConfig(Range: Range, Bucket: Bucket).ToJson();
        var rows = Series.Select((r, i) => new ChartSeries
        {
            Id = Guid.NewGuid(),
            HabitNodeId = r.HabitNodeId,
            MetricDefinitionId = r.MetricDefinitionId,
            Label = r.Label,
            Color = r.Color,
            SortOrder = i,
        }).ToList();

        Guid id;
        if (_editingId is { } existing)
        {
            await charts.UpdateAsync(existing, c =>
            {
                c.Name = Name;
                c.ChartType = ChartKindParser.ToToken(SelectedKind);
                c.ConfigJson = configJson;
            }, ct).ConfigureAwait(true);
            await charts.ReplaceSeriesAsync(existing, rows, ct).ConfigureAwait(true);
            id = existing;
        }
        else
        {
            id = await charts.CreateAsync(new Chart
            {
                Name = Name,
                ChartType = ChartKindParser.ToToken(SelectedKind),
                ConfigJson = configJson,
            }, rows, ct).ConfigureAwait(true);
            _editingId = id;
        }
        Saved?.Invoke(this, id);
    }
}
