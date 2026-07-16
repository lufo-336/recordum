using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class CompletionListViewModelTests
{
    [Fact]
    public async Task LoadAsync_takes_top_N_completions_in_descending_order()
    {
        var cs = new FakeCompletionService();
        var habitId = Guid.NewGuid();
        for (int i = 0; i < 30; i++)
        {
            var ts = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i);
            cs.Store[Guid.NewGuid()] = new Completion
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                CompletedAt = ts,
                CompletedAtMs = new DateTimeOffset(ts).ToUnixTimeMilliseconds(),
            };
        }
        var sut = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData(), new FakeDiaryService());

        await sut.LoadAsync(habitId);

        sut.Rows.Should().HaveCount(20);
        sut.Rows[0].CompletedAt.Date.Should().Be(new DateTime(2026, 5, 30));
    }

    [Fact]
    public async Task DeleteCommand_removes_row_and_calls_DeleteAsync()
    {
        var cs = new FakeCompletionService();
        var habitId = Guid.NewGuid();
        var c = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 };
        cs.Store[c.Id] = c;
        var sut = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData(), new FakeDiaryService());
        await sut.LoadAsync(habitId);

        await sut.Rows[0].DeleteCommand.ExecuteAsync(null);

        cs.DeleteCalls.Should().Contain(c.Id);
        sut.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadRangeAsync_lists_only_completions_within_range_descending()
    {
        var cs = new FakeCompletionService();
        var habit = Guid.NewGuid();
        foreach (var d in new[] { 10, 14, 18, 25 }) // giorni di giugno 2026
        {
            var ts = new DateTime(2026, 6, d, 12, 0, 0, DateTimeKind.Utc);
            cs.Store[Guid.NewGuid()] = new Completion
            {
                Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = ts,
                CompletedAtMs = new DateTimeOffset(ts).ToUnixTimeMilliseconds(),
            };
        }
        var sut = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData(), new FakeDiaryService());

        await sut.LoadRangeAsync(habit, new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 20));

        sut.Rows.Should().HaveCount(2);
        sut.Rows[0].CompletedAt.Day.Should().Be(18); // desc
        sut.Rows[1].CompletedAt.Day.Should().Be(14);
    }

    [Fact]
    public async Task LoadRangeAsync_with_null_bounds_lists_everything()
    {
        var cs = new FakeCompletionService();
        var habit = Guid.NewGuid();
        for (int d = 1; d <= 5; d++)
        {
            var ts = new DateTime(2026, 6, d, 9, 0, 0, DateTimeKind.Utc);
            cs.Store[Guid.NewGuid()] = new Completion
            {
                Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = ts,
                CompletedAtMs = new DateTimeOffset(ts).ToUnixTimeMilliseconds(),
            };
        }
        var sut = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData(), new FakeDiaryService());

        await sut.LoadRangeAsync(habit, null, null);

        sut.Rows.Should().HaveCount(5);
    }

    [WpfFact]
    public async Task LoadDayAsync_lists_only_that_day_with_readable_field_values()
    {
        var cs = new FakeCompletionService();
        var ms = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        var habit = Guid.NewGuid();
        var unitId = rd.Units[0].Id; // "l" / Litre
        var waterId = Guid.NewGuid();
        await ms.CreateAsync(new MetricDefinition { Id = waterId, HabitNodeId = habit, Name = "Water", Type = MetricType.Decimal, UnitOfMeasureId = unitId, SortOrder = 0 });

        var day = new DateOnly(2026, 6, 1);
        var onDay = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var onDayCompletion = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = onDay,
            CompletedAtMs = new DateTimeOffset(onDay).ToUnixTimeMilliseconds(),
            Values = new List<CompletionValue> { new() { MetricDefinitionId = waterId, SeriesIndex = 0, ValueDecimal = 1.5m } },
        };
        cs.Store[onDayCompletion.Id] = onDayCompletion;
        var otherDay = new DateTime(2026, 5, 30, 9, 0, 0, DateTimeKind.Utc);
        cs.Store[Guid.NewGuid()] = new Completion { Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = otherDay, CompletedAtMs = new DateTimeOffset(otherDay).ToUnixTimeMilliseconds() };

        var sut = new CompletionListViewModel(cs, ms, rd, new FakeDiaryService());
        await sut.LoadDayAsync(habit, day);

        sut.Rows.Should().ContainSingle();
        var num = 1.5m.ToString(System.Globalization.CultureInfo.CurrentCulture);
        sut.Rows[0].FieldDisplays.Should().ContainSingle()
            .Which.Should().Be($"Water: {num} {rd.Units[0].Code}");
    }

    [WpfFact]
    public async Task LoadDayAsync_groups_fields_by_series_for_multi_series_completion()
    {
        var cs = new FakeCompletionService();
        var ms = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        var habit = Guid.NewGuid();
        var countId = Guid.NewGuid();
        var repsId = Guid.NewGuid();
        await ms.CreateAsync(new MetricDefinition { Id = countId, HabitNodeId = habit, Name = "Count", Type = MetricType.Integer, SortOrder = 0 });
        await ms.CreateAsync(new MetricDefinition { Id = repsId, HabitNodeId = habit, Name = "Reps", Type = MetricType.Integer, SortOrder = 1 });

        var day = new DateOnly(2026, 6, 1);
        var onDay = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        cs.Store[Guid.NewGuid()] = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = onDay,
            CompletedAtMs = new DateTimeOffset(onDay).ToUnixTimeMilliseconds(),
            Values = new List<CompletionValue>
            {
                new() { MetricDefinitionId = countId, SeriesIndex = 0, ValueInteger = 1 },
                new() { MetricDefinitionId = repsId,  SeriesIndex = 0, ValueInteger = 10 },
                new() { MetricDefinitionId = countId, SeriesIndex = 1, ValueInteger = 2 },
                new() { MetricDefinitionId = repsId,  SeriesIndex = 1, ValueInteger = 12 },
            },
        };

        var sut = new CompletionListViewModel(cs, ms, rd, new FakeDiaryService());
        await sut.LoadDayAsync(habit, day);

        var displays = sut.Rows.Single().FieldDisplays;
        displays.Should().HaveCount(2); // one line per series, not four jumbled fields
#pragma warning disable CA1863 // test-only formatting of a localized series label
        var series1 = string.Format(System.Globalization.CultureInfo.CurrentCulture, Recordum.WPF.Resources.Strings.HabitBuilder_SeriesRow, 1);
        var series2 = string.Format(System.Globalization.CultureInfo.CurrentCulture, Recordum.WPF.Resources.Strings.HabitBuilder_SeriesRow, 2);
#pragma warning restore CA1863
        displays[0].Should().StartWith(series1).And.Contain("Count: 1").And.Contain("Reps: 10");
        displays[1].Should().StartWith(series2).And.Contain("Count: 2").And.Contain("Reps: 12");
    }

    [WpfFact]
    public async Task LoadDayAsync_shows_scale_label_not_raw_index()
    {
        var cs = new FakeCompletionService();
        var ms = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        rd.Scales.Clear();
        var scaleId = Guid.NewGuid();
        rd.Scales.Add(new ScaleTemplate { Id = scaleId, Code = "mood", Name = "Mood", MinValue = 1, MaxValue = 3, LabelsJson = "[\"Low\",\"Neutral\",\"High\"]" });
        var habit = Guid.NewGuid();
        var moodId = Guid.NewGuid();
        await ms.CreateAsync(new MetricDefinition { Id = moodId, HabitNodeId = habit, Name = "Mood", Type = MetricType.Integer, ScaleTemplateId = scaleId, SortOrder = 0 });

        var day = new DateOnly(2026, 6, 1);
        var onDay = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        cs.Store[Guid.NewGuid()] = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = onDay,
            CompletedAtMs = new DateTimeOffset(onDay).ToUnixTimeMilliseconds(),
            Values = new List<CompletionValue> { new() { MetricDefinitionId = moodId, SeriesIndex = 0, ValueInteger = 2 } }, // 1-based value 2 = Neutral
        };

        var sut = new CompletionListViewModel(cs, ms, rd, new FakeDiaryService());
        await sut.LoadDayAsync(habit, day);

        sut.Rows[0].FieldDisplays.Should().ContainSingle().Which.Should().Be("Mood: Neutral");
    }

    [WpfFact]
    public async Task LoadDayAsync_renders_a_duration_field_value()
    {
        // CR13: a series with only a duration (ValueSeconds) used to show a blank field because
        // FormatValue had no Seconds branch. 90s must render as "1:30" via DurationFormat.
        var cs = new FakeCompletionService();
        var ms = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        var habit = Guid.NewGuid();
        var timeId = Guid.NewGuid();
        await ms.CreateAsync(new MetricDefinition { Id = timeId, HabitNodeId = habit, Name = "Time", Type = MetricType.Seconds, SortOrder = 0 });

        var day = new DateOnly(2026, 6, 1);
        var onDay = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        cs.Store[Guid.NewGuid()] = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = onDay,
            CompletedAtMs = new DateTimeOffset(onDay).ToUnixTimeMilliseconds(),
            Values = new List<CompletionValue> { new() { MetricDefinitionId = timeId, SeriesIndex = 0, ValueSeconds = 90 } },
        };

        var sut = new CompletionListViewModel(cs, ms, rd, new FakeDiaryService());
        await sut.LoadDayAsync(habit, day);

        sut.Rows[0].FieldDisplays.Should().ContainSingle().Which.Should().Be("Time: 1:30");
    }

    [WpfFact]
    public async Task LoadDayAsync_renders_a_duration_field_in_the_metrics_own_unit()
    {
        // U8: a duration metric with a time unit chosen on it (e.g. "hours") renders in that unit
        // instead of the bare h:mm/mm:ss DurationFormat — 27000s on an "hours" metric reads "7.5 hours".
        var cs = new FakeCompletionService();
        var ms = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        var habit = Guid.NewGuid();
        var hoursUnitId = rd.Units.Single(u => u.Code == "hours").Id;
        var sleepId = Guid.NewGuid();
        await ms.CreateAsync(new MetricDefinition { Id = sleepId, HabitNodeId = habit, Name = "Slept", Type = MetricType.Seconds, UnitOfMeasureId = hoursUnitId, SortOrder = 0 });

        var day = new DateOnly(2026, 6, 1);
        var onDay = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        cs.Store[Guid.NewGuid()] = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = onDay,
            CompletedAtMs = new DateTimeOffset(onDay).ToUnixTimeMilliseconds(),
            Values = new List<CompletionValue> { new() { MetricDefinitionId = sleepId, SeriesIndex = 0, ValueSeconds = 27000 } },
        };

        var sut = new CompletionListViewModel(cs, ms, rd, new FakeDiaryService());
        await sut.LoadDayAsync(habit, day);

        var expected = 7.5m.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) + " hours";
        sut.Rows[0].FieldDisplays.Should().ContainSingle().Which.Should().Be($"Slept: {expected}");
    }

    [Fact]
    public async Task Journal_rows_show_paired_note_body_and_hide_inline_edit()
    {
        var completions = new FakeCompletionService();
        var diary = new FakeDiaryService();
        var habitId = Guid.NewGuid();
        // Pair a completion with a diary note via the fake's store.
        var noteId = await diary.SaveEntryAsync(habitId, new DateOnly(2026, 6, 19), "diary body");
        var completionId = diary.Notes[noteId].CompletionId!.Value;
        completions.Store[completionId] = diary.Completions[completionId];

        var sut = new CompletionListViewModel(completions, new FakeMetricDefinitionService(), new FakeReferenceData(), diary)
        {
            IsJournal = true,
        };
        await sut.LoadAsync(habitId);

        var row = sut.Rows.Should().ContainSingle().Subject;
        row.Notes.Should().Be("diary body");
        row.CanInlineEdit.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_journal_row_routes_through_IDiaryService()
    {
        var completions = new FakeCompletionService();
        var diary = new FakeDiaryService();
        var habitId = Guid.NewGuid();
        var noteId = await diary.SaveEntryAsync(habitId, new DateOnly(2026, 6, 19), "x");
        var completionId = diary.Notes[noteId].CompletionId!.Value;
        completions.Store[completionId] = diary.Completions[completionId];

        var sut = new CompletionListViewModel(completions, new FakeMetricDefinitionService(), new FakeReferenceData(), diary)
        {
            IsJournal = true,
        };
        await sut.LoadAsync(habitId);
        await sut.Rows.Single().DeleteCommand.ExecuteAsync(null);

        diary.DeleteCalls.Should().Contain(completionId);
        completions.DeleteCalls.Should().BeEmpty(); // not deleted directly
        sut.Rows.Should().BeEmpty();
    }
}

public class CompletionRowViewModelTests
{
    [Fact]
    public async Task SaveEditCommand_persists_notes_and_date_via_UpdateAsync()
    {
        var cs = new FakeCompletionService();
        var habitId = Guid.NewGuid();
        var c = new Completion
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitId,
            CompletedAt = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc),
            CompletedAtMs = 1,
            Notes = "orig",
        };
        cs.Store[c.Id] = c;
        var sut = new CompletionRowViewModel(c, cs);

        sut.BeginEditCommand.Execute(null);
        sut.EditedNotes = "new";
        sut.EditedDate = new DateTime(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);
        await sut.SaveEditCommand.ExecuteAsync(null);

        cs.UpdateCalls.Should().ContainSingle().Which.Id.Should().Be(c.Id);
        cs.Store[c.Id].Notes.Should().Be("new");
        cs.Store[c.Id].CompletedAt.Date.Should().Be(new DateTime(2026, 5, 21));
        sut.IsEditing.Should().BeFalse();
    }

    [Fact]
    public void CancelEdit_exits_edit_mode_without_persisting()
    {
        var cs = new FakeCompletionService();
        var c = new Completion { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), CompletedAt = DateTime.UtcNow, CompletedAtMs = 1, Notes = "orig" };
        cs.Store[c.Id] = c;
        var sut = new CompletionRowViewModel(c, cs);
        sut.BeginEditCommand.Execute(null);
        sut.EditedNotes = "mutated";

        sut.CancelEditCommand.Execute(null);

        sut.IsEditing.Should().BeFalse();
        cs.UpdateCalls.Should().BeEmpty();
        cs.Store[c.Id].Notes.Should().Be("orig");
    }
}
