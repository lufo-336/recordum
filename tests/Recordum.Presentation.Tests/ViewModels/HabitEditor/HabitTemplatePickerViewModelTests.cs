using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.SeedData;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.Resources;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

public class HabitTemplatePickerViewModelTests
{
    private readonly FakeHabitTemplateService _templates = new();
    private readonly FakeHabitNodeService _nodes = new();

    private readonly FakeConfirmationDialog _confirm = new();

    private HabitTemplatePickerViewModel CreateSut() => new(_templates, _nodes, _confirm);

    [Fact]
    public async Task LoadAsync_separates_pinned_actions_from_official_templates()
    {
        for (var i = 0; i < 8; i++)
            await _templates.CreateAsync(new HabitTemplate { Name = $"t{i}", IsOfficial = true });
        var sut = CreateSut();

        await sut.LoadAsync();

        // The two primary actions live in their own pinned collection, out of the collapsible sections.
        sut.Actions.Select(a => a.Name).Should().Equal(
            Strings.HabitTemplatePicker_FromScratch, Strings.HabitTemplatePicker_BlankGroup);
        sut.Actions.Should().OnlyContain(a => a.TemplateId == null);

        // Official templates only — no action entries mixed in.
        sut.OfficialTemplates.Should().HaveCount(8);
        sut.OfficialTemplates.Should().OnlyContain(o => o.TemplateId != null && !o.IsCustom);
        sut.CustomOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_splits_custom_templates_into_their_own_section()
    {
        await _templates.CreateAsync(new HabitTemplate { Name = "official", IsOfficial = true });
        await _templates.CreateAsync(new HabitTemplate { Name = "mine", IsOfficial = false });
        var sut = CreateSut();

        await sut.LoadAsync();

        sut.OfficialTemplates.Select(o => o.Name).Should().Contain("official").And.NotContain("mine");
        sut.CustomOptions.Should().ContainSingle(o => o.Name == "mine" && o.IsCustom);
    }

    [Fact]
    public async Task RenameCustom_renames_template_and_reloads()
    {
        var id = await _templates.CreateAsync(new HabitTemplate { Name = "old", IsOfficial = false });
        var sut = CreateSut();
        await sut.LoadAsync();
        var option = sut.CustomOptions.Single();

        await sut.RenameCustomCommand.ExecuteAsync((option, "new"));

        _templates.Store[id].Name.Should().Be("new");
        sut.CustomOptions.Should().ContainSingle(o => o.Name == "new");
    }

    [Fact]
    public async Task DeleteCustom_deletes_template_and_reloads()
    {
        var id = await _templates.CreateAsync(new HabitTemplate { Name = "doomed", IsOfficial = false });
        var sut = CreateSut();
        await sut.LoadAsync();
        var option = sut.CustomOptions.Single();

        await sut.DeleteCustomCommand.ExecuteAsync(option);

        _confirm.Calls.Should().Be(1);
        _templates.Store.Should().NotContainKey(id);
        sut.CustomOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task PickCommand_for_template_calls_InstantiateAsync_with_parentId()
    {
        var parentId = Guid.NewGuid();
        var tId = await _templates.CreateAsync(new HabitTemplate { Name = "x", IsOfficial = true });
        var sut = CreateSut();
        await sut.LoadAsync(parentId: parentId);

        var option = sut.OfficialTemplates.Single(o => o.TemplateId == tId);
        await sut.PickCommand.ExecuteAsync(option);

        sut.CreatedHabitNodeId.Should().NotBeNull();
        _templates.InstantiateCalls.Should().ContainSingle().Which.Should().Match<(Guid t, Guid? p, Guid n)>(
            x => x.t == tId && x.p == parentId);
    }

    [Fact]
    public async Task PickCommand_for_blank_habit_requests_builder_instead_of_creating_node()
    {
        var parentId = Guid.NewGuid();
        var sut = CreateSut();
        await sut.LoadAsync(parentId: parentId);

        var blank = sut.Actions.Single(o => o.Name == Strings.HabitTemplatePicker_FromScratch);
        await sut.PickCommand.ExecuteAsync(blank);

        // "Build from scratch" no longer creates a node directly; it signals the host to open the builder.
        sut.OpenBuilderRequested.Should().BeTrue();
        sut.ParentForBuilder.Should().Be(parentId);
        sut.CreatedHabitNodeId.Should().BeNull();
        _nodes.Store.Should().BeEmpty();
    }

    [Fact]
    public async Task PickCommand_for_blank_group_creates_group_node()
    {
        var sut = CreateSut();
        await sut.LoadAsync();

        var blank = sut.Actions.Single(o => o.Name == Strings.HabitTemplatePicker_BlankGroup);
        await sut.PickCommand.ExecuteAsync(blank);

        _nodes.Store[sut.CreatedHabitNodeId!.Value].NodeType.Should().Be(NodeType.Group);
        _nodes.Store[sut.CreatedHabitNodeId.Value].HabitType.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCustom_asks_first_and_cancelling_keeps_the_template()
    {
        var id = await _templates.CreateAsync(new HabitTemplate { Name = "mine", IsOfficial = false });
        var sut = CreateSut();
        await sut.LoadAsync();
        _confirm.Result = false;

        await sut.DeleteCustomCommand.ExecuteAsync(sut.CustomOptions.Single());

        _confirm.Calls.Should().Be(1);
        _templates.Store.Should().ContainKey(id);
        sut.CustomOptions.Should().ContainSingle();
    }

    private sealed class GatedTemplates(FakeHabitTemplateService inner) : IHabitTemplateService
    {
        public TaskCompletionSource Gate { get; } = new();
        public int Instantiations;
        public async ValueTask<Guid> InstantiateAsync(Guid templateId, Guid? parentId, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Instantiations);
            await Gate.Task;
            return await inner.InstantiateAsync(templateId, parentId, ct);
        }
        public ValueTask<Guid> CreateAsync(HabitTemplate draft, CancellationToken ct = default) => inner.CreateAsync(draft, ct);
        public ValueTask UpdateAsync(Guid id, Action<HabitTemplate> mutate, CancellationToken ct = default) => inner.UpdateAsync(id, mutate, ct);
        public ValueTask DeleteAsync(Guid id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
        public ValueTask<HabitTemplate?> GetAsync(Guid id, CancellationToken ct = default) => inner.GetAsync(id, ct);
        public ValueTask<IReadOnlyList<HabitTemplate>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
    }

    [Fact]
    public async Task A_second_pick_while_the_first_is_in_flight_creates_nothing_more()
    {
        var gated = new GatedTemplates(_templates);
        await _templates.CreateAsync(new HabitTemplate { Name = "t", IsOfficial = true });
        var sut = new HabitTemplatePickerViewModel(gated, _nodes, _confirm);
        await sut.LoadAsync();
        var option = sut.OfficialTemplates.Single();

        var first = sut.PickCommand.ExecuteAsync(option);
        var second = sut.PickCommand.ExecuteAsync(option);
        gated.Gate.SetResult();
        await Task.WhenAll(first, second);

        gated.Instantiations.Should().Be(1);
        _templates.InstantiateCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_failed_pick_releases_the_guard_so_the_user_can_retry()
    {
        await _templates.CreateAsync(new HabitTemplate { Name = "t", IsOfficial = true });
        var sut = CreateSut();
        await sut.LoadAsync();
        var option = sut.OfficialTemplates.Single();

        _templates.NextException = new InvalidOperationException("boom");
        try { await sut.PickCommand.ExecuteAsync(option); } catch (InvalidOperationException) { }
        await sut.PickCommand.ExecuteAsync(option);

        sut.CreatedHabitNodeId.Should().NotBeNull();
        _templates.InstantiateCalls.Should().HaveCount(1);
    }
}

/// <summary>Il selettore dei modelli di spec §7.2 (3a, task 6): ricerca sul testo tradotto, il dettaglio della scheda scelta
/// letto da un creatore caricato col modello, "Usa questo modello" che crea passando dal creatore (una volta sola) e
/// "Personalizza…" che lo chiede aperto. Cultura fissata: i testi dei modelli ufficiali sono tradotti per Id.</summary>
[Collection(ProcessGlobalCultureCollection.Name)]
public sealed class HabitTemplatePickerTemplateTests : IDisposable
{
    private readonly CultureInfo? _strings = Strings.Culture;
    private readonly CultureInfo _current = CultureInfo.CurrentCulture;
    private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;
    private readonly FakeHabitTemplateService _templates = new();
    private readonly GatedComposed _composed = new();
    private readonly FakeAppSettingService _settings = new();

    public HabitTemplatePickerTemplateTests()
    {
        foreach (var t in HabitTemplateSeed.All) _templates.Store[t.Id] = t;
    }

    public void Dispose()
    {
        Strings.Culture = _strings;
        CultureInfo.CurrentCulture = _current;
        CultureInfo.CurrentUICulture = _ui;
    }

    private static void Pin(string name)
    {
        var c = CultureInfo.GetCultureInfo(name);
        Strings.Culture = c;
        CultureInfo.CurrentCulture = c;
        CultureInfo.CurrentUICulture = c;
    }

    private static readonly Guid Sleep = Guid.Parse("00000003-0000-0000-0000-000000000005");
    private static readonly Guid FoodDiary = Guid.Parse("00000003-0000-0000-0000-000000000008");

    private HabitBuilderViewModel NewBuilder(IHabitTemplateService? templates = null) =>
        new(_composed, templates ?? _templates, new SeedReferenceData(), new FakeClock(), new RecentColorStore(_settings),
            new FavoriteColorStore(_settings), new FakeScreenColorPicker());

    private async Task<HabitTemplatePickerViewModel> LoadedAsync(Guid? parentId = null, IHabitTemplateService? detailTemplates = null)
    {
        var sut = new HabitTemplatePickerViewModel(_templates, new FakeHabitNodeService(), new FakeConfirmationDialog(),
            () => NewBuilder(detailTemplates));
        await sut.LoadAsync(parentId);
        return sut;
    }

    private static PickerOption Card(HabitTemplatePickerViewModel sut, Guid id) =>
        sut.VisibleOfficial.Concat(sut.VisibleCustom).Single(o => o.TemplateId == id);

    [Fact]
    public async Task Search_ignores_case_and_accents_on_translated_text()
    {
        Pin("it-IT");
        var sut = await LoadedAsync();

        sut.SearchText = "diario";
        sut.VisibleOfficial.Select(o => o.Name).Should().Contain("Diario del cibo").And.NotContain("Monitor del sonno");

        sut.SearchText = "umóre";
        sut.VisibleOfficial.Select(o => o.Name).Should().Equal("Monitor dell'umore");
        sut.NoResults.Should().BeFalse();
    }

    [Fact]
    public async Task Empty_search_result_says_so()
    {
        Pin("en-US");
        var sut = await LoadedAsync();
        sut.NoResults.Should().BeFalse("senza ricerca si vedono tutti");

        sut.SearchText = "zzzz";
        sut.VisibleOfficial.Should().BeEmpty();
        sut.VisibleCustom.Should().BeEmpty();
        sut.NoResults.Should().BeTrue();

        sut.SearchText = "";
        sut.VisibleOfficial.Should().HaveCount(8);
        sut.NoResults.Should().BeFalse();
    }

    [Fact]
    public async Task A_selected_card_hidden_by_the_search_is_deselected()
    {
        Pin("en-US");
        var sut = await LoadedAsync();
        sut.Selected = Card(sut, Sleep);

        sut.SearchText = "zzzz";

        sut.Selected.Should().BeNull("una scheda che non si vede non si usa");
        sut.UseSelectedCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Cards_carry_the_translated_name_the_colour_and_the_type_line()
    {
        Pin("it-IT");
        var sut = await LoadedAsync();

        var food = Card(sut, FoodDiary);
        food.Name.Should().Be("Diario del cibo");
        food.Description.Should().StartWith("Annota ogni pasto");
        food.Color.Should().Be("#e67e22");
        food.TypeLine.Should().Be("diario · più volte al giorno");
        Card(sut, Sleep).TypeLine.Should().Be("contatore · più volte al giorno");
        Card(sut, Guid.Parse("00000003-0000-0000-0000-000000000006")).TypeLine.Should().Be("astinenza");
    }

    /// <summary>ListAsync ordina per il nome seminato (inglese): in italiano le schede vanno in ordine col nome che si vede.</summary>
    [Fact]
    public async Task Official_cards_are_sorted_by_their_translated_name()
    {
        Pin("it-IT");
        var sut = await LoadedAsync();

        sut.OfficialTemplates.Select(o => o.Name).Should().BeInAscendingOrder(StringComparer.Create(CultureInfo.CurrentCulture, true));
        sut.VisibleOfficial.Select(o => o.Name).Should().Equal(sut.OfficialTemplates.Select(o => o.Name));
    }

    [Fact]
    public async Task Use_creates_once_even_on_double_click()
    {
        Pin("en-US");
        var sut = await LoadedAsync();
        sut.Selected = Card(sut, Sleep);
        _composed.Gate = new TaskCompletionSource();

        var first = sut.UseSelectedCommand.ExecuteAsync(null);
        var second = sut.UseSelectedCommand.ExecuteAsync(null);
        _composed.Gate.SetResult();
        await Task.WhenAll(first, second);

        _composed.Requests.Should().ContainSingle();
        sut.CreatedHabitNodeId.Should().Be(_composed.Requests[0].Node.Id);
    }

    [Fact]
    public async Task Use_creates_the_translated_habit()
    {
        Pin("it-IT");
        var parent = Guid.NewGuid();
        var sut = await LoadedAsync(parent);
        sut.Selected = Card(sut, Sleep);

        await sut.UseSelectedCommand.ExecuteAsync(null);

        var request = _composed.Requests.Should().ContainSingle().Subject;
        request.Node.Name.Should().Be("Monitor del sonno");
        request.Node.ParentId.Should().Be(parent);
        request.Metrics.Select(m => m.Name).Should().Equal("Ore dormite", "Qualità del sonno");
        sut.CreatedHabitNodeId.Should().Be(request.Node.Id);
        sut.CustomizeTemplateRequested.Should().BeNull();
    }

    [Fact]
    public async Task Customize_requests_the_builder_with_the_template()
    {
        Pin("en-US");
        var sut = await LoadedAsync();
        sut.CustomizeSelectedCommand.CanExecute(null).Should().BeFalse("senza una scheda scelta");
        sut.Selected = Card(sut, Sleep);

        sut.CustomizeSelectedCommand.Execute(null);

        sut.CustomizeTemplateRequested.Should().Be(Sleep);
        sut.CreatedHabitNodeId.Should().BeNull();
        _composed.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Use_on_a_template_the_creator_cannot_launch_as_is_opens_the_creator_instead()
    {
        Pin("en-US");
        // Una scala che il catalogo non ha e che il modello non definisce: il creatore si carica con il campo incompleto.
        var id = await _templates.CreateAsync(new HabitTemplate
        {
            Name = "Broken", IsOfficial = false,
            PayloadJson = "{\"node\":{\"name\":\"Broken\",\"habit_type\":\"State\"},\"metrics\":[{\"name\":\"m\",\"type\":\"Integer\",\"scale_code\":\"nope\"}]}",
        });
        var sut = await LoadedAsync();
        sut.Selected = Card(sut, id);

        await sut.UseSelectedCommand.ExecuteAsync(null);

        _composed.Requests.Should().BeEmpty("non si crea un'abitudine malformata");
        sut.CreatedHabitNodeId.Should().BeNull();
        sut.CustomizeTemplateRequested.Should().Be(id, "il creatore si apre caricato e dice perche'");
    }

    [Fact]
    public async Task Detail_lists_what_the_template_records()
    {
        Pin("it-IT");
        var sut = await LoadedAsync();

        sut.Selected = Card(sut, Sleep);
        await sut.DetailLoad;

        var detail = sut.Detail!;
        detail.Name.Should().Be("Monitor del sonno");
        detail.HomeRow.Name.Should().Be("Monitor del sonno");
        detail.TypeLine.Should().Be("contatore · più volte al giorno");
        var values = detail.Contents.Select(r => r.Value).ToList();
        values.Should().Contain(v => v.Contains("Ore dormite (h)"));
        values.Should().Contain(v => v.Contains("Qualità del sonno"));
        values.Should().Contain("più volte al giorno: Notte, Pisolino");
    }

    [Fact]
    public async Task A_fast_selection_change_never_shows_the_previous_cards_detail()
    {
        Pin("en-US");
        var gated = new GatedGet(_templates, Sleep);
        var sut = await LoadedAsync(detailTemplates: gated);

        sut.Selected = Card(sut, Sleep);          // resta appeso sul modello
        var slow = sut.DetailLoad;
        sut.Selected = Card(sut, FoodDiary);
        await sut.DetailLoad;
        gated.Gate.SetResult();
        await slow;

        sut.Detail!.Name.Should().Be(Card(sut, FoodDiary).Name);
    }

    [Fact]
    public async Task Changing_card_keeps_the_previous_detail_until_the_new_one_is_ready()
    {
        Pin("en-US");
        var gated = new GatedGet(_templates, Sleep);
        var sut = await LoadedAsync(detailTemplates: gated);
        sut.Selected = Card(sut, FoodDiary);
        await sut.DetailLoad;

        sut.Selected = Card(sut, Sleep);          // resta appeso sul modello

        sut.Detail!.Name.Should().Be(Card(sut, FoodDiary).Name, "il riquadro non lampeggia sul segnaposto fra due schede");
        gated.Gate.SetResult();
        await sut.DetailLoad;
        sut.Detail!.Name.Should().Be(Card(sut, Sleep).Name);
    }

    [Fact]
    public async Task A_detail_that_fails_to_load_says_so_instead_of_inviting_a_choice()
    {
        Pin("it-IT");
        var gated = new GatedGet(_templates, Sleep) { Fault = new InvalidOperationException("disco") };
        var sut = await LoadedAsync(detailTemplates: gated);
        sut.Selected = Card(sut, FoodDiary);
        await sut.DetailLoad;

        sut.Selected = Card(sut, Sleep);
        await sut.DetailLoad;                     // osservato: il caricamento non esce guasto

        sut.Detail.Should().BeNull("il dettaglio della scheda di prima non resta sotto il nome di questa");
        sut.DetailFailed.Should().BeTrue();
        sut.DetailPlaceholder.Should().Be(Strings.HabitTemplatePicker_DetailFailed)
            .And.NotBe(Strings.HabitTemplatePicker_DetailEmpty);

        sut.Selected = Card(sut, FoodDiary);
        await sut.DetailLoad;
        sut.DetailFailed.Should().BeFalse("una scheda che si carica torna normale");
        sut.Detail.Should().NotBeNull();
    }

    [Fact]
    public async Task After_a_failed_detail_the_next_card_shows_the_normal_placeholder_while_it_loads()
    {
        Pin("it-IT");
        var gated = new GatedGet(_templates, Sleep) { Fault = new InvalidOperationException("disco"), FaultId = FoodDiary };
        var sut = await LoadedAsync(detailTemplates: gated);
        sut.Selected = Card(sut, FoodDiary);
        await sut.DetailLoad;
        sut.DetailFailed.Should().BeTrue();

        sut.Selected = Card(sut, Sleep);          // resta appeso sul modello

        sut.DetailFailed.Should().BeFalse("la scheda nuova si sta caricando, non e' fallita");
        sut.DetailPlaceholder.Should().Be(Strings.HabitTemplatePicker_DetailEmpty);
        gated.Gate.SetResult();
        await sut.DetailLoad;
        sut.Detail!.Name.Should().Be(Card(sut, Sleep).Name);
    }

    private sealed class GatedComposed : IComposedHabitService
    {
        public TaskCompletionSource? Gate { get; set; }
        public List<ComposedHabitRequest> Requests { get; } = [];
        public async ValueTask<Guid> CreateComposedHabitAsync(ComposedHabitRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            if (Gate is { } gate) await gate.Task;
            return request.Node.Id;
        }
    }

    private sealed class GatedGet(FakeHabitTemplateService inner, Guid gatedId) : IHabitTemplateService
    {
        public TaskCompletionSource Gate { get; } = new();
        public Exception? Fault { get; init; }
        /// <summary>Il modello che fallisce; senza, quello appeso.</summary>
        public Guid? FaultId { get; init; }
        public async ValueTask<HabitTemplate?> GetAsync(Guid id, CancellationToken ct = default)
        {
            if (id == (FaultId ?? gatedId) && Fault is { } fault) throw fault;
            if (id == gatedId) await Gate.Task;
            return await inner.GetAsync(id, ct);
        }
        public ValueTask<Guid> CreateAsync(HabitTemplate draft, CancellationToken ct = default) => inner.CreateAsync(draft, ct);
        public ValueTask UpdateAsync(Guid id, Action<HabitTemplate> mutate, CancellationToken ct = default) => inner.UpdateAsync(id, mutate, ct);
        public ValueTask DeleteAsync(Guid id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
        public ValueTask<IReadOnlyList<HabitTemplate>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
        public ValueTask<Guid> InstantiateAsync(Guid templateId, Guid? parentId, CancellationToken ct = default) => inner.InstantiateAsync(templateId, parentId, ct);
    }

    private sealed class SeedReferenceData : IReferenceDataQueryService
    {
        public ValueTask<IReadOnlyList<UnitOfMeasure>> ListUnitsAsync(CancellationToken ct = default) => new(UnitOfMeasureSeed.All);
        public ValueTask<IReadOnlyList<ScaleTemplate>> ListScalesAsync(CancellationToken ct = default) => new(ScaleTemplateSeed.All);
    }
}
