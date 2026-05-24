using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class ResetTimeResolverTests
{
    private static ResetTimeResolver CreateResolver(DbFixture fx)
    {
        var settings = new AppSettingService(fx.Factory, fx.Clock, NullLogger<AppSettingService>.Instance);
        return new ResetTimeResolver(fx.Factory, settings, NullLogger<ResetTimeResolver>.Instance);
    }

    [Fact]
    public async Task Habit_with_own_reset_time_returns_it()
    {
        var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = habitId,
                NodeType = NodeType.Habit,
                HabitType = HabitType.Action,
                Name = "h",
                ResetTime = new TimeOnly(5, 0),
            });
            await db.SaveChangesAsync();
        }
        var resolver = CreateResolver(fx);

        var result = await resolver.GetEffectiveResetTimeAsync(habitId);

        result.Should().Be(new TimeOnly(5, 0));
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Habit_without_walks_to_parent()
    {
        var fx = new DbFixture();
        var groupId = Guid.NewGuid();
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = groupId,
                NodeType = NodeType.Group,
                Name = "g",
                ResetTime = new TimeOnly(7, 30),
            });
            db.HabitNodes.Add(new HabitNode
            {
                Id = habitId,
                ParentId = groupId,
                NodeType = NodeType.Habit,
                HabitType = HabitType.Action,
                Name = "h",
                ResetTime = null,
            });
            await db.SaveChangesAsync();
        }
        var resolver = CreateResolver(fx);

        var result = await resolver.GetEffectiveResetTimeAsync(habitId);

        result.Should().Be(new TimeOnly(7, 30));
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task None_in_chain_falls_back_to_app_setting()
    {
        var fx = new DbFixture();
        var groupId = Guid.NewGuid();
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = groupId, NodeType = NodeType.Group, Name = "g", ResetTime = null });
            db.HabitNodes.Add(new HabitNode
            {
                Id = habitId,
                ParentId = groupId,
                NodeType = NodeType.Habit,
                HabitType = HabitType.Action,
                Name = "h",
                ResetTime = null,
            });
            await db.SaveChangesAsync();
        }
        var settings = new AppSettingService(fx.Factory, fx.Clock, NullLogger<AppSettingService>.Instance);
        await settings.SetAsync("daily_reset_time", "04:00");
        var resolver = new ResetTimeResolver(fx.Factory, settings, NullLogger<ResetTimeResolver>.Instance);

        var result = await resolver.GetEffectiveResetTimeAsync(habitId);

        result.Should().Be(new TimeOnly(4, 0));
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Default_when_no_setting_is_midnight()
    {
        var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = habitId,
                NodeType = NodeType.Habit,
                HabitType = HabitType.Action,
                Name = "h",
                ResetTime = null,
            });
            await db.SaveChangesAsync();
        }
        var resolver = CreateResolver(fx);

        var result = await resolver.GetEffectiveResetTimeAsync(habitId);

        result.Should().Be(new TimeOnly(0, 0));
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Unknown_habit_returns_app_setting_fallback()
    {
        var fx = new DbFixture();
        var resolver = CreateResolver(fx);

        var result = await resolver.GetEffectiveResetTimeAsync(Guid.NewGuid());

        result.Should().Be(new TimeOnly(0, 0));
        await fx.DisposeAsync();
    }
}
