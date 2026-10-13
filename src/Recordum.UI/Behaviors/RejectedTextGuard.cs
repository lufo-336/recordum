using Avalonia;
using Avalonia.Controls;
using Avalonia.Reactive;

namespace Recordum.UI.Behaviors;

/// <summary>Ruling R10: un testo rifiutato dal binding (altro separatore, raggruppamento) svuota il valore nel
/// ViewModel, cosi' cio' che si salva e' cio' che si vede: Salva/Lancia non usano il valore vecchio mentre la
/// casella mostra l'errore. Il testo digitato resta nella casella: svuotare il ViewModel lo riscriverebbe, quindi
/// lo si rimette (il nuovo rifiuto non cambia piu' niente). Il ViewModel e' il DataContext di <paramref name="owner"/>.</summary>
public static class RejectedTextGuard
{
    public static void Watch<TVm>(TextBox box, Control owner, Action<TVm> clear) where TVm : class
    {
        var restoring = false;
        box.GetObservable(DataValidationErrors.HasErrorsProperty).Subscribe(new AnonymousObserver<bool>(hasErrors =>
        {
            if (!hasErrors || restoring || owner.DataContext is not TVm vm) return;
            var rejected = box.Text;
            restoring = true;
            try
            {
                clear(vm);
                if (box.Text != rejected) box.Text = rejected;
            }
            finally { restoring = false; }
        }));
    }
}
