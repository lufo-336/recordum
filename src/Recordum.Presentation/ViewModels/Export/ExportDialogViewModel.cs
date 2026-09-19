using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services.Queries;
using Recordum.Plugins.Contracts.Export;
using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Presentation.Services;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.Export;

public sealed partial class ExportDialogViewModel : ObservableObject
{
    private readonly IExportQueryService _query;
    private readonly IExportJobService _jobs;
    private readonly IExtensionPointRegistry _registry;
    private readonly ISaveFileDialog _save;
    private readonly IExportTemplateService _templates;
    private readonly IClock _clock;
    private readonly IResetTimeResolver _resetTimes;

    private ExportSubject _subject = ExportSubject.SingleHabit;
    private Guid? _habitId;

    [ObservableProperty] private string _subjectLabel = "";
    [ObservableProperty] private ExportFormat _selectedFormat = ExportFormat.Pdf;

    [ObservableProperty] private bool _includeMetadata = true;
    [ObservableProperty] private bool _includeSummary = true;
    [ObservableProperty] private bool _includeHeatmap = true;
    [ObservableProperty] private bool _includeCompletions = true;
    [ObservableProperty] private bool _includeNotes = true;
    [ObservableProperty] private string _range = "all";

    public ObservableCollection<ExportTemplate> Templates { get; } = [];
    [ObservableProperty] private ExportTemplate? _selectedTemplate;

    public IReadOnlyList<ExportFormat> AvailableFormats { get; } = Enum.GetValues<ExportFormat>();

    public event EventHandler? Closed;

    public ExportDialogViewModel(
        IExportQueryService query, IExportJobService jobs, IExtensionPointRegistry registry,
        ISaveFileDialog save, IExportTemplateService templates, IClock clock, IResetTimeResolver resetTimes)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(resetTimes);
        _query = query; _jobs = jobs; _registry = registry; _save = save; _templates = templates; _clock = clock;
        _resetTimes = resetTimes;
    }

    public async Task LoadTemplatesAsync(CancellationToken ct = default)
    {
        Templates.Clear();
        foreach (var t in await _templates.ListAsync(ct).ConfigureAwait(true)) Templates.Add(t);
    }

    partial void OnSelectedTemplateChanged(ExportTemplate? value)
    {
        if (value is null) return;
        var layout = ExportLayout.Parse(value.LayoutJson);
        IncludeMetadata = layout.Metadata;
        IncludeSummary = layout.Summary;
        IncludeHeatmap = layout.Heatmap;
        IncludeCompletions = layout.Completions;
        IncludeNotes = layout.Notes;
        Range = layout.Range;
    }

    public void LoadForHabit(Guid habitId, string name)
    {
        _subject = ExportSubject.SingleHabit;
        _habitId = habitId;
        SubjectLabel = name;
    }

    public void LoadForLibrary()
    {
        _subject = ExportSubject.Library;
        _habitId = null;
        SubjectLabel = "Library";
    }

    private ExportLayout CurrentLayout()
        => new(IncludeMetadata, IncludeSummary, IncludeHeatmap, IncludeCompletions, IncludeNotes, Range);

    [RelayCommand]
    private async Task ExportAsync(CancellationToken ct)
    {
        var formatter = _registry.GetExtensions(WellKnownExtensionPoints.ExportFormat)
            .OfType<IExportFormatter>()
            .FirstOrDefault(f => f.CanFormat(SelectedFormat));
        if (formatter is null) return;

        var path = _save.Prompt(SuggestedFileName(), SelectedFormat);
        if (string.IsNullOrEmpty(path)) return;

        var layout = CurrentLayout();
        // Il dialog non ha un'abitudine di riferimento (puo' esportare l'intera libreria): usa il
        // confine dell'app, come il range dei grafici.
        var day = await _resetTimes.GetAppDayBoundaryAsync(ct).ConfigureAwait(true);
        var today = day.Today(_clock);
        var from = ResolveFrom(layout.Range, today);
        var request = new ExportRequest(_subject, _habitId, from, today, layout);

        var model = await _query.BuildAsync(request, ct).ConfigureAwait(true);
        var bytes = formatter.Write(model);
        await _save.WriteAsync(path, bytes, ct).ConfigureAwait(true);
        await _jobs.RecordAsync(new ExportJob { Format = SelectedFormat, FilePath = path }, ct).ConfigureAwait(true);
        Closed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(this, EventArgs.Empty);

    private string SuggestedFileName()
    {
        var ext = SelectedFormat switch
        {
            ExportFormat.Pdf => "pdf",
            ExportFormat.Html => "html",
            ExportFormat.Markdown => "md",
            ExportFormat.Json => "json",
            _ => "csv",
        };
        var stem = string.IsNullOrWhiteSpace(SubjectLabel) ? "recordum-export" : SubjectLabel;
        return $"{stem}.{ext}";
    }

    private static DateOnly ResolveFrom(string range, DateOnly today) => range switch
    {
        "30d" => today.AddDays(-29),
        "90d" => today.AddDays(-89),
        "1y" => today.AddDays(-364),
        _ => new DateOnly(2000, 1, 1),
    };
}
