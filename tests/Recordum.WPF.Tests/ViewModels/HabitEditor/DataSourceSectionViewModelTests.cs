using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class DataSourceSectionViewModelTests
{
    private readonly FakeAppSettingService _settings = new();
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter };

    [Fact]
    public async Task Toggle_on_persists_true_to_AppSetting()
    {
        var sut = new DataSourceSectionViewModel(_settings);
        await sut.LoadAsync(_node, cfg: null);

        sut.IsEnabled = true;
        await Task.Delay(20);

        (await _settings.GetAsync(DataSourceSectionViewModel.KeyFor(_node.Id))).Should().Be("true");
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public async Task Toggle_off_persists_false()
    {
        await _settings.SetAsync(DataSourceSectionViewModel.KeyFor(_node.Id), "true");
        var sut = new DataSourceSectionViewModel(_settings);
        await sut.LoadAsync(_node, cfg: null);
        sut.IsEnabled.Should().BeTrue();

        sut.IsEnabled = false;
        await Task.Delay(20);

        (await _settings.GetAsync(DataSourceSectionViewModel.KeyFor(_node.Id))).Should().Be("false");
    }

    [Fact]
    public void IsApplicable_excludes_Journal_and_Group()
    {
        var sut = new DataSourceSectionViewModel(_settings);
        sut.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.Counter).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.Resistance).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.State).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.Journal).Should().BeFalse();
        sut.IsApplicable(NodeType.Group, null).Should().BeFalse();
    }
}
