using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Recordum.UI.Views.Rows;

/// <summary>Aggancia il menu della riga alla prima richiesta (tasto destro, tasto menu, Shift+F10)
/// invece che alla costruzione: la risorsa <c>RowMenu</c> del dizionario XAML nasce solo quando la si
/// chiede. Dalla seconda volta è il <see cref="ContextMenu"/> agganciato a gestire la richiesta.</summary>
internal static class RowMenu
{
    public static void AttachOnDemand(Control owner, Control host) =>
        host.ContextRequested += (_, e) =>
        {
            if (host.ContextMenu is not null) return;
            // Nella colonna Panoramica di un gruppo le righe non hanno menu: lo dà la Home.
            if (owner.FindAncestorOfType<HabitScreen.Group.PanoramicaColumnView>() is not null) { e.Handled = true; return; }
            host.ContextMenu = (ContextMenu)owner.Resources["RowMenu"]!;
            host.ContextMenu.Open(host);
            e.Handled = true;
        };
}
