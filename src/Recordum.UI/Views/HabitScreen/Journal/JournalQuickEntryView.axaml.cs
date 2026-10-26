using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;

namespace Recordum.UI.Views.HabitScreen.Journal;

/// <summary>Il composer della nota veloce. Il menu <c>#</c> sotto il cursore della TextBox e' di
/// <see cref="TagMenuController"/>, lo stesso del diario.</summary>
public partial class JournalQuickEntryView : UserControl
{
    private readonly TagMenuController _tagMenu;

    public JournalQuickEntryView()
    {
        InitializeComponent();
        _tagMenu = new TagMenuController(TagMenu, InlineList, DraftBox,
            () => DraftBox.Text ?? string.Empty,
            () => DraftBox.CaretIndex,
            (text, caret) => { DraftBox.Text = text; DraftBox.CaretIndex = caret; },
            CaretRect,
            () => DraftBox.Focus());
        DataContextChanged += (_, _) => _tagMenu.ViewModel = DataContext as JournalQuickEntryViewModel;
        DraftBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty || e.Property == TextBox.CaretIndexProperty) _tagMenu.OnTextOrCaretChanged();
        };
    }

    // Il menu si ancora alla riga del cursore, non al fondo dell'intera TextBox.
    private Rect? CaretRect()
    {
        var presenter = DraftBox.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter?.TextLayout is not { } layout) return null;
        var caret = layout.HitTestTextPosition(DraftBox.CaretIndex);
        return presenter.TranslatePoint(caret.TopLeft, DraftBox) is { } p ? new Rect(p.X, p.Y, 1, caret.Height) : null;
    }
}
