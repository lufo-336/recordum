using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Core.Models;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitEditor;
using Recordum.UI.Views.HabitEditor.Sections;
using Xunit;
using static Recordum.UI.Tests.HabitEditor.EditorPaneTests;

namespace Recordum.UI.Tests.HabitEditor;

/// <summary>F2b Task 7: le sei sezioni del ritmo di Configura, su DB privato (stesso host di EditorPaneTests).</summary>
public class RhythmSectionsTests
{
    private static async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> f)
    {
        using var scope = Host.Provider.CreateScope();
        return await f(scope.ServiceProvider);
    }

    private static Task<HabitConfig?> ConfigAsync(Guid id) =>
        InScope(sp => sp.GetRequiredService<IHabitConfigService>().GetAsync(id).AsTask());

    private static Task<HabitUiPrefs> PrefsAsync(Guid id) =>
        InScope(sp => sp.GetRequiredService<IHabitUiPrefsService>().GetAsync(id).AsTask());

    private static Task<Goal?> GoalAsync(Guid id) =>
        InScope(sp => sp.GetRequiredService<IGoalService>().GetActiveAsync(id).AsTask());

    private static async Task<(Opened O, EditorPane Pane, TSection Section, TView View)> OpenSectionAsync<TSection, TView>(
        Func<HabitEditorViewModel, TSection> pick, string name, HabitType type = HabitType.Action)
        where TSection : SectionViewModelBase where TView : Control
    {
        var id = await CreateAsync($"{name}-{Guid.NewGuid():N}", NodeType.Habit, type);
        var o = await OpenAsync(id);
        var pane = await OpenConfiguraAsync(o);
        var section = pick(o.Vm.Workspace.ActiveSubjectTab!.Screen.Editor);
        section.IsVisible.Should().BeTrue("la sezione si applica a questo tipo");
        section.IsExpanded = true;
        PrivateDbHost.Pump();
        return (o, pane, section, pane.GetVisualDescendants().OfType<TView>().Single());
    }

    private static void TypeInto(Opened o, TextBox box, string text)
    {
        box.Focus();
        PrivateDbHost.Pump();
        box.SelectAll();
        o.Window.KeyTextInput(text);
        PrivateDbHost.Pump();
        box.Text.Should().Be(text, "il testo e' arrivato nella casella (fuoco: {0})", box.IsFocused);
        o.Window.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "NameBox").Focus();   // perdita del fuoco: il binding e' LostFocus
        PrivateDbHost.Pump();
    }

    [AvaloniaFact]
    public async Task Scheduling_specific_days_and_weekly_target_reach_the_db()
    {
        var (o, _, s, v) = await OpenSectionAsync<SchedulingSectionViewModel, SchedulingSectionView>(e => e.Scheduling, "Sch");
        try
        {
            Named<ComboBox>(v, "ScheduleTypeBox").SelectedItem = ScheduleType.SpecificDays;
            PrivateDbHost.Pump();
            var toggles = v.GetVisualDescendants().OfType<ToggleButton>().Where(t => t.Classes.Contains("pill")).ToList();
            toggles.Should().HaveCount(7).And.OnlyContain(t => t.IsEffectivelyVisible);
            toggles[0].IsChecked = true;   // Mon
            toggles[2].IsChecked = true;   // Wed
            await UntilAsync(() => ConfigAsync(o.Id).GetAwaiter().GetResult() is { ScheduleType: ScheduleType.SpecificDays, ScheduleDaysJson: not null } c
                && c.ScheduleDaysJson.Contains("Mon") && c.ScheduleDaysJson.Contains("Wed") && !c.ScheduleDaysJson.Contains("Tue"));

            Named<ComboBox>(v, "ScheduleTypeBox").SelectedItem = ScheduleType.WeeklyX;
            PrivateDbHost.Pump();
            TypeInto(o, Named<TextBox>(v, "WeeklyTargetBox"), "3");
            await UntilAsync(() => ConfigAsync(o.Id).GetAwaiter().GetResult() is { ScheduleType: ScheduleType.WeeklyX, ScheduleWeeklyTarget: 3 });
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Streak_mode_reaches_the_db()
    {
        var (o, _, _, v) = await OpenSectionAsync<StreakSectionViewModel, StreakSectionView>(e => e.Streak, "Str");
        try
        {
            Named<ComboBox>(v, "StreakModeBox").SelectedItem = StreakMode.Weekly;
            Named<ComboBox>(v, "StreakRecoveryBox").SelectedItem = StreakRecovery.GraceDay;
            await UntilAsync(() => ConfigAsync(o.Id).GetAwaiter().GetResult() is { StreakMode: StreakMode.Weekly, StreakRecovery: StreakRecovery.GraceDay });
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Goal_with_a_period_reaches_the_db_and_a_rejected_combination_shows_the_error()
    {
        using var _ = TestCulture.Pin();
        var (o, _, s, v) = await OpenSectionAsync<GoalSectionViewModel, GoalSectionView>(e => e.Goal, "Goal");
        try
        {
            TypeInto(o, Named<TextBox>(v, "TargetBox"), "7");
            await UntilAsync(() => GoalAsync(o.Id).GetAwaiter().GetResult() is { TargetValue: 7m });

            // Rolling senza giorni viola ck_goal_period_consistency: errore del ViewModel, nessuna eccezione.
            Named<ComboBox>(v, "PeriodBox").SelectedItem = GoalPeriodType.Rolling;
            await UntilAsync(() => s.Status == EditorStatus.Error);
            s.ErrorMessage.Should().NotBeNullOrEmpty();
            (await GoalAsync(o.Id))!.PeriodType.Should().Be(GoalPeriodType.Open, "la combinazione rifiutata non e' salvata");

            // I giorni arrivano dopo: ora la combinazione e' valida e il periodo si salva (stato in memoria == DB).
            s.ShowAdvanced = true;
            PrivateDbHost.Pump();
            TypeInto(o, Named<TextBox>(v, "RollingDaysBox"), "10");
            await UntilAsync(() => GoalAsync(o.Id).GetAwaiter().GetResult() is { PeriodType: GoalPeriodType.Rolling, RollingDays: 10 });
            s.Status.Should().Be(EditorStatus.Saved);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Goal_target_text_the_binding_rejects_leaves_the_stored_value()
    {
        using var _ = TestCulture.Pin();
        var (o, _, s, v) = await OpenSectionAsync<GoalSectionViewModel, GoalSectionView>(e => e.Goal, "GoalRej");
        try
        {
            TypeInto(o, Named<TextBox>(v, "TargetBox"), "7");
            await UntilAsync(() => GoalAsync(o.Id).GetAwaiter().GetResult() is { TargetValue: 7m });
            TypeInto(o, Named<TextBox>(v, "TargetBox"), "abc");
            PrivateDbHost.Pump();
            await Task.Delay(300);   // un'eventuale scrittura sbagliata e' fire-and-forget: le si da' il tempo di arrivare
            (await GoalAsync(o.Id))!.TargetValue.Should().Be(7m, "un refuso non sovrascrive il valore salvato (R6)");
            s.TargetValue.Should().Be(7m);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task ResetTime_reaches_the_db_and_the_warning_is_visible()
    {
        var (o, _, _, v) = await OpenSectionAsync<ResetTimeSectionViewModel, ResetTimeSectionView>(e => e.ResetTime, "Rst");
        try
        {
            var warning = Named<TextBlock>(v, "ResetWarning");
            warning.IsEffectivelyVisible.Should().BeTrue("decisione 8: il cambio reinterpreta lo storico");
            warning.Text.Should().Be(Recordum.Presentation.Resources.Strings.Editor_ResetTime_Warning);
            TypeInto(o, Named<TextBox>(v, "ResetTimeBox"), "04:00");
            await UntilAsync(() => ReadAsync(o.Id).GetAwaiter().GetResult().ResetTime == new TimeOnly(4, 0));
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Changing_the_reset_time_with_the_screen_open_moves_the_calendar_today()
    {
        var clock = Host.Clock.Now;
        Host.Clock.Now = new DateTimeOffset(2026, 3, 15, 2, 30, 0, TimeSpan.Zero);   // 02:30: col reset 04:00 e' ancora il 14
        var (o, _, _, v) = await OpenSectionAsync<ResetTimeSectionViewModel, ResetTimeSectionView>(e => e.ResetTime, "RstCal");
        try
        {
            var calendar = o.Vm.Workspace.ActiveSubjectTab!.Screen.Calendar;
            DateOnly? Today() => calendar.Cells.SingleOrDefault(c => c.IsToday)?.Date;
            Today().Should().Be(new DateOnly(2026, 3, 15), "reset predefinito: il 15 e' gia' iniziato");

            TypeInto(o, Named<TextBox>(v, "ResetTimeBox"), "04:00");
            await UntilAsync(() => Today() == new DateOnly(2026, 3, 14));
            Named<TextBlock>(v, "ResetWarning").IsEffectivelyVisible.Should().BeTrue();
        }
        finally { o.Window.Close(); Host.Clock.Now = clock; }
    }

    [AvaloniaFact]
    public async Task Registration_repeatable_and_the_journal_gesture_reach_the_db()
    {
        var (o, _, _, v) = await OpenSectionAsync<RegistrationSectionViewModel, RegistrationSectionView>(e => e.Registration, "Reg", HabitType.Counter);
        try
        {
            Named<CheckBox>(v, "RepeatableBox").IsChecked = true;
            await UntilAsync(() => ConfigAsync(o.Id).GetAwaiter().GetResult() is { Repeatable: true });
        }
        finally { o.Window.Close(); }

        var (o2, _, s2, v2) = await OpenSectionAsync<RegistrationSectionViewModel, RegistrationSectionView>(e => e.Registration, "RegJ", HabitType.Journal);
        try
        {
            Named<ComboBox>(v2, "GestureBox").SelectedItem = s2.GestureOptions.Single(g => g.Gesture == JournalPrimaryGesture.QuickNote);
            await UntilAsync(() => PrefsAsync(o2.Id).GetAwaiter().GetResult()
                .JournalGesture == JournalPrimaryGesture.QuickNote);
        }
        finally { o2.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task HomeRow_strip_reaches_the_db_and_changes_the_real_home_row()
    {
        var (o, _, s, v) = await OpenSectionAsync<HomeRowSectionViewModel, HomeRowSectionView>(e => e.HomeRow, "Hrw");
        try
        {
            var row = () => o.Vm.Home.Rows.Single(r => r.HabitNodeId == o.Id);
            row().StripLength.Should().Be(HomeStrip.Days7, "il default di un'azione");
            Named<ComboBox>(v, "StripBox").SelectedItem = s.StripOptions.Single(x => x.Strip == HomeStrip.Days14);
            await UntilAsync(() => PrefsAsync(o.Id).GetAwaiter().GetResult().Strip == HomeStrip.Days14);
            await UntilAsync(() => row().StripLength == HomeStrip.Days14 && row().StripCounts.Length == 14);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task The_rhythm_sections_have_a_branch_and_only_for_their_own_types()
    {
        var (o, pane, _, _) = await OpenSectionAsync<SchedulingSectionViewModel, SchedulingSectionView>(e => e.Scheduling, "Brn");
        try
        {
            var editor = o.Vm.Workspace.ActiveSubjectTab!.Screen.Editor;
            foreach (SectionViewModelBase sec in new SectionViewModelBase[] { editor.Scheduling, editor.Streak, editor.Goal, editor.ResetTime, editor.Registration, editor.HomeRow })
                new SectionTemplate().Build(sec).Should().NotBeNull(sec.Title);
            editor.Registration.IsVisible.Should().BeFalse("un'azione non ha registrazione");
            pane.GetVisualDescendants().OfType<Expander>().Where(e => e.IsVisible)
                .Select(e => e.Header).Should().NotContain(editor.Registration.Title);
        }
        finally { o.Window.Close(); }
    }
}
