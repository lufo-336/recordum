using Avalonia;
using Avalonia.Controls;

namespace Recordum.UI.Views.HabitScreen.Group;

/// <summary>La testa di un gruppo. Sotto i 640 px l'avanzamento va sotto il nome, sotto i 420 i numeri su due colonne
/// (le stesse soglie della testa dell'abitudine).</summary>
public partial class GroupHeadView : UserControl
{
    private const double StackBelow = 640, TwoFiguresBelow = 420;

    public GroupHeadView() => InitializeComponent();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != BoundsProperty || Bounds.Width <= 0) return;
        var stacked = Bounds.Width < StackBelow;
        Grid.SetRow(Progress, stacked ? 1 : 0);
        Grid.SetColumn(Progress, stacked ? 0 : 1);
        // Affiancato: un blocco di 150 px a destra, testi a destra. Sotto il nome: tutta la larghezza, testi a sinistra.
        Progress.Width = stacked ? double.NaN : 150;
        Progress.HorizontalAlignment = stacked ? Avalonia.Layout.HorizontalAlignment.Stretch : Avalonia.Layout.HorizontalAlignment.Right;
        var textSide = stacked ? Avalonia.Layout.HorizontalAlignment.Left : Avalonia.Layout.HorizontalAlignment.Right;
        ProgressCount.HorizontalAlignment = ProgressCaption.HorizontalAlignment = textSide;
        Progress.Margin = stacked ? new Thickness(0, 10, 0, 0) : default;
        NameBlock.Margin = stacked ? default : new Thickness(0, 0, 16, 0);
        Figures.Columns = Bounds.Width < TwoFiguresBelow ? 2 : 4;
    }
}
