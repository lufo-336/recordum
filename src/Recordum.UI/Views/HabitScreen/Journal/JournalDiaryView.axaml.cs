using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;

namespace Recordum.UI.Views.HabitScreen.Journal;

/// <summary>La colonna Diario in modalita' diario (decisione 4): il <c>DiaryEditor</c> legato a <c>ActiveText</c>, con il
/// menu <c>#</c> di <see cref="TagMenuController"/>; "Inserisci immagine…" mette i riferimenti al cursore.</summary>
public partial class JournalDiaryView : UserControl
{
    private readonly TagMenuController _tagMenu;

    public JournalDiaryView()
    {
        InitializeComponent();
        _tagMenu = new TagMenuController(TagMenu, InlineList, Editor,
            () => Editor.Markdown,
            () => Editor.CaretOffset,
            Editor.ReplaceText,
            Editor.CaretBounds,
            FocusEditor);
        DataContextChanged += (_, _) => _tagMenu.ViewModel = DataContext as JournalQuickEntryViewModel;
        Editor.TextOrCaretChanged += (_, _) => _tagMenu.OnTextOrCaretChanged();
    }

    private void FocusEditor() => Editor.GetVisualDescendants().OfType<TextArea>().FirstOrDefault()?.Focus();

    // async void: e' un gestore di evento. InsertPhotosAsync riporta gli errori per file in ErrorMessage. Mentre l'import
    // gira (IsInserting) la superficie non cambia (Modifica, Annulla, giorno aspettano), ma il testo si'.
    private async void OnInsertImage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not JournalQuickEntryViewModel { IsInserting: false } vm) return; // un inserimento alla volta
        var caret = Editor.CaretOffset;
        try
        {
            if (await vm.InsertPhotosAsync(Editor.Markdown, caret, CancellationToken.None).ConfigureAwait(true) is not { } r) return;
            // Il testo scritto durante l'import resta: dal risultato si prendono solo le righe di riferimento e si mettono
            // al cursore di adesso (un passo di annulla).
            var refs = r.Text[caret..r.Caret].TrimStart('\n');
            var (text, at) = JournalQuickEntryViewModel.InsertReferenceLines(Editor.Markdown, Editor.CaretOffset, refs);
            Editor.ReplaceText(text, at);
            FocusEditor();
        }
#pragma warning disable CA1031 // un'eccezione in un async void chiuderebbe l'app: si dice, il testo resta
        catch (Exception ex)
#pragma warning restore CA1031
        {
            System.Diagnostics.Debug.WriteLine($"JournalDiaryView insert image failed: {ex}");
            vm.ErrorMessage = Recordum.Presentation.Resources.Strings.Diario_Editor_InsertImageFailed;
        }
    }
}
