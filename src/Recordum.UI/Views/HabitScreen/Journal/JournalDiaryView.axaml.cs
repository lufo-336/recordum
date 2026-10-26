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

    // async void: e' un gestore di evento. InsertPhotosAsync riporta gli errori per file in ErrorMessage.
    private async void OnInsertImage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not JournalQuickEntryViewModel vm) return;
        try
        {
            if (await vm.InsertPhotosAsync(Editor.Markdown, Editor.CaretOffset, CancellationToken.None).ConfigureAwait(true) is { } r)
            {
                Editor.ReplaceText(r.Text, r.Caret); // un passo di annulla, il cursore dopo i riferimenti
                FocusEditor();
            }
        }
#pragma warning disable CA1031 // un'eccezione in un async void chiuderebbe l'app: si traccia, il testo resta
        catch (Exception ex)
#pragma warning restore CA1031
        {
            System.Diagnostics.Debug.WriteLine($"JournalDiaryView insert image failed: {ex}");
        }
    }
}
