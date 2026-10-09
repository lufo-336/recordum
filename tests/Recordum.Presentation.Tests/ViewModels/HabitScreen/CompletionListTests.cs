using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

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

    [Fact]
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

    [Fact]
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
        var series1 = string.Format(System.Globalization.CultureInfo.CurrentCulture, Recordum.Presentation.Resources.Strings.HabitBuilder_SeriesRow, 1);
        var series2 = string.Format(System.Globalization.CultureInfo.CurrentCulture, Recordum.Presentation.Resources.Strings.HabitBuilder_SeriesRow, 2);
#pragma warning restore CA1863
        displays[0].Should().StartWith(series1).And.Contain("Count: 1").And.Contain("Reps: 10");
        displays[1].Should().StartWith(series2).And.Contain("Count: 2").And.Contain("Reps: 12");
    }

    [Fact]
    public async Task LoadDayAsync_keeps_one_line_per_series_when_a_single_series_carries_several_fields()
    {
        // W23-VF6: a multi-series registration read "Series 1 — Count: 1 · Reps: 10" while a
        // single-series one broke the very same fields onto a line each — two layouts for one object.
        // A lone series still drops the prefix: there is nothing to disambiguate.
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
                new() { MetricDefinitionId = repsId, SeriesIndex = 0, ValueInteger = 10 },
            },
        };

        var sut = new CompletionListViewModel(cs, ms, rd, new FakeDiaryService());
        await sut.LoadDayAsync(habit, day);

        sut.Rows.Single().FieldDisplays.Should().ContainSingle()
            .Which.Should().Be("Count: 1 · Reps: 10");
    }

    [Fact]
    public async Task LoadDayAsync_labels_series_with_their_configured_names()
    {
        var cs = new FakeCompletionService();
        var ms = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        var habit = Guid.NewGuid();
        var hoursId = Guid.NewGuid();
        await ms.CreateAsync(new MetricDefinition { Id = hoursId, HabitNodeId = habit, Name = "Hours", Type = MetricType.Integer, SortOrder = 0 });

        var day = new DateOnly(2026, 6, 1);
        var onDay = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        cs.Store[Guid.NewGuid()] = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = onDay,
            CompletedAtMs = new DateTimeOffset(onDay).ToUnixTimeMilliseconds(),
            Values = new List<CompletionValue>
            {
                new() { MetricDefinitionId = hoursId, SeriesIndex = 0, ValueInteger = 7 },
                new() { MetricDefinitionId = hoursId, SeriesIndex = 1, ValueInteger = 1 },
            },
        };

        // Registered as "Night"/"Nap" in the quick-add — the records list must read them back the
        // same way instead of the positional "Series 1 / Series 2" fallback (W23-VF3).
        var sut = new CompletionListViewModel(cs, ms, rd, new FakeDiaryService())
        {
            SeriesLabelsJson = "[\"Night\",\"Nap\"]",
        };
        await sut.LoadDayAsync(habit, day);

        var displays = sut.Rows.Single().FieldDisplays;
        displays[0].Should().StartWith("Night —").And.Contain("Hours: 7");
        displays[1].Should().StartWith("Nap —").And.Contain("Hours: 1");
    }

    [Fact]
    public async Task LoadDayAsync_falls_back_to_the_positional_label_for_unnamed_series()
    {
        var cs = new FakeCompletionService();
        var ms = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        var habit = Guid.NewGuid();
        var hoursId = Guid.NewGuid();
        await ms.CreateAsync(new MetricDefinition { Id = hoursId, HabitNodeId = habit, Name = "Hours", Type = MetricType.Integer, SortOrder = 0 });

        var day = new DateOnly(2026, 6, 1);
        var onDay = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        cs.Store[Guid.NewGuid()] = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = onDay,
            CompletedAtMs = new DateTimeOffset(onDay).ToUnixTimeMilliseconds(),
            Values = new List<CompletionValue>
            {
                new() { MetricDefinitionId = hoursId, SeriesIndex = 0, ValueInteger = 7 },
                new() { MetricDefinitionId = hoursId, SeriesIndex = 1, ValueInteger = 1 },
            },
        };

        // Only the first series was ever renamed; the second keeps the numbered fallback.
        var sut = new CompletionListViewModel(cs, ms, rd, new FakeDiaryService())
        {
            SeriesLabelsJson = "[\"Night\",\"\"]",
        };
        await sut.LoadDayAsync(habit, day);

        var displays = sut.Rows.Single().FieldDisplays;
#pragma warning disable CA1863 // test-only formatting of a localized series label
        var series2 = string.Format(System.Globalization.CultureInfo.CurrentCulture, Recordum.Presentation.Resources.Strings.HabitBuilder_SeriesRow, 2);
#pragma warning restore CA1863
        displays[0].Should().StartWith("Night —");
        displays[1].Should().StartWith(series2);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
    public async Task LoadDayAsync_reads_a_duration_as_a_clock_even_when_the_metric_has_a_unit()
    {
        // A single recording reads as a duration, not as a decimal: 27000s is "7:30:00" whatever unit
        // the metric declares. The unit governs AGGREGATES — day total, goal, export — where a number
        // is what you compare against a target. Before this, a metric with a unit read "7,5 ore" here
        // and one without read "7:30:00": two layouts for the same fact.
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

        sut.Rows[0].FieldDisplays.Should().ContainSingle().Which.Should().Be("Slept: 7:30:00");
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
    public void Display_is_the_wall_clock_of_the_habit()
    {
        // Estate: Europe/Rome = UTC+2. Col fuso di default (UTC) darebbe 21:30: e' il parametro muto.
        var rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        var at = new DateTime(2026, 7, 10, 21, 30, 0, DateTimeKind.Utc);
        var c = new Completion { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), CompletedAt = at,
            CompletedAtMs = new DateTimeOffset(at).ToUnixTimeMilliseconds() };

        var sut = new CompletionRowViewModel(c, new FakeCompletionService(),
            dayBoundary: new Recordum.Core.Time.DayBoundary(rome, TimeOnly.MinValue));

        sut.Display.Should().Be("2026-07-10 23:30");
    }

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
