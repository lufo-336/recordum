using System.Text.Json;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class SchedulingDayPickerTests
{
    private readonly FakeHabitConfigService _configs = new();
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action };

    private async Task<SchedulingSectionViewModel> CreateSutAsync(string? scheduleDaysJson = null)
    {
        var cfg = new HabitConfig
        {
            Id = Guid.NewGuid(),
            HabitNodeId = _node.Id,
            ScheduleType = ScheduleType.SpecificDays,
            ScheduleDaysJson = scheduleDaysJson
        };
        var sut = new SchedulingSectionViewModel(_configs);
        await sut.LoadAsync(_node, cfg);
        return sut;
    }

    [Fact]
    public async Task LoadAsync_with_two_days_json_selects_correct_day_options()
    {
        // "Mon" and "Wed" should be selected, the rest not.
        var sut = await CreateSutAsync("""["Mon","Wed"]""");

        var byKey = sut.DayOptions.ToDictionary(d => d.DayKey, d => d.IsSelected);
        byKey["Mon"].Should().BeTrue();
        byKey["Wed"].Should().BeTrue();
        byKey["Tue"].Should().BeFalse();
        byKey["Thu"].Should().BeFalse();
        byKey["Fri"].Should().BeFalse();
        byKey["Sat"].Should().BeFalse();
        byKey["Sun"].Should().BeFalse();
    }

    [Fact]
    public async Task DayOptions_has_exactly_seven_entries()
    {
        var sut = await CreateSutAsync();
        sut.DayOptions.Should().HaveCount(7);
    }

    [Fact]
    public async Task Toggling_an_unselected_day_adds_it_to_ScheduleDaysJson_and_persists()
    {
        // Start with Mon + Wed selected.
        var sut = await CreateSutAsync("""["Mon","Wed"]""");

        // Toggle Fri on.
        var fri = sut.DayOptions.Single(d => d.DayKey == "Fri");
        fri.IsSelected = true;
        await Task.Delay(30);

        // ScheduleDaysJson should now contain Mon, Wed, Fri (order-independent).
        var stored = JsonSerializer.Deserialize<string[]>(_configs.Store[_node.Id].ScheduleDaysJson!)!;
        stored.Should().BeEquivalentTo(new[] { "Mon", "Wed", "Fri" });
    }

    [Fact]
    public async Task Toggling_a_selected_day_off_removes_it_from_ScheduleDaysJson()
    {
        var sut = await CreateSutAsync("""["Mon","Tue"]""");

        var mon = sut.DayOptions.Single(d => d.DayKey == "Mon");
        mon.IsSelected = false;
        await Task.Delay(30);

        var stored = _configs.Store[_node.Id].ScheduleDaysJson;
        // Only Tue should remain.
        var arr = JsonSerializer.Deserialize<string[]>(stored!)!;
        arr.Should().BeEquivalentTo(new[] { "Tue" });
    }

    [Fact]
    public async Task LoadAsync_with_null_json_leaves_all_days_unselected()
    {
        var sut = await CreateSutAsync(null);
        sut.DayOptions.Should().AllSatisfy(d => d.IsSelected.Should().BeFalse());
    }
}
