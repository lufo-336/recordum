using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.History;

/// <summary>B1: il blocco del giorno scelto e l'elenco del periodo stanno nella stessa colonna; una registrazione
/// compare in uno solo dei due.</summary>
public class PeriodListExcludedDayTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);

    private static async Task<(HabitScreenRig Rig, Guid Id)> LoadAsync(
        HabitType type, Action<HabitScreenRig, Guid> seed, DayBoundary? boundary = null)
    {
        var rig = HabitScreenViewModelFactory.CreateRig(Now, boundary);
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode
        {
            Id = id, NodeType = NodeType.Habit, HabitType = type, Name = "corsa",
            CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        seed(rig, id);
        await rig.Vm.LoadAsync(id);
        return (rig, id);
    }

    private static void Record(HabitScreenRig rig, Guid habit, DateTime utc, string? notes = null)
    {
        var id = Guid.NewGuid();
        rig.Completions.Store[id] = new Completion
        {
            Id = id, HabitNodeId = habit, CompletedAt = utc, Notes = notes,
            CompletedAtMs = new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        };
    }

    private static DateTime Utc(int day, int hour = 8) => new(2026, 10, day, hour, 0, 0, DateTimeKind.Utc);

    private static AbstinencePeriod Period(Guid habit, DateTime start, DateTime? end) => new()
    {
        Id = Guid.NewGuid(), HabitNodeId = habit,
        StartedAtMs = new DateTimeOffset(start, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        EndedAtMs = end is { } e ? new DateTimeOffset(e, TimeSpan.Zero).ToUnixTimeMilliseconds() : null,
    };

    private static int[] ListedDays(HabitScreenRig rig)
        => rig.Vm.PeriodRecords.Records.Rows.Select(r => r.CompletedAt.Day).ToArray();

    [Fact]
    public async Task Period_list_skips_the_selected_day()
    {
        var (rig, _) = await LoadAsync(HabitType.Action, (r, id) =>
        {
            Record(r, id, Utc(26)); Record(r, id, Utc(27)); Record(r, id, Utc(28));
        });

        await rig.Vm.SelectDayAsync(new DateOnly(2026, 10, 27));

        ListedDays(rig).Should().Equal(28, 26);
        rig.Vm.Records.Records.Rows.Should().ContainSingle().Which.CompletedAt.Day.Should().Be(27);
    }

    [Fact]
    public async Task Opening_the_screen_already_skips_today()
    {
        var (rig, _) = await LoadAsync(HabitType.Action, (r, id) => { Record(r, id, Utc(27)); Record(r, id, Utc(28)); });

        ListedDays(rig).Should().Equal(27);
    }

    [Fact]
    public async Task Selecting_another_day_brings_the_previous_one_back()
    {
        var (rig, _) = await LoadAsync(HabitType.Action, (r, id) =>
        {
            Record(r, id, Utc(26)); Record(r, id, Utc(27)); Record(r, id, Utc(28));
        });

        await rig.Vm.SelectDayAsync(new DateOnly(2026, 10, 27));
        await rig.Vm.SelectDayAsync(new DateOnly(2026, 10, 26));

        ListedDays(rig).Should().Equal(28, 27);
    }

    [Fact]
    public async Task The_skipped_day_is_the_habits_day_not_the_utc_date()
    {
        // Inizio di giornata alle 04:00: la registrazione dell'una di notte del 28 appartiene al 27.
        var boundary = new DayBoundary(TimeZoneInfo.Utc, new TimeOnly(4, 0));
        var (rig, _) = await LoadAsync(HabitType.Action, (r, id) =>
        {
            Record(r, id, Utc(27, 8)); Record(r, id, Utc(28, 1)); Record(r, id, Utc(28, 8));
        }, boundary);

        await rig.Vm.SelectDayAsync(new DateOnly(2026, 10, 27));

        rig.Vm.PeriodRecords.Records.Rows.Should().ContainSingle().Which.CompletedAt.Hour.Should().Be(8);
        rig.Vm.PeriodRecords.Records.Rows[0].CompletedAt.Day.Should().Be(28);
    }

    [Fact]
    public async Task Empty_message_does_not_lie()
    {
        // Le registrazioni del periodo stanno tutte nel giorno scelto: l'elenco è vuoto, ma "niente registrato" sarebbe falso.
        var (rig, _) = await LoadAsync(HabitType.Action, (r, id) => Record(r, id, Utc(28)));

        rig.Vm.PeriodRecords.Records.Rows.Should().BeEmpty();
        rig.Vm.PeriodRecords.IsEmpty.Should().BeFalse();

        // Scelto un giorno senza registrazioni, quella di oggi torna nell'elenco.
        await rig.Vm.SelectDayAsync(new DateOnly(2026, 10, 20));
        rig.Vm.PeriodRecords.IsEmpty.Should().BeFalse();
        ListedDays(rig).Should().Equal(28);
    }

    [Fact]
    public async Task A_period_with_nothing_at_all_still_says_so()
    {
        var (rig, _) = await LoadAsync(HabitType.Action, (_, _) => { });

        rig.Vm.PeriodRecords.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Abstinence_current_period_is_shown_once()
    {
        var (rig, _) = await LoadAsync(HabitType.Resistance, (r, id) => r.Abstinence.Periods[id] =
        [
            Period(id, Utc(2), Utc(9)),
            Period(id, Utc(10), null),
        ]);

        // Oggi scelto: "In corso" sta nel blocco del giorno, l'elenco tiene solo il periodo chiuso.
        rig.Vm.Records.Periods.Rows.Should().ContainSingle().Which.IsCurrent.Should().BeTrue();
        rig.Vm.PeriodRecords.Periods.Rows.Should().ContainSingle().Which.IsCurrent.Should().BeFalse();

        // Scelto un giorno del periodo chiuso, le parti si scambiano.
        await rig.Vm.SelectDayAsync(new DateOnly(2026, 10, 5));
        rig.Vm.Records.Periods.Rows.Should().ContainSingle().Which.IsCurrent.Should().BeFalse();
        rig.Vm.PeriodRecords.Periods.Rows.Should().ContainSingle().Which.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task Abstinence_empty_message_does_not_lie()
    {
        var (rig, _) = await LoadAsync(HabitType.Resistance, (r, id) => r.Abstinence.Periods[id] = [Period(id, Utc(10), null)]);

        rig.Vm.PeriodRecords.Periods.Rows.Should().BeEmpty();
        rig.Vm.PeriodRecords.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_the_last_listed_period_does_not_make_the_message_lie()
    {
        var (rig, _) = await LoadAsync(HabitType.Resistance, (r, id) => r.Abstinence.Periods[id] =
        [
            Period(id, Utc(2), Utc(9)),
            Period(id, Utc(10), null),
        ]);

        await rig.Vm.PeriodRecords.Periods.Rows.Single().DeleteCommand.ExecuteAsync(null);

        rig.Vm.PeriodRecords.Periods.Rows.Should().BeEmpty();
        rig.Vm.PeriodRecords.IsEmpty.Should().BeFalse("\"In corso\" sta nel blocco del giorno");
    }

    [Fact]
    public async Task With_notes_filter_still_works()
    {
        var (rig, _) = await LoadAsync(HabitType.Action, (r, id) =>
        {
            Record(r, id, Utc(25), "con nota"); Record(r, id, Utc(26)); Record(r, id, Utc(28), "oggi");
        });

        rig.Vm.PeriodRecords.Records.OnlyWithNotes = true;
        ListedDays(rig).Should().Equal(25);

        // Il filtro resta acceso cambiando giorno: torna la nota di oggi, esce quella del 25.
        await rig.Vm.SelectDayAsync(new DateOnly(2026, 10, 25));
        ListedDays(rig).Should().Equal(28);

        // Di nuovo su oggi e tolto il filtro: tutte tranne quella di oggi.
        await rig.Vm.SelectDayAsync(new DateOnly(2026, 10, 28));

        rig.Vm.PeriodRecords.Records.OnlyWithNotes = false;
        ListedDays(rig).Should().Equal(26, 25);
    }
}
