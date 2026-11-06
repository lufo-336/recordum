using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Errors;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.Templates;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.ViewModels.HabitBuilder.Preview;

namespace Recordum.Presentation.ViewModels.HabitEditor;

/// <summary>
/// Modal picker shown by the Home "+ New" button. Lists official templates plus two
/// blank options (blank habit / blank group). Picking an option instantiates the entity
/// (cloning the template via <see cref="IHabitTemplateService.InstantiateAsync"/> or creating an
/// empty node) and surfaces the new id via <see cref="CreatedHabitNodeId"/> for the modal host
/// to close itself and load it into the editor.
/// <para>Il selettore di spec §7.2 (Avalonia) aggiunge la ricerca, la scheda scelta col suo dettaglio, "Usa questo modello" e
/// "Personalizza…": entrambi passano dal creatore (<paramref name="builders"/>, dalla DI), cosi' un modello crea la stessa
/// abitudine che il creatore comporrebbe (principio 8) e i testi ufficiali si traducono in un punto solo. WPF usa
/// <see cref="PickAsync"/> com'era.</para>
/// </summary>
public sealed partial class HabitTemplatePickerViewModel(
    IHabitTemplateService templates,
    IHabitNodeService nodes,
    IConfirmationDialog confirm,
    Func<HabitBuilderViewModel>? builders = null) : ObservableObject
{
    private readonly IHabitTemplateService _templates = templates;
    private readonly IHabitNodeService _nodes = nodes;
    private readonly Func<HabitBuilderViewModel>? _builders = builders;

    /// <summary>Vero quando la DI ha dato il creatore: senza, "Usa" ripiega su <c>InstantiateAsync</c> e il dettaglio resta vuoto.</summary>
    public bool HasBuilderFactory => _builders is not null;

    /// <summary>The two primary create actions ("build from scratch", "blank group"), pinned at the top
    /// of the picker outside the collapsible template sections.</summary>
    public ObservableCollection<PickerOption> Actions { get; } = new();

    /// <summary>Official/built-in templates (<see cref="HabitTemplate.IsOfficial"/> == true), shown in the
    /// collapsible "Predefined templates" section.</summary>
    public ObservableCollection<PickerOption> OfficialTemplates { get; } = new();

    /// <summary>User-saved templates (<see cref="HabitTemplate.IsOfficial"/> == false), shown in a
    /// separate collapsible "Your templates" section with rename/delete affordances.</summary>
    public ObservableCollection<PickerOption> CustomOptions { get; } = new();

    [ObservableProperty] private Guid? _createdHabitNodeId;

    /// <summary>Set when the "build from scratch" option is picked: the host closes the picker
    /// and opens the block-first builder instead of creating a node directly.</summary>
    [ObservableProperty] private bool _openBuilderRequested;

    /// <summary>Parent for the node the builder will create (carried from <see cref="LoadAsync"/>).</summary>
    public Guid? ParentForBuilder { get; private set; }

    public async Task LoadAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        ParentForBuilder = parentId;
        Actions.Clear();
        OfficialTemplates.Clear();
        CustomOptions.Clear();
        Actions.Add(new PickerOption(Strings.HabitTemplatePicker_FromScratch, Strings.HabitTemplatePicker_BlankHabit_Description, null, NodeType.Habit, parentId));
        Actions.Add(new PickerOption(Strings.HabitTemplatePicker_BlankGroup, Strings.HabitTemplatePicker_BlankGroup_Description, null, NodeType.Group, parentId));
        foreach (var t in await _templates.ListAsync(ct))
        {
            var (color, type, repeatable, gesture) = HabitBuilderViewModel.DescribeTemplate(t.PayloadJson);
            var option = new PickerOption(OfficialTexts.TemplateName(t), OfficialTexts.TemplateDescription(t) ?? "", t.Id,
                NodeType.Habit, parentId, !t.IsOfficial,
                string.IsNullOrWhiteSpace(color) ? PickerOption.NeutralColor : color, TypeLine(type, repeatable, gesture));
            if (t.IsOfficial)
                OfficialTemplates.Add(option);
            else
                CustomOptions.Add(option);
        }
        ApplyFilter();
    }

    // ===== Ricerca =====

    public ObservableCollection<PickerOption> VisibleOfficial { get; } = new();
    public ObservableCollection<PickerOption> VisibleCustom { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;

    /// <summary>Una ricerca che non trova niente, ne' fra i modelli ne' fra i tuoi.</summary>
    [ObservableProperty] private bool _noResults;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        Sync(VisibleOfficial, OfficialTemplates.Where(o => Matches(o, query)));
        Sync(VisibleCustom, CustomOptions.Where(o => Matches(o, query)));
        NoResults = query.Length > 0 && VisibleOfficial.Count == 0 && VisibleCustom.Count == 0;
        // Una scheda che non si vede non si usa; ricaricata (rinominata) si ritrova per Id.
        Selected = Selected?.TemplateId is { } id
            ? VisibleOfficial.Concat(VisibleCustom).FirstOrDefault(o => o.TemplateId == id)
            : null;
    }

    // Sul testo che l'utente vede (tradotto), senza maiuscole ne' accenti: "umóre" trova "Umore".
    private static bool Matches(PickerOption o, string query)
    {
        if (query.Length == 0) return true;
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return compare.IndexOf(o.Name, query, options) >= 0 || compare.IndexOf(o.Description, query, options) >= 0;
    }

    // Toglie e inserisce senza svuotare: la scheda scelta, se resta visibile, non esce mai dalla lista (e la lista non perde
    // la selezione a ogni tasto). Le due sequenze seguono lo stesso ordine, quindi basta inserire al proprio indice.
    private static void Sync(ObservableCollection<PickerOption> target, IEnumerable<PickerOption> wanted)
    {
        var list = wanted.ToList();
        for (var i = target.Count - 1; i >= 0; i--)
            if (!list.Contains(target[i])) target.RemoveAt(i);
        for (var i = 0; i < list.Count; i++)
            if (i >= target.Count || !Equals(target[i], list[i])) target.Insert(i, list[i]);
    }

    // ===== Scheda scelta e dettaglio =====

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UseSelectedCommand), nameof(CustomizeSelectedCommand))]
    private PickerOption? _selected;

    /// <summary>Il dettaglio della scheda scelta, letto da un creatore caricato col modello (la stessa fonte di "Usa"). Mentre
    /// si carica quello di una nuova scheda resta il precedente (niente lampo del segnaposto); null senza scheda o se non si
    /// e' potuto leggere.</summary>
    [ObservableProperty] private TemplateDetailViewModel? _detail;

    /// <summary>Il dettaglio della scheda scelta non si e' potuto leggere: il riquadro lo dice, invece di invitare a scegliere.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailPlaceholder))]
    private bool _detailFailed;

    /// <summary>Il testo del riquadro senza dettaglio.</summary>
    public string DetailPlaceholder => DetailFailed ? Strings.HabitTemplatePicker_DetailFailed : Strings.HabitTemplatePicker_DetailEmpty;

    /// <summary>Il caricamento del dettaglio in corso (i test lo aspettano).</summary>
    internal Task DetailLoad { get; private set; } = Task.CompletedTask;

    private int _detailGeneration;

    partial void OnSelectedChanged(PickerOption? value)
    {
        // La generazione: un cambio di scheda rapido non deve mai mostrare il dettaglio della scheda di prima.
        var generation = ++_detailGeneration;
        if (value?.TemplateId is { } id && _builders is not null)
        {
            DetailLoad = LoadDetailAsync(value, id, generation);   // il dettaglio di prima resta finche' arriva il nuovo
            return;
        }
        Detail = null;
        DetailFailed = false;
        DetailLoad = Task.CompletedTask;
    }

    private async Task LoadDetailAsync(PickerOption option, Guid templateId, int generation)
    {
        TemplateDetailViewModel? detail = null;
        var failed = false;
        try
        {
            var builder = await LoadedBuilderAsync(templateId).ConfigureAwait(true);
            detail = new TemplateDetailViewModel(option.Name, option.Description, builder.Preview!.HomeRow,
                TypeLine(builder.DerivedType, builder.Repeatable, builder.JournalGesture), Contents(builder));
        }
        catch (HabitTemplateNotFoundException) { }   // eliminato nel frattempo: niente dettaglio
        catch (Exception ex)
        {
            // Nessuno aspetta questo Task: un guasto non osservato sparirebbe e il riquadro inviterebbe ancora a scegliere.
            System.Diagnostics.Debug.WriteLine($"Template detail load failed: {ex}");
            failed = true;
        }
        if (generation != _detailGeneration) return;
        Detail = detail;
        DetailFailed = failed;
    }

    private async Task<HabitBuilderViewModel> LoadedBuilderAsync(Guid templateId)
    {
        var builder = _builders!();
        builder.SetParent(ParentForBuilder);
        await builder.InitializeAsync().ConfigureAwait(true);
        await builder.LoadTemplateAsync(templateId).ConfigureAwait(true);
        return builder;
    }

    /// <summary>"journal · più volte al giorno": la parola del tipo come in Home e, per contatore e diario, il riassunto della
    /// Registrazione di Configura (il diario a note veloci si registra piu' volte al giorno, quello a voce una).</summary>
    internal static string TypeLine(HabitType type, bool repeatable, JournalPrimaryGesture gesture) =>
        Times(type, repeatable, gesture) is { } times
            ? $"{HabitBuilderViewModel.TypeWord(type)} · {times}"
            : HabitBuilderViewModel.TypeWord(type);

    private static string? Times(HabitType type, bool repeatable, JournalPrimaryGesture gesture)
    {
        bool? several = type switch
        {
            HabitType.Counter => repeatable,
            HabitType.Journal => gesture == JournalPrimaryGesture.QuickNote,
            _ => null,
        };
        return several is { } many
            ? LowerFirst(many ? Strings.Editor_Summary_MultiplePerDay : Strings.Editor_Summary_OncePerDay)
            : null;
    }

    // Il riassunto di Configura e' una frase ("Più volte al giorno"); qui sta a meta' riga.
    private static string LowerFirst(string s) =>
        s.Length == 0 ? s : string.Concat(char.ToLower(s[0], CultureInfo.CurrentCulture).ToString(), s[1..]);

    /// <summary>Le righe di "Cosa contiene": cosa registri (i campi con unita' o scala), quante volte (con i nomi delle
    /// serie), il gesto principale del diario. Le parole sono quelle dei blocchi del creatore e di Configura.</summary>
    private static List<TemplateDetailRow> Contents(HabitBuilderViewModel b)
    {
        var rows = new List<TemplateDetailRow>();
        var what = b.CountsOccurrences ? Strings.HabitBuilder_CounterBlock_Help
            : b.Abstinence ? Strings.HabitBuilder_AbstinenceBlock_Help
            : string.Join(", ", b.Fields.Select(f => FieldText(b, f)));
        if (what.Length > 0) rows.Add(new(Strings.Editor_Block_Record, what));
        if (Times(b.DerivedType, b.Repeatable, b.JournalGesture) is { } times)
        {
            var labels = b.Repeatable && b.DerivedType == HabitType.Counter
                ? b.SeriesLabels.Select(l => l.Name.Trim()).Where(n => n.Length > 0).ToList()
                : [];
            rows.Add(new(Strings.Editor_Section_Registration, labels.Count > 0 ? $"{times}: {string.Join(", ", labels)}" : times));
        }
        if (b.DerivedType == HabitType.Journal)
            rows.Add(new(Strings.Editor_Registration_JournalGesture,
                b.JournalGestureOptions.First(o => o.Gesture == b.JournalGesture).Label));
        return rows;
    }

    private static string FieldText(HabitBuilderViewModel b, FieldBlockViewModel f)
    {
        var suffix = f.Nature == FieldNature.Scale
            ? b.AvailableScales.FirstOrDefault(s => s.Id == f.ScaleTemplateId) is { } scale ? $"{scale.MinValue}–{scale.MaxValue}" : null
            : b.AvailableUnits.FirstOrDefault(u => u.Id == f.UnitOfMeasureId) is { } unit
                ? string.IsNullOrWhiteSpace(unit.Symbol) ? OfficialTexts.UnitName(unit) : unit.Symbol
                : null;
        return suffix is null ? f.Name : $"{f.Name} ({suffix})";
    }

    // ===== Usa e Personalizza =====

    /// <summary>Set by "Personalizza…" (and by "Usa" on a template the creator cannot launch as is): the host closes the
    /// picker and opens the creator filled from this template, with <see cref="ParentForBuilder"/>.</summary>
    [ObservableProperty] private Guid? _customizeTemplateRequested;

    private bool HasSelectedTemplate() => Selected?.TemplateId is not null;

    /// <summary>"Usa questo modello": crea l'abitudine con un creatore nuovo caricato dal modello, senza mostrarlo. Se il creatore
    /// non la puo' creare cosi' com'e' (un'unita' o una scala che non c'e', una finestra mobile senza giorni), non tace:
    /// lo chiede aperto, come "Personalizza…", e la sua riga del perche' lo spiega.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedTemplate))]
    private async Task UseSelectedAsync()
    {
        // ExecuteAsync non consulta CanExecute e un doppio clic (o Invio ripetuto) crea due volte: la guardia sta qui.
        if (Selected is not { TemplateId: { } id } option || _picking || CreatedHabitNodeId is not null
            || CustomizeTemplateRequested is not null) return;
        _picking = true;
        try
        {
            if (_builders is null) { await PickCoreAsync(option); return; }
            var builder = await LoadedBuilderAsync(id).ConfigureAwait(true);
            if (!builder.CanLaunch) { CustomizeTemplateRequested = id; return; }
            await builder.LaunchCommand.ExecuteAsync(null).ConfigureAwait(true);
            CreatedHabitNodeId = builder.CreatedHabitNodeId;
        }
        finally { _picking = false; }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedTemplate))]
    private void CustomizeSelected()
    {
        if (Selected?.TemplateId is not { } id || _picking || CreatedHabitNodeId is not null) return;
        CustomizeTemplateRequested = id;
    }

    /// <summary>Renames a custom template, then reloads so both sections reflect the change.</summary>
    [RelayCommand]
    private async Task RenameCustomAsync((PickerOption Option, string NewName) args)
    {
        if (args.Option?.TemplateId is not { } id || !args.Option.IsCustom) return;
        if (string.IsNullOrWhiteSpace(args.NewName)) return;
        var name = args.NewName.Trim();
        await _templates.UpdateAsync(id, t => t.Name = name);
        await LoadAsync(ParentForBuilder);
    }

    /// <summary>Deletes a custom template, then reloads the "Your templates" section.</summary>
    [RelayCommand]
    private async Task DeleteCustomAsync(PickerOption option)
    {
        if (option?.TemplateId is not { } id || !option.IsCustom) return;
#pragma warning disable CA1863 // CompositeFormat caching not worth it for a once-per-click UI string
        var message = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.HabitTemplatePicker_DeleteConfirm_Message, option.Name);
#pragma warning restore CA1863
        if (!await confirm.ConfirmAsync(Strings.HabitTemplatePicker_DeleteConfirm_Title, message)) return;
        await _templates.DeleteAsync(id);
        await LoadAsync(ParentForBuilder);
    }

    [RelayCommand]
    private async Task PickAsync(PickerOption option)
    {
        // ExecuteAsync non consulta CanExecute e un doppio clic crea due volte: la guardia sta qui.
        if (option is null || _picking || CreatedHabitNodeId is not null) return;
        _picking = true;
        try { await PickCoreAsync(option); }
        finally { _picking = false; }
    }

    private bool _picking;

    private async Task PickCoreAsync(PickerOption option)
    {
        if (option.TemplateId is { } tid)
        {
            CreatedHabitNodeId = await _templates.InstantiateAsync(tid, option.ParentId);
            return;
        }
        if (option.NodeType == NodeType.Habit)
        {
            // "Build from scratch": don't create a node here — signal the host to open the builder.
            ParentForBuilder = option.ParentId;
            OpenBuilderRequested = true;
            return;
        }
        var draft = new HabitNode
        {
            ParentId = option.ParentId,
            NodeType = option.NodeType,
            HabitType = option.NodeType == NodeType.Habit ? HabitType.Action : null,
            Name = option.NodeType == NodeType.Habit ? Strings.Editor_Default_NewHabit : Strings.Editor_Default_NewGroup,
            Color = "#808080",
        };
        CreatedHabitNodeId = await _nodes.CreateAsync(draft);
    }
}

/// <summary>Una voce del selettore. <see cref="Color"/> e <see cref="TypeLine"/> servono alla scheda (§7.2: barra del colore,
/// "journal · più volte al giorno"); le voci senza modello li tengono al default.</summary>
public sealed record PickerOption(string Name, string Description, Guid? TemplateId, NodeType NodeType, Guid? ParentId,
    bool IsCustom = false, string Color = PickerOption.NeutralColor, string TypeLine = "")
{
    public const string NeutralColor = "#808080";
}

/// <summary>Il dettaglio della scheda scelta: come apparira' in Home, il tipo e cosa contiene.</summary>
public sealed record TemplateDetailViewModel(string Name, string Description, HomeRowPreviewModel HomeRow, string TypeLine,
    IReadOnlyList<TemplateDetailRow> Contents);

/// <summary>Una riga di "Cosa contiene": "Cosa registri · Ore dormite (h), Qualità del sonno (1–5)".</summary>
public sealed record TemplateDetailRow(string Label, string Value);
