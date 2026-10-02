using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Recordum.Presentation.ViewModels;

namespace Recordum.UI.Behaviors;

/// <summary>Trascinamento delle righe della Home. Si aggancia all'ItemsControl RADICE: gli eventi
/// delle righe annidate nei gruppi arrivano qui risalendo, quindi un solo insieme di gestori copre
/// tutto l'albero. La mappatura zona→intenzione (<see cref="DropResolver"/>) e l'applicazione
/// (<c>HomeViewModel.ApplyDropAsync</c>) stanno in Recordum.Presentation e non cambiano.
/// <para>Differenze rispetto al gemello WPF, tutte dovute all'API di Avalonia 12: l'ancora del
/// trascinamento sono gli argomenti del <b>PointerPressed</b>, non quelli del movimento; non esiste
/// <c>QueryContinueDrag</c> né un'API per l'immagine trascinata, quindi il fantasma (DragGhost +
/// GetCursorPos) sparisce e la riga sorgente si limita a sfumare (IsBeingDragged, D4); lo stato non
/// è più <c>static</c> ma vive in una sessione per ItemsControl agganciato.</para></summary>
public static class TreeDragDropBehavior
{
    /// <summary>Quanto deve muoversi il puntatore prima che una pressione diventi un trascinamento.
    /// Lo legge anche HomeView, per non scambiare un press-drag per un clic di selezione (D4).</summary>
    public const double DragThreshold = 5;

    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, bool>("Enabled", typeof(TreeDragDropBehavior));

    /// <summary>Formato in-process: il ViewModel della riga non viene mai serializzato, viaggia
    /// per riferimento. È il gemello del <c>typeof(HomeRowViewModel)</c> che WPF usava come chiave.</summary>
    public static readonly DataFormat<HomeRowViewModel> RowFormat =
        DataFormat.CreateInProcessFormat<HomeRowViewModel>("recordum.homerow");

    // Una sessione per ItemsControl, non campi statici: due alberi agganciati non si rubano
    // l'indicatore a vicenda. La tabella è debole, così un albero smontato non resta vivo.
    private static readonly ConditionalWeakTable<ItemsControl, DragSession> Sessions = new();

    static TreeDragDropBehavior() => EnabledProperty.Changed.AddClassHandler<ItemsControl>(OnEnabledChanged);

    public static bool GetEnabled(ItemsControl element) => element.GetValue(EnabledProperty);
    public static void SetEnabled(ItemsControl element, bool value) => element.SetValue(EnabledProperty, value);

    /// <summary>Dove sta il puntatore rispetto alla riga: primo 40% sopra, fino al 70% dentro, il
    /// resto sotto. Funzione pura, identica ai numeri di WPF.</summary>
    public static DropZone ResolveZone(double fraction) =>
        fraction < 0.4 ? DropZone.Before :
        fraction < 0.7 ? DropZone.Onto :
        DropZone.After;

    /// <summary>La card di riga antenata del punto afferrato, riconosciuta dalla classe
    /// <c>habitblock</c>. In WPF il riconoscimento era un confronto per RIFERIMENTO sullo Style, che
    /// in Avalonia non ha gemello: la classe è il marcatore, e vale anche per le righe annidate nei
    /// gruppi (dove il fallback WPF <c>ContainerFromElement</c> non arrivava).</summary>
    public static Border? FindRowCard(object? source)
    {
        for (var v = source as Visual; v is not null; v = v.GetVisualParent())
            if (v is Border { Classes: var c } b && c.Contains("habitblock"))
                return b;
        return null;
    }

    private static void OnEnabledChanged(ItemsControl ic, AvaloniaPropertyChangedEventArgs e)
    {
        if (Sessions.TryGetValue(ic, out var existing))
        {
            existing.Detach();
            Sessions.Remove(ic);
        }
        if (e.GetNewValue<bool>())
        {
            var session = new DragSession(ic);
            Sessions.Add(ic, session);
            session.Attach();
        }
    }

    internal sealed class DragSession(ItemsControl root)
    {
        private Point _start;
        private PointerPressedEventArgs? _pressed;
        private HomeRowViewModel? _indicated;

        public void Attach()
        {
            root.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
            root.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
            root.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
            DragDrop.SetAllowDrop(root, true);
            DragDrop.AddDragOverHandler(root, OnDragOver);
            DragDrop.AddDragLeaveHandler(root, OnDragLeave);
            DragDrop.AddDropHandler(root, OnDrop);
        }

        public void Detach()
        {
            root.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
            root.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
            root.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
            DragDrop.SetAllowDrop(root, false);
            DragDrop.RemoveDragOverHandler(root, OnDragOver);
            DragDrop.RemoveDragLeaveHandler(root, OnDragLeave);
            DragDrop.RemoveDropHandler(root, OnDrop);
            SetIndicator(null, false, RowDropEdge.None);
        }

        // DoDragDropAsync vuole gli argomenti della PRESSIONE, non del movimento: si conservano qui
        // e si consumano appena il puntatore ha superato la soglia.
        private void OnPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(root).Properties.IsLeftButtonPressed) return;
            _start = e.GetPosition(root);
            _pressed = e;
        }

        private void OnReleased(object? sender, PointerReleasedEventArgs e) => _pressed = null;

        private async void OnMoved(object? sender, PointerEventArgs e)
        {
            if (_pressed is not { } pressed) return;
            if (!e.GetCurrentPoint(root).Properties.IsLeftButtonPressed) { _pressed = null; return; }
            var delta = e.GetPosition(root) - _start;
            if (Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) < DragThreshold) return;
            if ((e.Source as StyledElement)?.DataContext is not HomeRowViewModel src) return;

            _pressed = null;
            src.IsBeingDragged = true;
            try
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.Create(RowFormat, src));
                await DragDrop.DoDragDropAsync(pressed, data, DragDropEffects.Move);
            }
            // `async void`: un'eccezione non catturata qui ucciderebbe il processo. Si traccia e si
            // prosegue, come i gestori di MainWindow dopo CR15.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Trace.TraceError($"drag-and-drop non riuscito: {ex}");
            }
            finally
            {
                // Gira al rilascio E all'annullamento, ovunque il trascinamento sia finito.
                src.IsBeingDragged = false;
                SetIndicator(null, false, RowDropEdge.None);
            }
        }

        // Un solo indicatore alla volta: o l'evidenziazione "dentro" del gruppo (IsDropTarget) o un
        // bordo di inserimento (DropEdge) sulla riga sorvolata.
        private void OnDragOver(object? sender, DragEventArgs e)
        {
            if (e.DataTransfer.TryGetValue(RowFormat) is { } src
                && FindRowCard(e.Source) is { DataContext: HomeRowViewModel row } card
                && !ReferenceEquals(row, src))
            {
                e.DragEffects = DragDropEffects.Move;
                // La zona si misura sulla CARD, non sul sotto-elemento che ha scatenato l'evento:
                // altrimenti i terzi Before/Onto/After si mapperebbero sull'altezza di un testo.
                var zone = ResolveZone(e.GetPosition(card).Y / Math.Max(1, card.Bounds.Height));
                if (zone == DropZone.Onto && row.IsGroup)
                    SetIndicator(row, isGroupTarget: true, RowDropEdge.None);
                else
                    SetIndicator(row, isGroupTarget: false,
                        zone == DropZone.Before ? RowDropEdge.Before : RowDropEdge.After);
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
                SetIndicator(null, false, RowDropEdge.None);
            }
        }

        private void OnDragLeave(object? sender, DragEventArgs e) => SetIndicator(null, false, RowDropEdge.None);

        private void SetIndicator(HomeRowViewModel? row, bool isGroupTarget, RowDropEdge edge)
        {
            if (_indicated is not null)
            {
                _indicated.IsDropTarget = false;
                _indicated.DropEdge = RowDropEdge.None;
            }
            _indicated = row;
            if (_indicated is not null)
            {
                _indicated.IsDropTarget = isGroupTarget;
                _indicated.DropEdge = edge;
            }
        }

        private async void OnDrop(object? sender, DragEventArgs e)
        {
            SetIndicator(null, false, RowDropEdge.None);
            try
            {
                if (e.DataTransfer.TryGetValue(RowFormat) is not { } src) return;
                if (FindRowCard(e.Source) is not { DataContext: HomeRowViewModel target } card) return;
                if (ReferenceEquals(src, target)) return;
                if (root.DataContext is not HomeViewModel home) return;

                // Divergenza VOLUTA da WPF: là la zona del rilascio si misurava sull'elemento grezzo
                // che aveva scatenato l'evento, mentre il sorvolo la misurava sulla card — la
                // correzione CR17 era stata applicata a un solo ramo dei due, quindi rilasciare sopra
                // un testo o una striscia calcolava la zona sull'altezza di QUEL figlio. Qui i due
                // rami usano la stessa card e l'anteprima non può più mentire sull'esito.
                var zone = ResolveZone(e.GetPosition(card).Y / Math.Max(1, card.Bounds.Height));
                var plan = DropResolver.ResolveDrop(zone, target.IsGroup, target.HabitNodeId, target.ParentId);
                await home.ApplyDropAsync(src, plan);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Trace.TraceError($"rilascio non applicato: {ex}");
            }
        }
    }
}
