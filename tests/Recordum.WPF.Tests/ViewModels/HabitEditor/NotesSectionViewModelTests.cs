using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class NotesSectionViewModelTests
{
    private readonly FakeHabitConfigService _configs = new();
    private readonly IRecentColorStore _recent = new RecentColorStore(new FakeAppSettingService());
    private readonly IFavoriteColorStore _fav = new FavoriteColorStore(new FakeAppSettingService());
    private readonly FakeScreenColorPicker _picker = new();
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action };

    [Fact]
    public async Task LoadAsync_reads_existing_marker_color()
    {
        var sut = new NotesSectionViewModel(_configs, _recent, _fav, _picker);

        await sut.LoadAsync(_node, new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = _node.Id, NoteMarkerColor = "#aabbcc" });

        sut.NoteMarkerColor.Should().Be("#aabbcc");
    }

    [Fact]
    public async Task LoadAsync_defaults_to_amber_when_null()
    {
        var sut = new NotesSectionViewModel(_configs, _recent, _fav, _picker);

        await sut.LoadAsync(_node, new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = _node.Id, NoteMarkerColor = null });

        sut.NoteMarkerColor.Should().Be(NotesSectionViewModel.DefaultNoteMarkerColor);
    }

    [Fact]
    public async Task Changing_NoteMarkerColor_persists_via_config_service()
    {
        var sut = new NotesSectionViewModel(_configs, _recent, _fav, _picker);
        await sut.LoadAsync(_node, new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = _node.Id });

        sut.NoteMarkerColor = "#112233";
        await Task.Delay(20);

        _configs.Store[_node.Id].NoteMarkerColor.Should().Be("#112233");
        sut.Status.Should().Be(EditorStatus.Saved);
    }

    [Fact]
    public void IsApplicable_is_false_for_group()
    {
        var sut = new NotesSectionViewModel(_configs, _recent, _fav, _picker);
        sut.IsApplicable(NodeType.Group, null).Should().BeFalse();
    }

    [Fact]
    public void IsApplicable_is_true_for_any_habit()
    {
        var sut = new NotesSectionViewModel(_configs, _recent, _fav, _picker);
        sut.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeTrue();
        sut.IsApplicable(NodeType.Habit, HabitType.State).Should().BeTrue();
    }

    [Fact]
    public async Task AddFavorite_then_load_exposes_the_color()
    {
        var settings = new FakeAppSettingService();
        var favs = new FavoriteColorStore(settings);
        var sut = new NotesSectionViewModel(new FakeHabitConfigService(), new RecentColorStore(new FakeAppSettingService()), favs, new FakeScreenColorPicker());

        await sut.AddFavoriteCommand.ExecuteAsync("#3b82f6");

        sut.FavoriteColors.Should().Contain("#3b82f6");
    }
}
