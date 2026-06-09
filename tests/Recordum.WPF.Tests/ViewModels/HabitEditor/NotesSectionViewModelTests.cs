using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class NotesSectionViewModelTests
{
    private readonly FakeHabitConfigService _configs = new();
    private readonly IRecentColorStore _recent = new RecentColorStore(new FakeAppSettingService());
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action };

    [Fact]
    public async Task LoadAsync_reads_existing_marker_color()
    {
        var sut = new NotesSectionViewModel(_configs, _recent);

        await sut.LoadAsync(_node, new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = _node.Id, NoteMarkerColor = "#aabbcc" });

        sut.NoteMarkerColor.Should().Be("#aabbcc");
    }

    [Fact]
    public async Task LoadAsync_defaults_to_amber_when_null()
    {
        var sut = new NotesSectionViewModel(_configs, _recent);

        await sut.LoadAsync(_node, new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = _node.Id, NoteMarkerColor = null });

        sut.NoteMarkerColor.Should().Be(NotesSectionViewModel.DefaultNoteMarkerColor);
    }

    [Fact]
    public async Task Changing_NoteMarkerColor_persists_via_config_service()
    {
        var sut = new NotesSectionViewModel(_configs, _recent);
        await sut.LoadAsync(_node, new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = _node.Id });

        sut.NoteMarkerColor = "#112233";
        await Task.Delay(20);

        _configs.Store[_node.Id].NoteMarkerColor.Should().Be("#112233");
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public void IsApplicable_is_false_for_group()
    {
        var sut = new NotesSectionViewModel(_configs, _recent);
        sut.IsApplicable(NodeType.Group, null).Should().BeFalse();
    }

    [Fact]
    public void IsApplicable_is_true_for_any_habit()
    {
        var sut = new NotesSectionViewModel(_configs, _recent);
        sut.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.State).Should().BeTrue();
    }
}
