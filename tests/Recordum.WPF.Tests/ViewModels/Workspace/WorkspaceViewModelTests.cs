using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.Workspace;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Workspace;

public class WorkspaceViewModelTests
{
    [Fact]
    public void Starts_with_panoramica_active_and_no_tabs()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();

        sut.Tabs.Should().BeEmpty();
        sut.ActiveSubjectTab.Should().BeNull();
        sut.IsPanoramicaActive.Should().BeTrue();
    }

    [Fact]
    public void Sidebar_defaults_expanded_with_a_sensible_width()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();

        sut.SidebarCollapsed.Should().BeFalse();
        sut.SidebarWidth.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Single_open_creates_one_preview_tab_and_activates_it()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        var id = Guid.NewGuid();

        await sut.OpenSubjectAsync(id, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa");

        sut.Tabs.Should().ContainSingle();
        sut.Tabs[0].IsPreview.Should().BeTrue();
        sut.Tabs[0].SubjectId.Should().Be(id);
        sut.ActiveSubjectTab.Should().Be(sut.Tabs[0]);
        sut.IsPanoramicaActive.Should().BeFalse();
    }

    [Fact]
    public async Task Second_single_open_replaces_the_preview_tab()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa");
        var second = Guid.NewGuid();

        await sut.OpenSubjectAsync(second, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Peso");

        sut.Tabs.Should().ContainSingle();
        sut.Tabs[0].SubjectId.Should().Be(second);
        sut.Tabs[0].IsPreview.Should().BeTrue();
    }

    [Fact]
    public async Task Opening_an_already_open_subject_activates_it_without_duplicating()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        var id = Guid.NewGuid();
        await sut.OpenSubjectAsync(id, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Peso", pin: true);

        await sut.OpenSubjectAsync(id, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa");

        sut.Tabs.Where(t => t.SubjectId == id).Should().ContainSingle();
        sut.ActiveSubjectTab!.SubjectId.Should().Be(id);
    }

    [Fact]
    public async Task Open_with_pin_true_creates_a_pinned_tab()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();

        await sut.OpenSubjectAsync(Guid.NewGuid(), Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa", pin: true);

        sut.Tabs[0].IsPreview.Should().BeFalse();
    }

    [Fact]
    public async Task PinActiveTab_promotes_the_preview_and_clears_preview_slot()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa");

        sut.PinActiveTab();

        sut.ActiveSubjectTab!.IsPreview.Should().BeFalse();
        // A later single open now ADDS rather than replaces (preview slot is empty).
        var n = sut.Tabs.Count;
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Peso");
        sut.Tabs.Should().HaveCount(n + 1);
    }

    [Fact]
    public async Task CloseTab_removes_it_and_closing_last_returns_to_panoramica()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", pin: true);
        var tab = sut.Tabs[0];

        sut.CloseTab(tab);

        sut.Tabs.Should().BeEmpty();
        sut.IsPanoramicaActive.Should().BeTrue();
    }

    [Fact]
    public async Task CloseTab_activates_a_neighbour_when_others_remain()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "B", pin: true);
        var first = sut.Tabs[0];
        sut.ActivateTab(first);

        sut.CloseTab(first);

        sut.Tabs.Should().ContainSingle();
        sut.ActiveSubjectTab.Should().Be(sut.Tabs[0]);
    }

    [Fact]
    public async Task MoveTab_reorders()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "B", pin: true);
        var a = sut.Tabs[0];

        sut.MoveTab(a, 1);

        sut.Tabs[1].Should().Be(a);
    }

    [Fact]
    public async Task NotifySubjectEdited_promotes_the_matching_preview_tab()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        var id = Guid.NewGuid();
        await sut.OpenSubjectAsync(id, WorkspaceTabKind.Habit, "Diario");
        sut.Tabs[0].IsPreview.Should().BeTrue();

        sut.NotifySubjectEdited(id);

        sut.Tabs[0].IsPreview.Should().BeFalse();
    }

    [Fact]
    public async Task NotifySubjectEdited_ignores_unknown_subjects()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Diario");

        sut.NotifySubjectEdited(Guid.NewGuid());

        sut.Tabs[0].IsPreview.Should().BeTrue();
    }

    [Fact]
    public async Task SaveState_persists_only_pinned_tabs_plus_active_and_sidebar()
    {
        var (sut, settings) = WorkspaceViewModelFactory.Create();
        var pinned = Guid.NewGuid();
        await sut.OpenSubjectAsync(pinned, WorkspaceTabKind.Habit, "Corsa", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "preview"); // preview, must NOT persist
        sut.ActivateTab(sut.Tabs.First(t => t.SubjectId == pinned));
        sut.SidebarCollapsed = true;
        sut.SidebarWidth = 240;

        await sut.SaveStateAsync();

        var json = await settings.GetAsync(WorkspaceViewModel.StateSettingKey);
        var state = WorkspaceState.FromJson(json);
        state!.Tabs.Should().ContainSingle();
        state.Tabs[0].SubjectId.Should().Be(pinned);
        state.ActiveTabId.Should().Be(pinned.ToString());
        state.SidebarCollapsed.Should().BeTrue();
        state.SidebarWidth.Should().Be(240);
    }

    [Fact]
    public async Task SaveState_records_panoramica_when_no_subject_active()
    {
        var (sut, settings) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", pin: true);
        sut.ActivatePanoramica();

        await sut.SaveStateAsync();

        var state = WorkspaceState.FromJson(await settings.GetAsync(WorkspaceViewModel.StateSettingKey));
        state!.ActiveTabId.Should().Be(WorkspaceState.PanoramicaId);
    }

    [Fact]
    public async Task RestoreState_reopens_pinned_tabs_and_active()
    {
        var (writer, settings) = WorkspaceViewModelFactory.Create();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await writer.OpenSubjectAsync(a, WorkspaceTabKind.Habit, "A", pin: true);
        await writer.OpenSubjectAsync(b, WorkspaceTabKind.Group, "B", pin: true);
        writer.ActivateTab(writer.Tabs.First(t => t.SubjectId == b));
        await writer.SaveStateAsync();

        // New VM over the SAME settings store.
        var sut = new WorkspaceViewModel(new FakeSubjectScreenFactory(), settings);

        await sut.RestoreStateAsync((id, kind) => kind == WorkspaceTabKind.Group ? "B" : "A");

        sut.Tabs.Select(t => t.SubjectId).Should().Equal(a, b);
        sut.Tabs.Should().OnlyContain(t => !t.IsPreview);
        sut.ActiveSubjectTab!.SubjectId.Should().Be(b);
    }

    [Fact]
    public async Task RestoreState_clamps_an_oversized_persisted_sidebar_width()
    {
        var settings = new FakeAppSettingService();
        var state = new WorkspaceState { SidebarWidth = 900, ActiveTabId = WorkspaceState.PanoramicaId };
        await settings.SetAsync(WorkspaceViewModel.StateSettingKey, state.ToJson());
        var sut = new WorkspaceViewModel(new FakeSubjectScreenFactory(), settings);

        await sut.RestoreStateAsync((id, kind) => "x");

        sut.SidebarWidth.Should().Be(WorkspaceViewModel.MaxSidebarWidth);
    }

    [Fact]
    public async Task RestoreState_emits_no_saves_while_restoring()
    {
        // Persist a pinned tab + sidebar state into a store.
        var (writer, settings) = WorkspaceViewModelFactory.Create();
        await writer.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        writer.SidebarCollapsed = true;
        await writer.SaveStateAsync();

        // New VM over the SAME store, wired like MainWindowViewModel (save on every change).
        var sut = new WorkspaceViewModel(new FakeSubjectScreenFactory(), settings);
        sut.Tabs.CollectionChanged += async (_, _) => await sut.SaveStateAsync();
        sut.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName is nameof(WorkspaceViewModel.ActiveSubjectTab)
                or nameof(WorkspaceViewModel.SidebarCollapsed) or nameof(WorkspaceViewModel.SidebarWidth))
                await sut.SaveStateAsync();
        };
        var before = settings.SetCount;

        await sut.RestoreStateAsync((id, kind) => "A");

        // Restore mutates tabs/active/sidebar; none of those may trigger a persist.
        settings.SetCount.Should().Be(before);
    }

    [Fact]
    public async Task RestoreState_ignores_a_stale_open_columns_value_and_defaults_clean()
    {
        var (sut, settings) = WorkspaceViewModelFactory.Create();
        await settings.SetAsync("ui.open_columns", "Cronologia,Note"); // legacy key, must be ignored

        await sut.RestoreStateAsync((id, kind) => "x");

        sut.Tabs.Should().BeEmpty();
        sut.IsPanoramicaActive.Should().BeTrue();
    }

    [Fact]
    public async Task SaveState_after_Dispose_is_suppressed()
    {
        // At shutdown Dispose() clears Tabs, firing the host's CollectionChanged → SaveStateAsync.
        // By then the DI scope (and its DbContext) is disposed, so the save must be a no-op.
        var (sut, settings, _) = WorkspaceViewModelFactory.CreateWithScreens();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", pin: true);
        var writesBeforeDispose = settings.SetCount;

        sut.Dispose();
        await sut.SaveStateAsync();

        settings.SetCount.Should().Be(writesBeforeDispose);
    }

    [Fact]
    public async Task CloseTab_disposes_the_tabs_scope()
    {
        var (sut, _, screens) = WorkspaceViewModelFactory.CreateWithScreens();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", pin: true);
        var scope = screens.Scopes[0];

        sut.CloseTab(sut.Tabs[0]);

        scope.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Replacing_a_preview_tab_disposes_the_replaced_tabs_scope()
    {
        var (sut, _, screens) = WorkspaceViewModelFactory.CreateWithScreens();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa"); // preview
        var firstScope = screens.Scopes[0];

        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Peso"); // replaces preview

        firstScope.Disposed.Should().BeTrue();
        sut.Tabs.Should().ContainSingle();
    }

    [Fact]
    public async Task A_tabs_AllColumnsClosed_closes_that_tab()
    {
        var (sut, _, _) = WorkspaceViewModelFactory.CreateWithScreens();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", pin: true);
        var tab = sut.Tabs[0];

        tab.Screen.RaiseAllColumnsClosedForTest();

        sut.Tabs.Should().BeEmpty();
        sut.IsPanoramicaActive.Should().BeTrue();
    }

    [Fact]
    public async Task A_tabs_ExportRequested_bubbles_up_with_the_subject_id()
    {
        var (sut, _, _) = WorkspaceViewModelFactory.CreateWithScreens();
        var id = Guid.NewGuid();
        await sut.OpenSubjectAsync(id, WorkspaceTabKind.Habit, "Corsa", pin: true);
        Guid? exported = null;
        sut.ExportRequested += (_, sid) => exported = sid;

        sut.Tabs[0].Screen.RaiseExportRequestedForTest();

        exported.Should().Be(id);
    }

    [Fact]
    public async Task CloseTabCmd_command_removes_the_tab()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", pin: true);
        var tab = sut.Tabs[0];

        sut.CloseTabCmdCommand.Execute(tab);

        sut.Tabs.Should().BeEmpty();
    }

    [Fact]
    public async Task CloseActiveTab_closes_the_active_tab()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "B", pin: true);
        var active = sut.ActiveSubjectTab!;

        sut.CloseActiveTabCommand.Execute(null);

        sut.Tabs.Should().NotContain(active);
        sut.Tabs.Should().ContainSingle();
    }

    [Fact]
    public async Task TogglePin_pins_a_preview_then_demotes_back_to_preview()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa"); // preview
        var tab = sut.Tabs[0];
        tab.IsPreview.Should().BeTrue();

        sut.TogglePinCommand.Execute(tab);
        tab.IsPreview.Should().BeFalse();

        sut.TogglePinCommand.Execute(tab);
        tab.IsPreview.Should().BeTrue();
    }

    [Fact]
    public async Task TogglePin_demote_keeps_at_most_one_preview_tab()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Pinned", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Preview"); // the preview slot
        var pinned = sut.Tabs[0];
        var preview = sut.Tabs[1];

        sut.TogglePinCommand.Execute(pinned); // demote the pinned one to preview

        // The old preview was promoted; only the just-demoted tab is now a preview.
        sut.Tabs.Count(t => t.IsPreview).Should().Be(1);
        pinned.IsPreview.Should().BeTrue();
        preview.IsPreview.Should().BeFalse();
    }

    [Fact]
    public void ToggleSidebar_flips_collapsed()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        sut.SidebarCollapsed.Should().BeFalse();

        sut.ToggleSidebarCommand.Execute(null);

        sut.SidebarCollapsed.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_disposes_all_open_tabs_scopes()
    {
        var (sut, _, screens) = WorkspaceViewModelFactory.CreateWithScreens();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "B", pin: true);

        sut.Dispose();

        screens.Scopes.Should().OnlyContain(s => s.Disposed);
    }

    // Focus "zen" mode (workspace level): when the active tab's screen has a focused column,
    // the shell hides the sidebar + tab strip so the panel fills the window (spec §3.6, escalated
    // from column-focus to a workspace-wide focus per the author's intent).

    [Fact]
    public void IsFocusZen_is_false_when_no_tab_is_active()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();

        sut.IsFocusZen.Should().BeFalse();
    }

    [Fact]
    public async Task Focusing_the_active_screen_turns_zen_on_and_raises_change()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa");
        using var monitor = sut.Monitor();

        sut.ActiveSubjectTab!.Screen.FocusedColumn = HabitColumn.Note;

        sut.IsFocusZen.Should().BeTrue();
        monitor.Should().RaisePropertyChangeFor(x => x.IsFocusZen);
    }

    [Fact]
    public async Task Exiting_focus_on_the_active_screen_turns_zen_off()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa");
        sut.ActiveSubjectTab!.Screen.FocusedColumn = HabitColumn.Note;

        sut.ActiveSubjectTab!.Screen.FocusedColumn = null;

        sut.IsFocusZen.Should().BeFalse();
    }

    [Fact]
    public async Task Switching_tabs_retargets_zen_to_the_new_active_screen()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        var a = sut.ActiveSubjectTab!;
        a.Screen.FocusedColumn = HabitColumn.Note;            // tab A is focused
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "B", pin: true);  // active = B, not focused

        sut.IsFocusZen.Should().BeFalse();                    // B is not focused

        sut.ActivateTab(a);
        sut.IsFocusZen.Should().BeTrue();                     // back on focused A
    }

    [Fact]
    public async Task A_background_tabs_focus_does_not_drive_zen()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        var a = sut.ActiveSubjectTab!;
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "B", pin: true);  // active = B

        a.Screen.FocusedColumn = HabitColumn.Note;            // focus the *inactive* tab A

        sut.IsFocusZen.Should().BeFalse();                    // only the active tab drives zen
    }
}
