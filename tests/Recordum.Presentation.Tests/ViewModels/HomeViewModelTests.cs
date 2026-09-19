using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Quick;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Events;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels;

public class HomeViewModelTests
{
    private readonly FakeHabitTreeQueryService _query = new();
    private readonly FakeHabitNodeService _nodes = new();
    private readonly FakePurgeService _purge = new();

    private HomeViewModel CreateSut() => new(_query, _nodes, _purge);

    [Fact]
    public async Task LoadAsync_populates_rows_for_Active_tab_excluding_archived()
    {
        _query.HomeRows.Add(SampleRow(name: "Live"));
        _query.HomeRows.Add(SampleRow(name: "Archived", archived: true));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.State.Should().Be(HomeState.Loaded);
        sut.Rows.Should().ContainSingle(r => r.Name == "Live");
    }

    [Fact]
    public async Task ApplyDropAsync_does_not_throw_when_the_dragged_node_vanished()
    {
        // CR16: a concurrent reload/cleanup can remove the dragged node or its target parent between
        // the drag and the drop. MoveAndReorderAsync then throws HabitNodeNotFound/AlreadySoftDeleted;
        // the async-void OnDrop must not let that crash the app — the drop is a no-op.
        _query.HomeRows.Add(SampleRow(name: "Run"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var src = sut.Rows[0];
        _nodes.NextException = new Recordum.Core.Errors.HabitNodeNotFoundException(System.Guid.NewGuid());

        var act = async () => await sut.ApplyDropAsync(
            src, new DropPlan(null, null, false));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SwitchTab_to_Archived_re_queries_and_filters()
    {
        _query.HomeRows.Add(SampleRow(name: "Live"));
        _query.HomeRows.Add(SampleRow(name: "Old", archived: true));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("Old");
    }

    [Fact]
    public async Task SwitchTab_to_Trash_uses_GetTrashTreeAsync()
    {
        _query.TrashRows.Add(SampleRow(name: "Dead"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Trash);

        sut.Rows.Should().ContainSingle().Which.Name.Should().Be("Dead");
        _query.TrashCallCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_sets_Error_on_service_exception()
    {
        _query.NextException = new InvalidOperationException("db down");
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.State.Should().Be(HomeState.Error);
    }

    [Fact]
    public async Task RetryCommand_after_Error_loads_again()
    {
        _query.NextException = new InvalidOperationException("first");
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        _query.HomeRows.Add(SampleRow(name: "OK"));
        await sut.RetryCommand.ExecuteAsync(null);

        sut.State.Should().Be(HomeState.Loaded);
        sut.Rows.Should().ContainSingle().Which.Name.Should().Be("OK");
    }

    [Fact]
    public async Task LoadAsync_with_no_rows_sets_state_to_Empty()
    {
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.State.Should().Be(HomeState.Empty);
        sut.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchTab_to_same_tab_does_not_re_query()
    {
        _query.HomeRows.Add(SampleRow(name: "x"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var firstCallCount = _query.HomeCallCount;

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Active);

        _query.HomeCallCount.Should().Be(firstCallCount);
    }

    [Fact]
    public async Task TypeFilter_excludes_other_types()
    {
        _query.HomeRows.Add(SampleRow(name: "a", type: HabitType.Action));
        _query.HomeRows.Add(SampleRow(name: "c", type: HabitType.Counter));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.ToggleTypeFilterCommand.Execute(HabitType.Action);

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("a");
    }

    [Fact]
    public async Task SearchText_filters_by_name_case_insensitive_contains()
    {
        _query.HomeRows.Add(SampleRow(name: "Morning yoga"));
        _query.HomeRows.Add(SampleRow(name: "Read book"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.SearchText = "YOGA";

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("Morning yoga");
    }

    [Fact]
    public async Task HideCompletedToday_filters_completed_rows()
    {
        var done = SampleRow(name: "done") with { IsCompletedToday = true };
        var pending = SampleRow(name: "pending");
        _query.HomeRows.Add(done);
        _query.HomeRows.Add(pending);
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.HideCompletedToday = true;

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("pending");
    }

    [Fact]
    public async Task TagFilter_keeps_only_rows_with_matching_tag()
    {
        var tagA = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "tagged") with { TagIds = ImmutableArray.Create(tagA) });
        _query.HomeRows.Add(SampleRow(name: "untagged"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.TagFilter = ImmutableHashSet.Create(tagA);

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("tagged");
    }

    [Fact]
    public async Task TabSwitch_resets_all_filters()
    {
        _query.HomeRows.Add(SampleRow(name: "a"));
        _query.HomeRows.Add(SampleRow(name: "b", archived: true));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.SearchText = "a";
        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);

        sut.SearchText.Should().BeNullOrEmpty();
        sut.Rows.Should().ContainSingle().Which.Name.Should().Be("b");
    }

    [Fact]
    public async Task Rebuild_sets_CurrentTab_on_each_row()
    {
        _query.HomeRows.Add(SampleRow(name: "a"));
        _query.HomeRows.Add(SampleRow(name: "b", archived: true));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.Rows.Should().OnlyContain(r => r.CurrentTab == HomeTab.Active);

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);
        sut.Rows.Should().OnlyContain(r => r.CurrentTab == HomeTab.Archived);
    }

    [Fact]
    public async Task PinScope_Global_appears_first_then_Local_then_others()
    {
        _query.HomeRows.Add(SampleRow(name: "n", id: NewId("n")));
        _query.HomeRows.Add(SampleRow(name: "l", id: NewId("l")) with { PinScope = PinScope.Local });
        _query.HomeRows.Add(SampleRow(name: "g", id: NewId("g")) with { PinScope = PinScope.Global });
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.Rows.Select(r => r.Name).Should().Equal("g", "l", "n");
    }

    [Fact]
    public async Task Sort_by_Name_orders_alphabetically_within_pin_layer()
    {
        _query.HomeRows.Add(SampleRow(name: "Charlie"));
        _query.HomeRows.Add(SampleRow(name: "alpha"));
        _query.HomeRows.Add(SampleRow(name: "bravo"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SortOption = HomeSort.Name;
        sut.Rows.Select(r => r.Name).Should().Equal("alpha", "bravo", "Charlie");
    }

    [Fact]
    public async Task Hierarchy_nests_children_under_parent_in_Active_tab()
    {
        var groupId = NewId("group");
        _query.HomeRows.Add(SampleRow(name: "G", id: groupId, type: null));
        _query.HomeRows.Add(SampleRow(name: "child1", parent: groupId));
        _query.HomeRows.Add(SampleRow(name: "child2", parent: groupId));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.Rows.Select(r => (r.Name, r.IndentLevel)).Should().Equal(
            ("G", 0), ("child1", 1), ("child2", 1));
    }

    [Fact]
    public async Task SelectRow_sets_SelectedRow()
    {
        _query.HomeRows.Add(SampleRow(name: "a"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var row = sut.Rows.First();

        sut.SelectRowCommand.Execute(row);

        sut.SelectedRow.Should().BeSameAs(row);
    }

    [Fact]
    public async Task SelectRow_with_null_clears_selection()
    {
        _query.HomeRows.Add(SampleRow(name: "a"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectRowCommand.Execute(sut.Rows.First());

        sut.SelectRowCommand.Execute(null);

        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task SwitchTab_clears_SelectedRow()
    {
        _query.HomeRows.Add(SampleRow(name: "a"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectRowCommand.Execute(sut.Rows.First());

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);

        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task OpenNewHabit_launches_picker_and_selects_created_row()
    {
        var launcher = new FakeHabitTemplatePickerLauncher();
        var newId = Guid.NewGuid();
        launcher.NextResult = newId;
        _query.HomeRows.Add(SampleRow(name: "fresh", id: newId));
        var sut = new HomeViewModel(_query, _nodes, _purge, launcher);

        await sut.LoadCommand.ExecuteAsync(null);
        await sut.OpenNewHabitCommand.ExecuteAsync(null);

        launcher.PickCallCount.Should().Be(1);
        sut.SelectedRow.Should().NotBeNull();
        sut.SelectedRow!.HabitNodeId.Should().Be(newId);
    }

    [Fact]
    public async Task OpenNewHabit_with_no_launcher_is_a_noop()
    {
        var sut = CreateSut();
        await sut.OpenNewHabitCommand.ExecuteAsync(null);
        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task Hierarchy_is_flat_in_Archived_and_Trash()
    {
        var groupId = NewId("g");
        _query.HomeRows.Add(SampleRow(name: "G", id: groupId, type: null, archived: true));
        _query.HomeRows.Add(SampleRow(name: "c", parent: groupId, archived: true));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);

        sut.Rows.All(r => r.IndentLevel == 0).Should().BeTrue();
    }

    [Fact]
    public async Task SoftDelete_removes_row_from_active_list_without_tab_switch()
    {
        _query.HomeRows.Add(SampleRow(name: "keep"));
        _query.HomeRows.Add(SampleRow(name: "doomed"));
        // Mirror the backend: a soft-deleted node is no longer returned by the tree query.
        _nodes.OnSoftDelete = id => _query.HomeRows.RemoveAll(r => r.HabitNodeId == id);

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var doomed = sut.Rows.Single(r => r.Name == "doomed");

        await doomed.SoftDeleteCommand.ExecuteAsync(null);

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("keep");
    }

    [Fact]
    public async Task SoftDelete_of_selected_row_clears_selection()
    {
        _query.HomeRows.Add(SampleRow(name: "doomed"));
        _nodes.OnSoftDelete = id => _query.HomeRows.RemoveAll(r => r.HabitNodeId == id);

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var doomed = sut.Rows.Single();
        sut.SelectRowCommand.Execute(doomed);

        await doomed.SoftDeleteCommand.ExecuteAsync(null);

        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task SoftDelete_preserves_selection_of_a_different_row()
    {
        _query.HomeRows.Add(SampleRow(name: "keep", id: NewId("keep")));
        _query.HomeRows.Add(SampleRow(name: "doomed", id: NewId("doom")));
        _nodes.OnSoftDelete = id => _query.HomeRows.RemoveAll(r => r.HabitNodeId == id);

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var keep = sut.Rows.Single(r => r.Name == "keep");
        var doomed = sut.Rows.Single(r => r.Name == "doomed");
        sut.SelectRowCommand.Execute(keep);

        await doomed.SoftDeleteCommand.ExecuteAsync(null);

        sut.SelectedRow.Should().NotBeNull();
        sut.SelectedRow!.Name.Should().Be("keep");
    }

    [Fact]
    public async Task Archive_from_Active_tab_removes_row_without_tab_switch()
    {
        _query.HomeRows.Add(SampleRow(name: "keep"));
        _query.HomeRows.Add(SampleRow(name: "filed"));
        _nodes.OnSetArchived = (id, arch) =>
        {
            var i = _query.HomeRows.FindIndex(r => r.HabitNodeId == id);
            if (i >= 0) _query.HomeRows[i] = _query.HomeRows[i] with { IsArchived = arch };
        };

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var filed = sut.Rows.Single(r => r.Name == "filed");

        await filed.ArchiveCommand.ExecuteAsync(null);

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("keep");
    }

    [Fact]
    public async Task CompletionRecorded_event_reloads_the_list_for_visual_feedback()
    {
        var bus = new EventBus();
        _query.HomeRows.Add(SampleRow(name: "a"));
        var sut = new HomeViewModel(_query, _nodes, _purge, events: bus);
        await sut.LoadCommand.ExecuteAsync(null);
        var before = _query.HomeCallCount;

        bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow));

        _query.HomeCallCount.Should().BeGreaterThan(before);
    }

    /// <summary>W2V-6 — abstinence produces no completion, so before this the Home row learned
    /// nothing when a period opened or closed: after a relapse the row kept ticking a live clock on a
    /// period that had already ended, and only an unrelated reload cleared it.</summary>
    [Theory]
    [MemberData(nameof(AbstinenceEvents))]
    public async Task An_abstinence_change_reloads_the_list(EventName eventName)
    {
        var bus = new EventBus();
        _query.HomeRows.Add(SampleRow(name: "abs", type: HabitType.Resistance));
        var sut = new HomeViewModel(_query, _nodes, _purge, events: bus);
        await sut.LoadCommand.ExecuteAsync(null);
        var before = _query.HomeCallCount;

        bus.Publish(new AbstinenceChangedPayload(Guid.NewGuid(), eventName));

        _query.HomeCallCount.Should().BeGreaterThan(before);
    }

    public static TheoryData<EventName> AbstinenceEvents =>
    [
        WellKnownEvents.AbstinenceStarted,
        WellKnownEvents.AbstinenceLapsed,
        WellKnownEvents.AbstinencePeriodDeleted,
    ];

    [Fact]
    public async Task QuickAction_that_records_does_not_open_the_detail()
    {
        _query.HomeRows.Add(SampleRow(name: "counter", type: HabitType.Counter));
        var selector = new QuickActionHandlerSelector(
            new IQuickActionHandler[] { new StubQuickActionHandler(HabitType.Counter, recorded: true) });
        var sut = new HomeViewModel(_query, _nodes, _purge, quickActions: selector);
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.Rows.Single().QuickActionCommand.ExecuteAsync(null);

        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task QuickAction_that_only_requests_focus_selects_the_row()
    {
        _query.HomeRows.Add(SampleRow(name: "mood", type: HabitType.State));
        var selector = new QuickActionHandlerSelector(
            new IQuickActionHandler[] { new StubQuickActionHandler(HabitType.State, recorded: false) });
        var sut = new HomeViewModel(_query, _nodes, _purge, quickActions: selector);
        await sut.LoadCommand.ExecuteAsync(null);
        var row = sut.Rows.Single();

        await row.QuickActionCommand.ExecuteAsync(null);

        sut.SelectedRow.Should().BeSameAs(row);
    }

    [Fact]
    public async Task QuickAction_on_already_selected_row_raises_activation()
    {
        _query.HomeRows.Add(SampleRow(name: "mood", type: HabitType.State));
        var selector = new QuickActionHandlerSelector(
            new IQuickActionHandler[] { new StubQuickActionHandler(HabitType.State, recorded: false) });
        var sut = new HomeViewModel(_query, _nodes, _purge, quickActions: selector);
        await sut.LoadCommand.ExecuteAsync(null);
        var row = sut.Rows.Single();
        sut.SelectedRow = row; // already viewing this habit
        var activated = new List<Guid>();
        sut.HabitActivationRequested += (_, id) => activated.Add(id);

        await row.QuickActionCommand.ExecuteAsync(null);

        activated.Should().ContainSingle().Which.Should().Be(row.HabitNodeId);
    }

    [Fact]
    public async Task QuickAction_focus_on_unselected_row_does_not_raise_activation()
    {
        _query.HomeRows.Add(SampleRow(name: "mood", type: HabitType.State));
        var selector = new QuickActionHandlerSelector(
            new IQuickActionHandler[] { new StubQuickActionHandler(HabitType.State, recorded: false) });
        var sut = new HomeViewModel(_query, _nodes, _purge, quickActions: selector);
        await sut.LoadCommand.ExecuteAsync(null);
        var row = sut.Rows.Single();
        var activated = new List<Guid>();
        sut.HabitActivationRequested += (_, id) => activated.Add(id);

        await row.QuickActionCommand.ExecuteAsync(null);

        sut.SelectedRow.Should().BeSameAs(row); // selection change drives the open
        activated.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectedRow_drives_IsSelected_and_clears_the_previous_row()
    {
        _query.HomeRows.Add(SampleRow(name: "First"));
        _query.HomeRows.Add(SampleRow(name: "Second"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var first = sut.Rows.Single(r => r.Name == "First");
        var second = sut.Rows.Single(r => r.Name == "Second");

        sut.SelectedRow = first;
        first.IsSelected.Should().BeTrue();
        second.IsSelected.Should().BeFalse();

        sut.SelectedRow = second;
        first.IsSelected.Should().BeFalse();
        second.IsSelected.Should().BeTrue();

        sut.SelectedRow = null;
        second.IsSelected.Should().BeFalse();
    }

    private sealed class StubQuickActionHandler(HabitType type, bool recorded) : IQuickActionHandler
    {
        public HabitType HabitType { get; } = type;
        public ValueTask<bool> ExecuteAsync(Guid habitNodeId, CancellationToken ct = default) => new(recorded);
    }

    [Fact]
    public async Task Collapsing_a_group_hides_descendants_and_survives_reload()
    {
        var groupId = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: groupId));
        _query.HomeRows.Add(SampleRow(name: "child", parent: groupId));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.Rows.Should().HaveCount(2);

        sut.Rows.Single(r => r.IsGroup).IsExpanded = false;   // toggle → rebuild

        sut.Rows.Should().ContainSingle(r => r.HabitNodeId == groupId);

        await sut.LoadCommand.ExecuteAsync(null);             // lo stato sopravvive al reload
        sut.Rows.Should().ContainSingle(r => r.HabitNodeId == groupId);
        sut.Rows.Single().IsExpanded.Should().BeFalse();

        sut.Rows.Single(r => r.IsGroup).IsExpanded = true;    // re-espansione
        sut.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Rebuild_builds_the_roots_hierarchy_in_parallel_with_flat_rows()
    {
        var g = Guid.NewGuid();
        var sub = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: g));
        _query.HomeRows.Add(SampleRow(name: "h1", parent: g));
        _query.HomeRows.Add(SampleRow(name: "Sub", type: null, id: sub, parent: g));
        _query.HomeRows.Add(SampleRow(name: "h2", parent: sub));
        _query.HomeRows.Add(SampleRow(name: "root"));
        var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        sut.Rows.Should().HaveCount(5);                       // flat invariata
        sut.Roots.Select(r => r.Name).Should().Equal("G", "root");
        var gRow = sut.Roots.Single(r => r.Name == "G");
        gRow.Children.Select(c => c.Name).Should().Equal("h1", "Sub");
        gRow.Children.Single(c => c.Name == "Sub").Children.Single().Name.Should().Be("h2");
        // stesse istanze nei due alberi (selezione/flag condivisi)
        gRow.Should().BeSameAs(sut.Rows.Single(r => r.Name == "G"));
    }

    [Fact]
    public async Task Collapsed_group_has_no_children_in_the_hierarchy()
    {
        var g = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: g));
        _query.HomeRows.Add(SampleRow(name: "h1", parent: g));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.Rows.Single(r => r.IsGroup).IsExpanded = false;

        sut.Roots.Should().ContainSingle();
        sut.Roots.Single().Children.Should().BeEmpty();
    }

    [Fact]
    public async Task NonActive_tabs_put_every_row_in_roots()
    {
        var g = Guid.NewGuid();
        _query.TrashRows.Add(SampleRow(name: "G", type: null, id: g));
        _query.TrashRows.Add(SampleRow(name: "h1", parent: g));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Trash);

        sut.Roots.Should().HaveCount(2);
        sut.Roots.Should().OnlyContain(r => r.Children.Count == 0);
    }

    [Fact]
    public async Task SelectById_expands_collapsed_ancestors_and_selects()
    {
        var groupId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: groupId));
        _query.HomeRows.Add(SampleRow(name: "child", id: childId, parent: groupId));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.Rows.Single(r => r.IsGroup).IsExpanded = false;   // il figlio sparisce

        sut.SelectById(childId);

        sut.SelectedRow!.HabitNodeId.Should().Be(childId);
        sut.Rows.Should().HaveCount(2);                        // antenato riespanso
    }

    [Fact]
    public async Task OpenNewHabit_with_group_selected_passes_group_as_parent()
    {
        var groupId = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: groupId));
        var launcher = new FakeHabitTemplatePickerLauncher();
        var sut = new HomeViewModel(_query, _nodes, _purge, launcher);
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == groupId);

        await sut.OpenNewHabitCommand.ExecuteAsync(null);

        launcher.LastParentId.Should().Be(groupId);
    }

    [Fact]
    public async Task OpenNewHabit_with_habit_selected_passes_its_parent_as_parent()
    {
        var groupId = Guid.NewGuid();
        var habitId = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: groupId));
        _query.HomeRows.Add(SampleRow(name: "h", id: habitId, parent: groupId));
        var launcher = new FakeHabitTemplatePickerLauncher();
        var sut = new HomeViewModel(_query, _nodes, _purge, launcher);
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == habitId);

        await sut.OpenNewHabitCommand.ExecuteAsync(null);

        launcher.LastParentId.Should().Be(groupId);
    }

    [Fact]
    public async Task CreateGroup_with_no_selection_creates_at_root_selects_and_raises()
    {
        _nodes.OnCreate = draft => _query.HomeRows.Add(
            new HomeTreeRow(draft.Id, draft.ParentId, draft.Name, draft.Color, null, null, 0,
                false, PinScope.None, 0, false,
                ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        Guid? raised = null;
        sut.GroupCreated += (_, id) => raised = id;

        await sut.CreateGroupCommand.ExecuteAsync(null);

        var created = _nodes.Store.Values.Single();
        created.NodeType.Should().Be(NodeType.Group);
        created.ParentId.Should().BeNull();
        sut.SelectedRow!.HabitNodeId.Should().Be(created.Id);
        raised.Should().Be(created.Id);
    }

    [Fact]
    public async Task CreateGroup_with_group_selected_creates_inside_it()
    {
        var groupId = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: groupId));
        _nodes.OnCreate = draft => _query.HomeRows.Add(
            new HomeTreeRow(draft.Id, draft.ParentId, draft.Name, draft.Color, null, null, 0,
                false, PinScope.None, 0, false,
                ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == groupId);

        await sut.CreateGroupCommand.ExecuteAsync(null);

        _nodes.Store.Values.Single(n => n.NodeType == NodeType.Group)
            .ParentId.Should().Be(groupId);
    }

    private static HomeTreeRow SampleRow(string name = "x", bool archived = false, HabitType? type = HabitType.Action, Guid? id = null, Guid? parent = null) =>
        new(id ?? Guid.NewGuid(), parent, name, "#000000", type, null, 0, false, PinScope.None, 0, archived, ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    private static Guid NewId(string seed)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(seed.PadRight(16)[..16]);
        return new Guid(bytes);
    }
}
