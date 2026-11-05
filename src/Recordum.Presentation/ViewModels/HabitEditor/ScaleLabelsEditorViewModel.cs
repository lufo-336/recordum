using System.Collections.ObjectModel;
using System.Globalization;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitEditor;

/// <summary>Una riga dell'elenco: il valore della scala a sinistra, la sua etichetta a destra.</summary>
public sealed partial class ScaleLabelRowViewModel(ScaleLabelsEditorViewModel owner, int value) : ObservableObject
{
    public ScaleLabelsEditorViewModel Owner { get; } = owner;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
    private int _value = value;

    /// <summary>Il nome accessibile del campo, col valore del gradino ("Etichetta per 3").</summary>
    public string AutomationName =>
#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for a UI string
        string.Format(CultureInfo.CurrentCulture, Strings.Editor_ScaleLabels_RowName, Value);
#pragma warning restore CA1863

    [ObservableProperty] private string _text = string.Empty;

    partial void OnTextChanged(string value) => Owner.RowEdited();
}

/// <summary>Le etichette di una scala come elenco di campi (spec §6.4, §12.7). <see cref="FixedCount"/> vero = una scala che
/// esiste: una riga per valore, dal minimo al massimo, niente aggiungi/togli. Falso = una scala nuova: righe da due in su,
/// il valore va da 1 a n.
/// <para><b>Trappola</b> (CLAUDE.md §8): ricostruire <see cref="Rows"/> è un caricamento, non una modifica — <c>_loading</c>
/// sospende <see cref="LabelsChanged"/> finché le righe non sono al loro posto, o chi salva scriverebbe a ogni apertura.</para></summary>
public sealed partial class ScaleLabelsEditorViewModel : ObservableObject
{
    public const int MinNewRows = 2;

    private bool _loading;

    public ScaleLabelsEditorViewModel() => Rows.CollectionChanged += OnRowsChanged;

    public ObservableCollection<ScaleLabelRowViewModel> Rows { get; } = [];

    [ObservableProperty] private bool _fixedCount;

    /// <summary>Si può togliere una riga: solo in una scala nuova e solo sopra il minimo di due.</summary>
    public bool CanRemove => !FixedCount && Rows.Count > MinNewRows;

    /// <summary>Un'etichetta per riga, in ordine. Scala esistente: i buchi restano (l'etichetta va per posizione) e tutte
    /// vuote = nessuna etichetta. Scala nuova: le righe vuote si scartano.</summary>
    public IReadOnlyList<string> Labels
    {
        get
        {
            var texts = Rows.Select(r => r.Text.Trim()).ToList();
            if (!FixedCount) return texts.Where(t => t.Length > 0).ToList();
            return texts.Any(t => t.Length > 0) ? texts : [];
        }
    }

    public event EventHandler? LabelsChanged;

    /// <summary>Scala esistente: una riga per valore da <paramref name="min"/> a <paramref name="max"/>, con le etichette
    /// date per posizione (le mancanti restano vuote). Non solleva <see cref="LabelsChanged"/>.</summary>
    public void LoadFixed(int min, int max, IReadOnlyList<string> labels)
    {
        Load(true, Enumerable.Range(0, Math.Max(0, max - min + 1)).Select(i => (min + i, i < labels.Count ? labels[i] : string.Empty)));
    }

    /// <summary>Scala nuova: le etichette date, e comunque almeno due righe. Non solleva <see cref="LabelsChanged"/>.</summary>
    public void LoadDraft(IReadOnlyList<string>? labels = null)
    {
        labels ??= [];
        Load(false, Enumerable.Range(0, Math.Max(MinNewRows, labels.Count)).Select(i => (1 + i, i < labels.Count ? labels[i] : string.Empty)));
    }

    private void Load(bool fixedCount, IEnumerable<(int Value, string Text)> rows)
    {
        _loading = true;
        try
        {
            FixedCount = fixedCount;
            Rows.Clear();
            foreach (var (value, text) in rows) Rows.Add(new ScaleLabelRowViewModel(this, value) { Text = text });
        }
        finally { _loading = false; }
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(Labels));
    }

    [RelayCommand]
    private void AddRow()
    {
        if (FixedCount) return;
        Rows.Add(new ScaleLabelRowViewModel(this, Rows.Count + 1));
    }

    [RelayCommand]
    private void RemoveRow(ScaleLabelRowViewModel? row)
    {
        if (!CanRemove || row is null || !Rows.Remove(row)) return;
        // I valori restano 1..n: togliere la seconda riga fa scalare le successive.
        for (var i = 0; i < Rows.Count; i++) Rows[i].Value = i + 1;
    }

    internal void RowEdited() => Raise();

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_loading) return;
        OnPropertyChanged(nameof(CanRemove));
        Raise();
    }

    private void Raise()
    {
        if (_loading) return;
        OnPropertyChanged(nameof(Labels));
        LabelsChanged?.Invoke(this, EventArgs.Empty);
    }
}
