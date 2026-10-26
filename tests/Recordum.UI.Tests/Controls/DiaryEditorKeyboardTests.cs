using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using AvaloniaEdit;
using FluentAssertions;
using Recordum.UI.Controls;
using Xunit;

namespace Recordum.UI.Tests.Controls;

/// <summary>Le scorciatoie dell'editor (decisione 9, Review Focus 3), premute come tasti veri sulla finestra.</summary>
public class DiaryEditorKeyboardTests
{
    private static (DiaryEditor Editor, Window Window) Show(string text, int caret, int selectionLength = 0)
    {
        var control = new DiaryEditor { Markdown = text };
        var window = new Window { Width = 500, Height = 300, Content = control };
        window.Show();
        var textEditor = control.GetLogicalDescendants().OfType<TextEditor>().Single();
        textEditor.TextArea.Focus();
        if (selectionLength > 0) textEditor.Select(caret, selectionLength);
        else control.CaretOffset = caret;
        Dispatcher.UIThread.RunJobs();
        return (control, window);
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static string Text(DiaryEditor editor) => editor.Markdown.Replace("\r\n", "\n", StringComparison.Ordinal);

    [AvaloniaTheory]
    [InlineData(PhysicalKey.B, "**ciao**")]
    [InlineData(PhysicalKey.I, "*ciao*")]
    public void Ctrl_B_and_Ctrl_I_wrap_the_selection(PhysicalKey key, string expected)
    {
        var (editor, window) = Show("ciao", 0, selectionLength: 4);

        Press(window, key, RawInputModifiers.Control);

        Text(editor).Should().Be(expected);
    }

    [AvaloniaTheory]
    [InlineData(PhysicalKey.B, "****", 2)]
    [InlineData(PhysicalKey.I, "**", 1)]
    public void Without_a_selection_the_pair_goes_in_with_the_caret_between(PhysicalKey key, string expected, int caret)
    {
        var (editor, window) = Show(string.Empty, 0);

        Press(window, key, RawInputModifiers.Control);

        Text(editor).Should().Be(expected);
        editor.CaretOffset.Should().Be(caret);
    }

    [AvaloniaTheory]
    [InlineData("- uno", "- uno\n- ")]
    [InlineData("* uno", "* uno\n* ")]
    [InlineData("3. tre", "3. tre\n4. ")]
    public void Enter_on_a_list_item_opens_the_next_one(string line, string expected)
    {
        var (editor, window) = Show(line, line.Length);

        Press(window, PhysicalKey.Enter);

        Text(editor).Should().Be(expected);
        editor.CaretOffset.Should().Be(editor.Markdown.Length, "il cursore va dopo il marcatore nuovo");
    }

    [AvaloniaFact]
    public void Enter_on_an_empty_item_empties_the_line_and_ends_the_list()
    {
        var (editor, window) = Show("- uno\n- ", "- uno\n- ".Length);

        Press(window, PhysicalKey.Enter);

        Text(editor).Should().Be("- uno\n");
        editor.CaretOffset.Should().Be(editor.Markdown.Length);
    }

    [AvaloniaFact]
    public void Enter_outside_a_list_is_a_plain_line_break()
    {
        var (editor, window) = Show("ciao", 4);

        Press(window, PhysicalKey.Enter);

        Text(editor).Should().Be("ciao\n");
    }

    [AvaloniaFact]
    public void A_key_the_host_handles_in_tunnel_never_reaches_the_editor()
    {
        // Il contratto per il menu '#' del Task 7: Invio con il menu aperto sceglie e non va a capo.
        var (editor, window) = Show("- uno", 5);
        editor.AddHandler(InputElement.KeyDownEvent, (_, e) => { if (e.Key == Key.Enter) e.Handled = true; }, RoutingStrategies.Tunnel);

        Press(window, PhysicalKey.Enter);

        Text(editor).Should().Be("- uno");
    }

    [AvaloniaFact]
    public void Read_only_ignores_the_shortcuts()
    {
        var (editor, window) = Show("ciao", 0, selectionLength: 4);
        editor.IsReadOnly = true;

        Press(window, PhysicalKey.B, RawInputModifiers.Control);

        Text(editor).Should().Be("ciao");
    }

    private static void Undo(Window window) => Press(window, PhysicalKey.Z, RawInputModifiers.Control);

    [AvaloniaFact]
    public void Text_set_through_Markdown_cannot_be_undone()
    {
        // Il binding porta la voce da modificare, la bozza o il testo dopo Salva: Ctrl+Z non deve riportare il testo di prima.
        var (editor, window) = Show("bozza", 0);
        editor.Markdown = "testo della voce";

        Undo(window);

        Text(editor).Should().Be("testo della voce");
    }

    [AvaloniaFact]
    public void ReplaceText_can_be_undone()
    {
        var (editor, window) = Show("ciao #pr", 8);
        editor.ReplaceText("ciao #pranzo ", 13);

        Undo(window);

        Text(editor).Should().Be("ciao #pr");
    }

    [AvaloniaFact]
    public void One_undo_takes_back_a_whole_Ctrl_B()
    {
        var (editor, window) = Show("ciao", 0, selectionLength: 4);
        Press(window, PhysicalKey.B, RawInputModifiers.Control);

        Undo(window);

        Text(editor).Should().Be("ciao");
    }

    [AvaloniaFact]
    public void One_undo_takes_back_the_new_list_item()
    {
        var (editor, window) = Show("- uno", 5);
        Press(window, PhysicalKey.Enter);

        Undo(window);

        Text(editor).Should().Be("- uno");
    }

    [AvaloniaFact]
    public void ReplaceText_sets_text_and_caret_and_raises_the_change()
    {
        var (editor, _) = Show("ciao #pr", 8);
        var raised = 0;
        editor.TextOrCaretChanged += (_, _) => raised++;

        editor.ReplaceText("ciao #pranzo ", 13);

        editor.Markdown.Should().Be("ciao #pranzo ");
        editor.CaretOffset.Should().Be(13);
        raised.Should().BeGreaterThan(0);
    }
}
