using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using FluentAssertions;
using Recordum.UI.Controls;
using Xunit;

namespace Recordum.UI.Tests.Controls;

/// <summary>GIO-8: ogni voce di un elenco puntato si rende col pallino quando l'editor non ha il fuoco
/// (es. nota aperta nella colonna Note); con il fuoco resta grezza la sola riga del cursore.</summary>
public class DiaryEditorBulletTests
{
    private static void Pump()
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs();
    }

    private static int BulletCount(DiaryEditor editor)
    {
        var textView = editor.GetLogicalDescendants().OfType<TextEditor>().Single().TextArea.TextView;
        textView.EnsureVisualLines();
        return textView.VisualLines
            .SelectMany(vl => vl.Elements)
            .OfType<FormattedTextElement>()
            .Count(e => e.DocumentLength == 2);
    }

    [AvaloniaTheory]
    [InlineData("- 14 km\n- ritmo 5:40", 2)]
    [InlineData("- a\n- b\n- c", 3)]
    public void Unfocused_editor_renders_a_bullet_element_for_every_list_item(string markdown, int expectedBullets)
    {
        var editor = new DiaryEditor { Markdown = markdown };
        var window = new Window { Width = 500, Height = 300, Content = editor };
        window.Show();
        Pump();

        BulletCount(editor).Should().Be(expectedBullets);
        window.Close();
    }

    [AvaloniaFact]
    public void Focused_editor_keeps_only_the_caret_line_raw_and_renders_it_when_focus_leaves()
    {
        var editor = new DiaryEditor { Markdown = "- 14 km\n- ritmo 5:40" };
        var other = new Button { Content = "Altro" };
        var window = new Window
        {
            Width = 500,
            Height = 300,
            Content = new StackPanel { Children = { editor, other } },
        };
        window.Show();
        Pump();

        BulletCount(editor).Should().Be(2);

        var textEditor = editor.GetLogicalDescendants().OfType<TextEditor>().Single();
        textEditor.TextArea.Focus();
        editor.CaretOffset = editor.Markdown.Length;
        Pump();

        BulletCount(editor).Should().Be(1, "con il fuoco sulla seconda riga, solo la prima ha il pallino");

        other.Focus();
        Pump();

        BulletCount(editor).Should().Be(2, "perso il fuoco, anche l'ultima riga torna resa col pallino");
        window.Close();
    }
}
