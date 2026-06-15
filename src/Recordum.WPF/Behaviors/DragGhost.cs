using System.Windows;
using System.Windows.Media;

namespace Recordum.WPF.Behaviors;

/// <summary>
/// Ghost di trascinamento: uno snapshot della card di riga su un Popup topmost che segue il cursore.
/// Singolo scopo: rendere il ghost e seguirlo. Nessuna logica di dominio.
/// </summary>
public sealed partial class DragGhost
{
    /// <summary>Converte pixel fisici dello schermo in unità WPF (DIU) data la matrice
    /// <c>TransformFromDevice</c> della sorgente di presentazione.</summary>
    internal static Point PhysicalToDip(Point physical, Matrix transformFromDevice)
        => transformFromDevice.Transform(physical);
}
