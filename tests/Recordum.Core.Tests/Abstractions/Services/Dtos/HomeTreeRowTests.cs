using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Xunit;

namespace Recordum.Core.Tests.Abstractions.Services.Dtos;

public class HomeTreeRowTests
{
    [Fact]
    public void Record_exposes_new_pin_archive_childcount_heatmap_fields()
    {
        var row = new HomeTreeRow(
            HabitNodeId: Guid.NewGuid(),
            ParentId: null,
            Name: "x",
            ColorHex: "#000000",
            HabitType: HabitType.Action,
            LastCompletionAt: null,
            CurrentStreakCount: 0,
            IsCompletedToday: false,
            PinScope: PinScope.Local,
            ChildCount: 0,
            IsArchived: false,
            CompletionsLast7Days: ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0),
            TagIds: ImmutableArray<Guid>.Empty);
        row.PinScope.Should().Be(PinScope.Local);
        row.CompletionsLast7Days.Length.Should().Be(7);
    }

    [Fact]
    public void Record_exposes_TagIds_array()
    {
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var row = new HomeTreeRow(
            HabitNodeId: Guid.NewGuid(),
            ParentId: null,
            Name: "x",
            ColorHex: "#000000",
            HabitType: HabitType.Action,
            LastCompletionAt: null,
            CurrentStreakCount: 0,
            IsCompletedToday: false,
            PinScope: PinScope.None,
            ChildCount: 0,
            IsArchived: false,
            CompletionsLast7Days: ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0),
            TagIds: ImmutableArray.Create(t1, t2));
        row.TagIds.Should().BeEquivalentTo(new[] { t1, t2 });
    }
}
