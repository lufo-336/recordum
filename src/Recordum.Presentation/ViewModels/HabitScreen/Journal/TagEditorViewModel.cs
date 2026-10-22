using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Text;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitScreen.Journal;

/// <summary>La riga Tag di una voce: i tag scelti e, mentre si scrive, i suggerimenti. Non conosce servizi: i nomi
/// esistenti glieli dà chi la ospita (<see cref="KnownNames"/>).</summary>
public sealed partial class TagEditorViewModel : ObservableObject
{
    public const int MaxSuggestions = 8;

    private IReadOnlyList<string> _knownNames = [];

    public ObservableCollection<string> Tags { get; } = [];

    [ObservableProperty] private string _query = string.Empty;

    public IReadOnlyList<string> Suggestions { get; private set; } = [];

    public IReadOnlyList<string> KnownNames
    {
        get => _knownNames;
        set { _knownNames = value; Refresh(); }
    }

    public TagEditorViewModel()
    {
        Tags.CollectionChanged += (_, _) => Refresh();
    }

    partial void OnQueryChanged(string value) => Refresh();

    /// <summary>Aggiunge un tag. Accetta anche la voce "Crea «…»" dei suggerimenti (vale come il testo digitato).
    /// Un solo <c>#</c> iniziale si toglie; un nome che il testo non saprebbe rileggere come tag
    /// (vuoto, spazi, punteggiatura, solo cifre, oltre 64 caratteri) o già scelto: ignorato.</summary>
    [RelayCommand]
    public void Add(string? name)
    {
        if (name is not null && name == CreateLabel(Query.Trim())) name = Query;
        name = name?.Trim();
        if (name is ['#', ..]) name = name[1..];
        if (!InlineTags.IsValidName(name)) return;
        if (Tags.Contains(name, StringComparer.OrdinalIgnoreCase)) return;
        Tags.Add(name);
        Query = string.Empty;
    }

    [RelayCommand]
    public void Remove(string? name)
    {
        if (name is not null) Tags.Remove(name);
    }

    /// <summary>Nomi esistenti che iniziano per <paramref name="prefix"/> (senza maiuscole), in ordine di nome,
    /// esclusi <paramref name="excluded"/> e i nomi che non sono tag validi, al massimo <see cref="MaxSuggestions"/>.</summary>
    public static List<string> Match(IEnumerable<string> known, string prefix, IEnumerable<string> excluded)
    {
        var skip = excluded.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return known
            .Where(n => InlineTags.IsValidName(n) && n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !skip.Contains(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Take(MaxSuggestions)
            .ToList();
    }

#pragma warning disable CA1863 // etichetta breve costruita a ogni battuta, non vale una CompositeFormat
    public static string CreateLabel(string name) => string.Format(CultureInfo.CurrentCulture, Strings.Diario_Tag_Create, name);
#pragma warning restore CA1863

    private void Refresh()
    {
        var q = Query.Trim();
        var list = q.Length == 0 ? [] : Match(_knownNames, q, Tags);
        if (InlineTags.IsValidName(q)
            && !_knownNames.Contains(q, StringComparer.OrdinalIgnoreCase)
            && !Tags.Contains(q, StringComparer.OrdinalIgnoreCase))
            list.Add(CreateLabel(q));
        Suggestions = list;
        OnPropertyChanged(nameof(Suggestions));
    }
}
