using System.Globalization;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.History;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>L'elenco del periodo nella Cronologia (spec §6.1). Oggi 28/10/2026 10:00, reset 05:00, it-IT.</summary>
public sealed class HistoryListTests : IDisposable
{
    private static readonly DayBoundary Boundary = new(TimeZoneInfo.Utc, new TimeOnly(5, 0));
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture, _uiCulture = CultureInfo.CurrentUICulture;

    public HistoryListTests() => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("it-IT");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    private static async Task<(HabitScreenRig Rig, Guid Id)> LoadAsync(HabitType type, Action<HabitScreenRig, Guid>? seed = null)
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero), Boundary);
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode
        {
            Id = id, NodeType = NodeType.Habit, HabitType = type, Name = "h",
            CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        seed?.Invoke(rig, id);
        await rig.Vm.LoadAsync(id);
        return (rig, id);
    }

    private static void Add(HabitScreenRig rig, Guid id, DateTimeOffset at, string? notes = null)
    {
        var c = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = id, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(), Notes = notes,
        };
        rig.Completions.Store[c.Id] = c;
    }

    [Fact]
    public async Task Period_list_follows_the_period()
    {
        var (rig, _) = await LoadAsync(HabitType.Action, (r, id) =>
        {
            Add(r, id, new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));
            Add(r, id, new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero));
            Add(r, id, new DateTimeOffset(2026, 10, 28, 7, 42, 0, TimeSpan.Zero));
            Add(r, id, new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));
        });
        rig.Vm.PeriodRecords.Records.Rows.Should().HaveCount(3);   // ottobre

        rig.Vm.Period.Period = HistoryPeriod.Week;
        await Task.Delay(30);

        rig.Vm.PeriodRecords.Records.Rows.Select(r => r.DayLabel).Should().Equal("mer 28 ott", "lun 26 ott");
    }

    [Fact]
    public async Task A_config_change_reloads_the_period_list_with_the_new_series_labels()
    {
        // UX2A-8: dopo un cambio delle etichette di serie l'elenco del periodo restava vecchio.
        var (rig, id) = await LoadAsync(HabitType.Counter, (r, i) => Add(r, i, new DateTimeOffset(2026, 10, 28, 7, 0, 0, TimeSpan.Zero)));
        var before = rig.Vm.PeriodRecords.Records.Rows.Single();
        rig.Configs.Store[id] = new HabitConfig { HabitNodeId = id, SeriesLabelsJson = "[\"a\",\"b\"]" };

        rig.Bus.Publish(new Recordum.Plugins.Contracts.Events.HabitUpdatedPayload(id));
        await Task.Delay(100);

        rig.Vm.PeriodRecords.Records.SeriesLabelsJson.Should().Be("[\"a\",\"b\"]");
        rig.Vm.PeriodRecords.Records.Rows.Single().Should().NotBeSameAs(before, "l'elenco e' stato riletto");
    }

    [Fact]
    public async Task Rows_have_day_and_time_labels()
    {
        var (rig, _) = await LoadAsync(HabitType.Action, (r, id) =>
        {
            Add(r, id, new DateTimeOffset(2026, 10, 28, 7, 42, 0, TimeSpan.Zero));
            Add(r, id, new DateTimeOffset(2026, 10, 27, 4, 30, 0, TimeSpan.Zero));   // prima del reset: è del 26
        });
        var rows = rig.Vm.PeriodRecords.Records.Rows;
        rows[0].DayLabel.Should().Be("mer 28 ott");
        rows[0].TimeLabel.Should().Be("07:42");
        rows[1].DayLabel.Should().Be("lun 26 ott");
    }

    [Fact]
    public async Task With_notes_filters_without_reloading()
    {
        var (rig, _) = await LoadAsync(HabitType.Action, (r, id) =>
        {
            Add(r, id, new DateTimeOffset(2026, 10, 28, 7, 0, 0, TimeSpan.Zero), "gambe pesanti");
            Add(r, id, new DateTimeOffset(2026, 10, 27, 7, 0, 0, TimeSpan.Zero));
        });
        var list = rig.Vm.PeriodRecords.Records;
        list.Rows.Should().HaveCount(2);

        rig.Completions.Store.Clear();   // un filtro che rilegge il DB troverebbe zero righe
        list.OnlyWithNotes = true;
        list.Rows.Should().ContainSingle().Which.Preview.Should().Be("gambe pesanti");

        list.OnlyWithNotes = false;
        list.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Preview_has_no_markdown()
    {
        var (rig, _) = await LoadAsync(HabitType.Action, (r, id) =>
            Add(r, id, new DateTimeOffset(2026, 10, 28, 7, 0, 0, TimeSpan.Zero), "\n## Al lago\n**fredda**"));
        rig.Vm.PeriodRecords.Records.Rows[0].Preview.Should().Be("Al lago");
    }

    [Fact]
    public async Task Abstinence_uses_the_period_list()
    {
        var (rig, _) = await LoadAsync(HabitType.Resistance, (r, id) =>
            r.Abstinence.Periods[id] =
            [
                new AbstinencePeriod
                {
                    HabitNodeId = id,
                    StartedAtMs = new DateTimeOffset(2026, 10, 16, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                },
            ]);
        rig.Vm.PeriodRecords.CurrentList.Should().BeSameAs(rig.Vm.PeriodRecords.Periods);
        rig.Vm.PeriodRecords.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task The_day_records_stay_on_the_selected_day()
    {
        var (rig, _) = await LoadAsync(HabitType.Action, (r, id) =>
        {
            Add(r, id, new DateTimeOffset(2026, 10, 28, 7, 0, 0, TimeSpan.Zero));
            Add(r, id, new DateTimeOffset(2026, 10, 20, 7, 0, 0, TimeSpan.Zero));
        });
        rig.Vm.Records.Records.Rows.Should().ContainSingle();
        rig.Vm.PeriodRecords.Records.Rows.Should().HaveCount(2);
    }
}
