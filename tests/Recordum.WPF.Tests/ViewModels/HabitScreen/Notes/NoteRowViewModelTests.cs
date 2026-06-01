using System.Globalization;
using FluentAssertions;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Notes;

public class NoteRowViewModelTests
{
    [Fact]
    public void Summary_returns_first_nonblank_line()
    {
        var n = new Note { Id = Guid.NewGuid(), BodyMd = "\n# Heading\nbody body body" };
        var sut = new NoteRowViewModel(n);
        sut.Summary.Should().Be("# Heading");
    }

    [Fact]
    public void Summary_truncates_long_first_line_at_40_chars_with_ellipsis()
    {
        var n = new Note { Id = Guid.NewGuid(), BodyMd = new string('a', 100) };
        var sut = new NoteRowViewModel(n);
        sut.Summary.Should().HaveLength(41);
        sut.Summary.Should().EndWith("…");
    }

    [Fact]
    public void Summary_empty_body_returns_untitled_marker()
    {
        var n = new Note { Id = Guid.NewGuid(), BodyMd = "" };
        var sut = new NoteRowViewModel(n);
        sut.Summary.Should().Be(Recordum.WPF.Resources.Strings.Notes_Untitled);
    }

    [Fact]
    public void UpdatedAtDisplay_formatted_yyyy_MM_dd_HH_mm()
    {
        var n = new Note
        {
            Id = Guid.NewGuid(),
            BodyMd = "x",
            UpdatedAtMs = new DateTimeOffset(2026, 5, 22, 10, 42, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        };
        var sut = new NoteRowViewModel(n);
        sut.UpdatedAtDisplay.Should().Be("2026-05-22 10:42");
    }

    [Fact]
    public void Refresh_raises_PropertyChanged_for_Summary_and_UpdatedAtDisplay()
    {
        var n = new Note { Id = Guid.NewGuid(), BodyMd = "before", UpdatedAtMs = 0 };
        var sut = new NoteRowViewModel(n);
        var changes = new List<string?>();
        sut.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        n.BodyMd = "after";
        sut.Refresh();

        changes.Should().Contain(["Summary", "UpdatedAtDisplay"]);
        sut.Summary.Should().Be("after");
    }

    [Fact]
    public void DateBadge_shows_general_label_when_no_target_date()
    {
        var row = new NoteRowViewModel(new Note { BodyMd = "x", TargetDate = null });
        row.DateBadge.Should().Be(Recordum.WPF.Resources.Strings.Notes_Badge_General);
    }

    [Fact]
    public void DateBadge_shows_the_day_when_target_date_set()
    {
        var date = new DateOnly(2026, 6, 1);
        var row = new NoteRowViewModel(new Note { BodyMd = "x", TargetDate = date });
        row.DateBadge.Should().Be(date.ToString("d MMM", CultureInfo.CurrentCulture));
    }
}
