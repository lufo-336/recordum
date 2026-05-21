using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

public sealed partial class StateQuickAddViewModel(
    ICompletionService completions,
    IMetricDefinitionService metrics,
    IClock clock) : QuickAddViewModelBase(completions, clock)
{
    private readonly IMetricDefinitionService _metrics = metrics;

    [ObservableProperty] private MetricDefinition? _primaryMetric;
    [ObservableProperty] private int? _selectedScaleIndex;
    [ObservableProperty] private string? _notes;

    public IReadOnlyList<string> ScaleLabels { get; private set; } = [];

    public async Task LoadMetricsAsync(CancellationToken ct = default)
    {
        if (Node is null) return;
        var list = await _metrics.ListByHabitAsync(Node.Id, ct).ConfigureAwait(true);
        PrimaryMetric = list.OrderBy(m => m.SortOrder).FirstOrDefault();
        ScaleLabels = ParseLabels(PrimaryMetric?.LabelsJson);
        OnPropertyChanged(nameof(ScaleLabels));
    }

    [RelayCommand]
    private Task Save(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        if (Node is null || PrimaryMetric is null || SelectedScaleIndex is null) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var (ts, ms, retro) = ResolveCompletionTimestamp();
            var draft = new Completion
            {
                HabitNodeId = Node.Id,
                CompletedAt = ts,
                CompletedAtMs = ms,
                IsRetroactive = retro,
                Notes = Notes,
            };
            var values = new[]
            {
                new CompletionValue
                {
                    MetricDefinitionId = PrimaryMetric.Id,
                    SeriesIndex = 0,
                    ValueInteger = SelectedScaleIndex.Value,
                },
            };
            var id = await Completions.RecordAsync(draft, values, ct: ct).ConfigureAwait(true);
            OnSaved(id);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private static string[] ParseLabels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
