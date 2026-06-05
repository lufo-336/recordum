using System.Globalization;
using FluentAssertions;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Notes;

public class NoteRowViewModelTests
{
    private static long Ms(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();

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
    public void AnchorDateDisplay_uses_TargetDate_when_present()
    {
        var created = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var row = new NoteRowViewModel(new Note
        {
            CreatedAt = created, UpdatedAtMs = Ms(created),
            TargetDate = new DateOnly(2026, 5, 20),
        });
        row.AnchorDateDisplay.Should().Be(new DateOnly(2026, 5, 20).ToString("d MMM yyyy", CultureInfo.CurrentCulture));
    }

    [Fact]
    public void AnchorDateDisplay_falls_back_to_CreatedAt_when_TargetDate_null()
    {
        var created = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var row = new NoteRowViewModel(new Note { CreatedAt = created, UpdatedAtMs = Ms(created), TargetDate = null });
        row.AnchorDateDisplay.Should().Be(new DateOnly(2026, 6, 1).ToString("d MMM yyyy", CultureInfo.CurrentCulture));
    }

    [Fact]
    public void WasModified_is_false_for_a_never_edited_note()
    {
        var created = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var row = new NoteRowViewModel(new Note { CreatedAt = created, UpdatedAtMs = Ms(created) });
        row.WasModified.Should().BeFalse();
    }

    [Fact]
    public void WasModified_is_true_after_an_edit()
    {
        var created = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var row = new NoteRowViewModel(new Note { CreatedAt = created, UpdatedAtMs = Ms(created.AddMinutes(5)) });
        row.WasModified.Should().BeTrue();
    }
}
