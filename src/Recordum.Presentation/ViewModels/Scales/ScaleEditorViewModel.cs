using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Scales;
using Recordum.Core.Text;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Templates;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for a UI string
namespace Recordum.Presentation.ViewModels.Scales;

/// <summary>Una riga dell'editor: uno scaglione (valore intero + nome). Il nome può restare vuoto: è una tacca.</summary>
public sealed partial class ScaleAnchorRowViewModel(ScaleEditorViewModel owner, int value, string label) : ObservableObject
{
    public ScaleEditorViewModel Owner { get; } = owner;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
    private int _value = value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagPreview))]
    private string _label = label;

    /// <summary>Il tag che il nome produrrà, con il <c>#</c>; niente tag per una ufficiale o per un nome che non ne dà uno valido.</summary>
    public string? TagPreview => !Owner.IsReadOnly && TagNames.FromLabel(Label) is { } t ? "#" + t : null;

    public string AutomationName =>
        string.Format(CultureInfo.CurrentCulture, Strings.Scale_Editor_MarkName, Value);

    internal void TagPreviewChanged() => OnPropertyChanged(nameof(TagPreview));

    partial void OnValueChanged(int value) => Owner.RowEdited();
    partial void OnLabelChanged(string value) => Owner.RowEdited();
}

public sealed record PrecisionChoice(ScalePrecision Value, string Label);

/// <summary>L'editor di una scala (GIO-23, spec §6): nome, estremi interi, precisione, scaglioni. Lo usano il creatore e
/// Configura. Il VM non scrive: dice <see cref="CanConfirm"/> e dà <see cref="ToDraft"/>; chi lo ospita salva.
/// <para>Caricare (<see cref="Load"/>) non solleva <see cref="Changed"/>: le righe si ricostruiscono, non si modificano (CLAUDE.md §8).</para></summary>
public sealed partial class ScaleEditorViewModel : ObservableObject
{
    private bool _loading;
    private ScaleTemplate? _source;
    private ScaleUsage? _usage;

    public ScaleEditorViewModel() => Rows.CollectionChanged += (_, _) => Refresh();

    public ObservableCollection<ScaleAnchorRowViewModel> Rows { get; } = [];

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _minValue = 1;
    [ObservableProperty] private int _maxValue = 5;
    [ObservableProperty] private ScalePrecision _precision = ScalePrecision.Integer;

    public IReadOnlyList<PrecisionChoice> PrecisionChoices { get; } =
        [new(ScalePrecision.Integer, Strings.Scale_Precision_Integer), new(ScalePrecision.Decimal, Strings.Scale_Precision_Decimal)];

    /// <summary>Una scala ufficiale non si modifica: si duplica.</summary>
    public bool IsReadOnly => _source?.IsOfficial == true;

    public IReadOnlyList<string> Errors { get; private set; } = [];
    public bool CanConfirm { get; private set; }
    public IReadOnlyList<ScalePoint> Preview { get; private set; } = [];

    public string? UsageText => _usage is { HabitCount: > 1 } u
        ? string.Format(CultureInfo.CurrentCulture, Strings.Scale_Editor_Usage, u.HabitCount) : null;

    /// <summary>Una modifica dell'utente (non un caricamento).</summary>
    public event EventHandler? Changed;

    partial void OnNameChanged(string value) => Edited();
    partial void OnMinValueChanged(int value) => Edited();
    partial void OnMaxValueChanged(int value) => Edited();
    partial void OnPrecisionChanged(ScalePrecision value) => Edited();

    internal void RowEdited() => Edited();

    private void Edited()
    {
        if (_loading) return;
        Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Load(ScaleTemplate s, ScaleUsage? usage)
    {
        ArgumentNullException.ThrowIfNull(s);
        _loading = true;
        try
        {
            _source = s;
            Name = OfficialTexts.ScaleName(s);
            MinValue = s.MinValue;
            MaxValue = s.MaxValue;
            Precision = s.Precision;
            Rows.Clear();
            if (s.IsOfficial)
                foreach (var a in ScaleText.Named(s)) Rows.Add(new ScaleAnchorRowViewModel(this, a.Value, a.Name));
            else
                foreach (var a in s.Anchors.OrderBy(a => a.Value)) Rows.Add(new ScaleAnchorRowViewModel(this, a.Value, a.Label ?? string.Empty));
            _usage = usage;
        }
        finally { _loading = false; }
        Refresh();
        OnPropertyChanged(nameof(IsReadOnly));
        foreach (var r in Rows) r.TagPreviewChanged();
    }

    /// <summary>Nessuna scala scelta: l'editor e' vuoto e non e' una modifica.</summary>
    public void Clear()
    {
        _loading = true;
        try { _source = null; _usage = null; Name = string.Empty; Rows.Clear(); }
        finally { _loading = false; }
        Refresh();
        OnPropertyChanged(nameof(IsReadOnly));
    }

    /// <summary>L'uso arriva dopo (è una query): rivaluta gli errori senza toccare le righe.</summary>
    public void ApplyUsage(ScaleUsage usage)
    {
        _usage = usage;
        Refresh();
    }

    [RelayCommand]
    private void AddRow()
    {
        var taken = Rows.Select(r => r.Value).ToHashSet();
        var v = MinValue;
        while (taken.Contains(v)) v++;
        if (v > MaxValue) return;
        Rows.Add(new ScaleAnchorRowViewModel(this, v, string.Empty));
    }

    [RelayCommand]
    private void RemoveRow(ScaleAnchorRowViewModel? row)
    {
        if (row is not null) Rows.Remove(row);
    }

    private bool CanFill => !IsReadOnly && MaxValue > MinValue && MaxValue - MinValue + 1 <= ScaleRules.TickLimit;

    [RelayCommand(CanExecute = nameof(CanFill))]
    private void FillEveryValue()
    {
        if (!CanFill) return;
        var taken = Rows.Select(r => r.Value).ToHashSet();
        for (var v = MinValue; v <= MaxValue; v++)
            if (!taken.Contains(v)) Rows.Add(new ScaleAnchorRowViewModel(this, v, string.Empty));
    }

    private void Refresh()
    {
        var errors = new List<string>();
        if (!IsReadOnly)
        {
            var values = Rows.Select(r => r.Value).ToList();
            var shape = ScaleRules.ShapeErrors(MinValue, MaxValue, values);
            if (string.IsNullOrWhiteSpace(Name)) errors.Add(Strings.Scale_Error_Name);
            if (Rows.Any(r => r.Label.Trim().Length > 100)) errors.Add(Strings.Scale_Error_LabelLength);
            errors.AddRange(shape.Select(c => Text(c)));
            if (_usage is { } u)
                errors.AddRange(ScaleRules.EditErrors(MinValue, MaxValue, Precision, u.RecordedMin, u.RecordedMax, u.RecordedHasDecimals)
                    .Select(c => Text(c, c == "narrow-min" ? u.RecordedMin : u.RecordedMax)));
            Preview = shape.Count == 0 ? ScaleCursorBase.BuildPoints(ToTemplate()) : [];
        }
        else
            Preview = ScaleCursorBase.BuildPoints(ToTemplate());
        Errors = errors;
        CanConfirm = !IsReadOnly && errors.Count == 0;
        OnPropertyChanged(nameof(Errors));
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(UsageText));
        FillEveryValueCommand.NotifyCanExecuteChanged();
    }

    private static string Text(string code, decimal? recorded = null) => code switch
    {
        "range" => Strings.Scale_Error_Range,
        "order" => Strings.Scale_Error_Order,
        "anchor-outside" => Strings.Scale_Error_AnchorOutside,
        "anchor-duplicate" => Strings.Scale_Error_AnchorDuplicate,
        "narrow-min" => string.Format(CultureInfo.CurrentCulture, Strings.Scale_Error_NarrowMin, (recorded ?? 0).ToString("0.##", CultureInfo.CurrentCulture)),
        "narrow-max" => string.Format(CultureInfo.CurrentCulture, Strings.Scale_Error_NarrowMax, (recorded ?? 0).ToString("0.##", CultureInfo.CurrentCulture)),
        "precision" => Strings.Scale_Error_Precision,
        _ => code,
    };

    /// <summary>Gli scaglioni in ordine di valore; una riga senza nome resta, senza etichetta: è una tacca.</summary>
    public ScaleDraft ToDraft() => new(Name.Trim(), MinValue, MaxValue, Precision,
        Rows.OrderBy(r => r.Value)
            .Select(r => new ScaleAnchorDraft(r.Value, string.IsNullOrWhiteSpace(r.Label) ? null : r.Label.Trim())).ToList());

    /// <summary>La scala com'è adesso, per l'anteprima e per il creatore (che la mette in staging). Mai ufficiale: i nomi sono quelli delle righe.</summary>
    public ScaleTemplate ToTemplate(Guid? id = null, string? code = null) => new()
    {
        Id = id ?? _source?.Id ?? Guid.NewGuid(),
        Code = code ?? _source?.Code ?? string.Empty,
        Name = Name.Trim(),
        MinValue = MinValue,
        MaxValue = MaxValue,
        Precision = Precision,
        IsOfficial = false,
        Anchors = ToDraft().Anchors.Select(a => new ScaleAnchor { Id = Guid.NewGuid(), Value = a.Value, Label = a.Label }).ToList(),
    };
}
