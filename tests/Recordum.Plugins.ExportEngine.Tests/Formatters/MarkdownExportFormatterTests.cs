using System.Text;
using Recordum.Core.Enums;
using Recordum.Plugins.ExportEngine.Formatters;

namespace Recordum.Plugins.ExportEngine.Tests.Formatters;

public sealed class MarkdownExportFormatterTests
{
    [Fact]
    public void Write_contains_title_summary_and_completion_table()
    {
        var f = new MarkdownExportFormatter();
        Assert.Equal(ExportFormat.Markdown, f.Format);
        var md = Encoding.UTF8.GetString(f.Write(SampleModel.One()));
        Assert.Contains("# Run", md, StringComparison.Ordinal);
        Assert.Contains("Current streak", md, StringComparison.Ordinal);
        Assert.Contains("| Date | Value | Metric |", md, StringComparison.Ordinal);
        Assert.Contains("felt good", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_prints_the_goal_progress()
    {
        var md = Encoding.UTF8.GetString(new MarkdownExportFormatter().Write(SampleModel.One()));
        Assert.Contains("- Goal: 12 / 20", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_prints_the_goal_unit_when_the_goal_tracks_a_metric()
    {
        var model = SampleModel.One();
        var habit = model.Habits[0];
        var withUnit = habit with { Summary = habit.Summary with { GoalUnit = "hours" } };
        var md = Encoding.UTF8.GetString(new MarkdownExportFormatter().Write(model with { Habits = [withUnit] }));

        Assert.Contains("- Goal: 12 / 20 hours", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_omits_the_goal_line_for_a_habit_without_one()
    {
        // SampleModel.WithAbstinencePeriods carries a null goal: no empty "Goal: /" line.
        var md = Encoding.UTF8.GetString(new MarkdownExportFormatter().Write(SampleModel.WithAbstinencePeriods()));
        Assert.DoesNotContain("Goal:", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_prints_the_value_with_its_unit()
    {
        var md = Encoding.UTF8.GetString(new MarkdownExportFormatter().Write(SampleModel.One()));
        Assert.Contains("| 2026-05-20 | 5 km | Distance |", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_prints_a_bare_value_when_the_metric_has_no_unit()
    {
        var model = SampleModel.One();
        var habit = model.Habits[0];
        var bare = habit.Completions[0] with { Unit = null };
        var md = Encoding.UTF8.GetString(new MarkdownExportFormatter().Write(
            model with { Habits = [habit with { Completions = [bare] }] }));

        Assert.Contains("| 2026-05-20 | 5 | Distance |", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_renders_periods_table_for_resistance_habit()
    {
        var md = Encoding.UTF8.GetString(new MarkdownExportFormatter().Write(SampleModel.WithAbstinencePeriods()));
        Assert.Contains("| Started | Ended | Duration | Outcome |", md, StringComparison.Ordinal);
        Assert.Contains("| 2026-06-13 09:00 | 2026-07-01 19:00 | 18.10:00:00 | relapsed |", md, StringComparison.Ordinal);
        Assert.Contains("| 2026-07-02 08:00 |  | 0.04:30:00 | open |", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_renders_a_markdown_image_link_per_attachment()
    {
        var md = Encoding.UTF8.GetString(new MarkdownExportFormatter().Write(SampleModel.WithNoteAttachments(null)));
        Assert.Contains("![lake.jpg](attachments/note1/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg)", md, StringComparison.Ordinal);
        Assert.Contains("![missing.png](attachments/note1/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png)", md, StringComparison.Ordinal);
    }
}
