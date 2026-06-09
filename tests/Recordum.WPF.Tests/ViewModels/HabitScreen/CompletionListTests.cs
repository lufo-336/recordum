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
        var sut = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData());

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
        var sut = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData());
        await sut.LoadAsync(habitId);

        await sut.Rows[0].DeleteCommand.ExecuteAsync(null);

        cs.DeleteCalls.Should().Contain(c.Id);
        sut.Rows.Should().BeEmpty();
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

        var sut = new CompletionListViewModel(cs, ms, rd);
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

        var sut = new CompletionListViewModel(cs, ms, rd);
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
            Values = new List<CompletionValue> { new() { MetricDefinitionId = moodId, SeriesIndex = 0, ValueInteger = 1 } }, // index 1 = Neutral
        };

        var sut = new CompletionListViewModel(cs, ms, rd);
        await sut.LoadDayAsync(habit, day);

        sut.Rows[0].FieldDisplays.Should().ContainSingle().Which.Should().Be("Mood: Neutral");
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
