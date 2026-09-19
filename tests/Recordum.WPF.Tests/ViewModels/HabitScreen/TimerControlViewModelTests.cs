using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class TimerControlViewModelTests
{
    private static TimerControlViewModel CreateSut(out FakeActiveTimerService ats, out FakeHabitConfigService configs, out FakeClock clock)
    {
        ats = new FakeActiveTimerService();
        configs = new FakeHabitConfigService();
        clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        return new TimerControlViewModel(ats, configs, clock);
    }

    [Fact]
    public async Task SetContextAsync_no_timer_in_config_marks_not_applicable()
    {
        var sut = CreateSut(out _, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };

        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = false });

        sut.IsApplicable.Should().BeFalse();
        sut.StartCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task SetContextAsync_with_timer_marks_applicable_and_allows_start()
    {
        var sut = CreateSut(out _, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };

        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch });

        sut.IsApplicable.Should().BeTrue();
        sut.StartCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task StartCommand_starts_timer_with_mode_from_config()
    {
        var sut = CreateSut(out var ats, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Countdown });

        await sut.StartCommand.ExecuteAsync(null);

        ats.StartCalls.Should().ContainSingle().Which.Should().Be((node.Id, TimerMode.Countdown));
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task StopCommand_stops_timer_and_resets_state()
    {
        var sut = CreateSut(out var ats, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch });
        await sut.StartCommand.ExecuteAsync(null);

        await sut.StopCommand.ExecuteAsync(null);

        ats.StopCalls.Should().ContainSingle().Which.Should().Be(node.Id);
        sut.IsActive.Should().BeFalse();
        sut.ElapsedSeconds.Should().Be(0);
    }

    [Fact]
    public async Task PauseCommand_then_ResumeCommand_toggles_state()
    {
        var sut = CreateSut(out var ats, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch });
        await sut.StartCommand.ExecuteAsync(null);

        await sut.PauseCommand.ExecuteAsync(null);
        ats.PauseCalls.Should().ContainSingle();
        sut.IsPaused.Should().BeTrue();
        sut.ResumeCommand.CanExecute(null).Should().BeTrue();

        await sut.ResumeCommand.ExecuteAsync(null);
        ats.ResumeCalls.Should().ContainSingle();
        sut.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task SetContextAsync_session_mode_populates_Segments()
    {
        var sut = CreateSut(out _, out var configs, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        configs.Segments.Add(new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "Work", SortOrder = 0 });
        configs.Segments.Add(new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "Rest", SortOrder = 1 });

        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Session });

        sut.Segments.Should().HaveCount(2);
        sut.Segments[0].Name.Should().Be("Work");
    }

    [Fact]
    public async Task NextSegmentCommand_advances_through_segments_then_stops()
    {
        var sut = CreateSut(out var ats, out var configs, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        configs.Segments.Add(new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "A", SortOrder = 0 });
        configs.Segments.Add(new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "B", SortOrder = 1 });
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Session });
        await sut.StartCommand.ExecuteAsync(null);

        await sut.NextSegmentCommand.ExecuteAsync(null);
        sut.SegmentIndex.Should().Be(1);
        ats.NextSegmentCalls.Should().ContainSingle();

        // Last segment → NextSegment stops the timer.
        await sut.NextSegmentCommand.ExecuteAsync(null);
        sut.IsActive.Should().BeFalse();
        ats.StopCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task NextSegmentCommand_disabled_for_non_session_mode()
    {
        var sut = CreateSut(out _, out _, out _);
        var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter };
        await sut.SetContextAsync(node, new HabitConfig { HabitNodeId = node.Id, HasIntegratedTimer = true, TimerMode = TimerMode.Stopwatch });
        await sut.StartCommand.ExecuteAsync(null);

        sut.NextSegmentCommand.CanExecute(null).Should().BeFalse();
    }
}
