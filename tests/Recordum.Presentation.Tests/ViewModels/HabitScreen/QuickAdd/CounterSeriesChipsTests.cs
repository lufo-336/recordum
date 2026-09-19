using System.Globalization;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.QuickAdd;

public class CounterSeriesChipsTests
{
    private static async Task<(CounterSeriesQuickAddViewModel vm, HabitNode node, FakeHabitConfigService configs)>
        LoadRepeatableAsync(bool repeatable = true, string? seriesLabelsJson = null)
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var configs = new FakeHabitConfigService();
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        var reps = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "reps", Type = MetricType.Integer, SortOrder = 0 };
        metrics.Store[reps.Id] = reps;

        var vm = new CounterSeriesQuickAddViewModel(cs, metrics, new FakeReferenceData(), new FakeClock(), new FakeActiveTimerService(), configs)
        {
            Node = node,
        };
        vm.SetRegistrationConfig(repeatable, seriesLabelsJson);
        await vm.LoadMetricsAsync();
        return (vm, node, configs);
    }

    [Fact]
    public async Task Repeatable_load_builds_one_named_chip_from_labels_with_fallback()
    {
        var (vm, node, _) = await LoadRepeatableAsync(seriesLabelsJson: "[\"Notte\",\"Riposino\"]");
        vm.IsRepeatable.Should().BeTrue();
        vm.Chips.Should().HaveCount(1);
        vm.Chips[0].Name.Should().Be("Notte");
        vm.Chips[0].IsSelected.Should().BeTrue();

        vm.AddSeriesCommand.Execute(null);
        vm.Chips.Should().HaveCount(2);
        vm.Chips[1].Name.Should().Be("Riposino");     // second label
        vm.SelectedSeriesIndex.Should().Be(1);          // the new series is selected
        vm.Series[0].IsVisibleRow.Should().BeFalse();   // only the selected row shows
        vm.Series[1].IsVisibleRow.Should().BeTrue();

        vm.AddSeriesCommand.Execute(null);
        vm.Chips[2].Name.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.HabitScreen_QuickAdd_SeriesFallback, 3));
        _ = node;
    }

    [Fact]
    public async Task Non_repeatable_load_shows_no_chips_and_a_single_visible_row()
    {
        var (vm, _, _) = await LoadRepeatableAsync(repeatable: false);
        vm.IsRepeatable.Should().BeFalse();
        vm.Chips.Should().BeEmpty();
        vm.Series.Should().HaveCount(1);
        vm.Series[0].IsVisibleRow.Should().BeTrue();
    }

    [Fact]
    public async Task Renaming_a_chip_persists_the_labels_json()
    {
        var (vm, node, configs) = await LoadRepeatableAsync(seriesLabelsJson: null);
        var chip = vm.Chips[0];
        vm.BeginRenameChipCommand.Execute(chip);
        chip.IsEditing.Should().BeTrue();
        chip.PendingName = "Notte";
        await vm.CommitRenameChipCommand.ExecuteAsync(chip);

        chip.IsEditing.Should().BeFalse();
        chip.Name.Should().Be("Notte");
        (await configs.GetAsync(node.Id))!.SeriesLabelsJson.Should().Be("[\"Notte\"]");
    }

    [Fact]
    public async Task Selecting_a_chip_switches_the_visible_row()
    {
        var (vm, _, _) = await LoadRepeatableAsync();
        vm.AddSeriesCommand.Execute(null);
        vm.SelectSeriesCommand.Execute(vm.Chips[0]);

        vm.SelectedSeriesIndex.Should().Be(0);
        vm.Series[0].IsVisibleRow.Should().BeTrue();
        vm.Series[1].IsVisibleRow.Should().BeFalse();
    }

    [Fact]
    public async Task Removing_a_non_last_row_keeps_the_surviving_labels_aligned()
    {
        var (vm, _, _) = await LoadRepeatableAsync(seriesLabelsJson: "[\"Notte\",\"Riposino\"]");
        vm.AddSeriesCommand.Execute(null);
        vm.Chips.Should().HaveCount(2);

        vm.RemoveSeriesCommand.Execute(vm.Series[0]);

        vm.Chips.Should().ContainSingle();
        vm.Chips[0].Name.Should().Be("Riposino");
    }
}
