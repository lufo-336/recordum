# Drag Ghost Popup — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sostituire il ghost di drag (oggi un `Adorner` ancorato all'`ItemsControl`, sfocato/a scatti/clippato) con un `Popup` topmost che fotografa la card di riga intera e segue il cursore reale via `QueryContinueDrag`.

**Architecture:** Un nuovo `DragGhost` (Popup + snapshot della card) rimpiazza `DragAdorner`. `TreeDragDropBehavior.OnMove` risolve la card via `FindRowCard`, crea il ghost e lo riposiziona a ogni `QueryContinueDrag` leggendo `GetCursorPos` (P/Invoke). `OnDragOver` torna a guidare solo gli indicatori. La pipeline OLE di drop/reorder resta invariata.

**Tech Stack:** WPF (.NET 10), `System.Windows.Controls.Primitives.Popup`, `RenderTargetBitmap`, P/Invoke `user32!GetCursorPos`, xUnit + `[WpfFact]` (STA) + FluentAssertions.

Spec di riferimento: `docs/superpowers/specs/2026-06-15-drag-ghost-popup-design.md`.

---

## File Structure

- **Create** `src/Recordum.WPF/Interop/NativeMethods.cs` — P/Invoke `GetCursorPos`, espone `TryGetCursorPos() : Point?` in pixel fisici dello schermo.
- **Create** `src/Recordum.WPF/Behaviors/DragGhost.cs` — `sealed class DragGhost : IDisposable`. Popup topmost + snapshot card + `SetScreenPosition`. Contiene `internal static PhysicalToDip(Point, Matrix)` (puro, testabile).
- **Modify** `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs` — aggiunge `FindAncestorWithStyle` (internal, testabile) + `FindRowCard` (private); riscrive `OnMove`; ripulisce `OnDragOver`; sostituisce il campo `_dragAdorner` con `_ghost`.
- **Delete** `src/Recordum.WPF/Behaviors/DragAdorner.cs` — sostituito da `DragGhost`.
- **Create** `tests/Recordum.WPF.Tests/Behaviors/DragGhostTests.cs` — test su `PhysicalToDip` e smoke su `DragGhost`.
- **Modify** `tests/Recordum.WPF.Tests/Behaviors/TreeDragDropBehaviorTests.cs` — aggiunge i test di `FindAncestorWithStyle`.

---

## Task 1: Interop `GetCursorPos`

**Files:**
- Create: `src/Recordum.WPF/Interop/NativeMethods.cs`

Nessun unit test significativo (chiama `user32.dll`); la copertura del comportamento dipendente dal cursore è manuale. Questo task è solo l'interop, verificato dalla compilazione.

- [ ] **Step 1: Crea il file interop**

```csharp
using System.Runtime.InteropServices;
using System.Windows;

namespace Recordum.WPF.Interop;

/// <summary>Win32 interop usato dal drag ghost. Singolo scopo: posizione reale del cursore.</summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>Posizione corrente del cursore in pixel fisici dello schermo virtuale, o null se la
    /// chiamata fallisce. <see cref="QueryContinueDragEventArgs"/> non porta la posizione del mouse,
    /// quindi il ghost la legge da qui.</summary>
    public static Point? TryGetCursorPos()
        => GetCursorPos(out var p) ? new Point(p.X, p.Y) : null;
}
```

- [ ] **Step 2: Compila**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: build OK (0 errori). `TryGetCursorPos` ancora non referenziato → eventuale warning di analisi su membro inutilizzato è accettabile in questo task isolato.

- [ ] **Step 3: Commit**

```bash
git add src/Recordum.WPF/Interop/NativeMethods.cs
git commit -m "feat(wpf): interop GetCursorPos per il drag ghost

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: `PhysicalToDip` (conversione DPI pura)

**Files:**
- Create: `src/Recordum.WPF/Behaviors/DragGhost.cs` (in questo task solo lo scheletro + il metodo puro)
- Test: `tests/Recordum.WPF.Tests/Behaviors/DragGhostTests.cs`

`PhysicalToDip` è l'unico pezzo del ghost testabile senza UI: converte pixel fisici → unità WPF (DIU) applicando la matrice `TransformFromDevice` della sorgente. I test del progetto WPF vedono già gli `internal` (i test esistenti chiamano `TreeDragDropBehavior.ResolveZone`, che è `internal`).

- [ ] **Step 1: Scrivi il test che fallisce**

```csharp
using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using Recordum.WPF.Behaviors;
using Xunit;

namespace Recordum.WPF.Tests.Behaviors;

public class DragGhostTests
{
    [Fact]
    public void Physical_to_dip_applies_inverse_dpi_scale()
    {
        // 150% DPI: la trasformazione device->dip scala per 1/1.5.
        var deviceToDip = new Matrix(1.0 / 1.5, 0, 0, 1.0 / 1.5, 0, 0);

        var dip = DragGhost.PhysicalToDip(new Point(150, 300), deviceToDip);

        dip.X.Should().BeApproximately(100, 0.0001);
        dip.Y.Should().BeApproximately(200, 0.0001);
    }

    [Fact]
    public void Physical_to_dip_is_identity_at_100_percent()
    {
        var dip = DragGhost.PhysicalToDip(new Point(42, 7), Matrix.Identity);

        dip.Should().Be(new Point(42, 7));
    }
}
```

- [ ] **Step 2: Esegui il test, verifica che fallisca**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~DragGhostTests"`
Expected: FAIL in compilazione — `DragGhost` non esiste.

- [ ] **Step 3: Crea `DragGhost.cs` con il solo metodo puro**

```csharp
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
```

> Nota: `partial` perché il resto della classe (Popup, snapshot, IDisposable) arriva nel Task 3, nello stesso file.

- [ ] **Step 4: Esegui il test, verifica che passi**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~DragGhostTests"`
Expected: PASS (2 test verdi).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Behaviors/DragGhost.cs tests/Recordum.WPF.Tests/Behaviors/DragGhostTests.cs
git commit -m "feat(wpf): DragGhost.PhysicalToDip + test conversione DPI

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: `DragGhost` completo (Popup + snapshot)

**Files:**
- Modify: `src/Recordum.WPF/Behaviors/DragGhost.cs`
- Test: `tests/Recordum.WPF.Tests/Behaviors/DragGhostTests.cs`

Completa la classe: snapshot nitido della card, Popup opaco topmost, `SetScreenPosition`, `Dispose`. Lo smoke test usa `[WpfFact]` (STA) perché costruisce visual WPF.

- [ ] **Step 1: Scrivi lo smoke test che fallisce**

```csharp
// Aggiungi questi using in cima a DragGhostTests.cs:
//   using System.Windows.Controls;
//   using System.Windows.Media;

[WpfFact]
public void Set_screen_position_opens_the_popup_once()
{
    var card = new Border { Width = 200, Height = 40, Background = Brushes.Gray };
    card.Measure(new Size(200, 40));
    card.Arrange(new Rect(0, 0, 200, 40));
    card.UpdateLayout();

    using var ghost = new DragGhost(card);
    ghost.IsOpen.Should().BeFalse("il popup non si apre finché non arriva una posizione");

    ghost.SetScreenPosition(new Point(100, 100));

    ghost.IsOpen.Should().BeTrue();
}

[WpfFact]
public void Dispose_closes_the_popup_and_is_idempotent()
{
    var card = new Border { Width = 120, Height = 30, Background = Brushes.Gray };
    card.Measure(new Size(120, 30));
    card.Arrange(new Rect(0, 0, 120, 30));
    card.UpdateLayout();

    var ghost = new DragGhost(card);
    ghost.SetScreenPosition(new Point(50, 50));

    ghost.Dispose();
    ghost.IsOpen.Should().BeFalse();

    var act = () => ghost.Dispose();
    act.Should().NotThrow();
    ghost.SetScreenPosition(new Point(10, 10)); // no-op dopo dispose
    ghost.IsOpen.Should().BeFalse();
}
```

- [ ] **Step 2: Esegui i test, verifica che falliscano**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~DragGhostTests"`
Expected: FAIL in compilazione — `DragGhost` non ha costruttore, `IsOpen`, `SetScreenPosition`, `Dispose`.

- [ ] **Step 3: Completa `DragGhost.cs`**

Sostituisci interamente il contenuto del file con:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Recordum.WPF.Behaviors;

/// <summary>
/// Ghost di trascinamento: uno snapshot della card di riga su un Popup topmost che segue il cursore.
/// Singolo scopo: rendere il ghost e seguirlo. Nessuna logica di dominio.
/// </summary>
public sealed class DragGhost : IDisposable
{
    private const double CornerRadius = 6;
    private const double GhostOpacity = 0.95;
    private const double CursorOffset = 8; // il ghost sta giù-a-destra così il cursore resta visibile

    private readonly Popup _popup;
    private readonly Visual _source; // per la matrice DPI della sorgente di presentazione
    private bool _opened;
    private bool _disposed;

    public DragGhost(FrameworkElement rowCard)
    {
        _source = rowCard;

        // Fotografa la card *ora*, prima che il chiamante la sbiadisca via IsBeingDragged.
        // Snapshot al DPI corrente => nitido; disegnato a dimensione logica => 1:1.
        var image = new Image
        {
            Source = Snapshot(rowCard),
            Width = rowCard.ActualWidth,
            Height = rowCard.ActualHeight,
            Stretch = Stretch.Fill,
        };

        // La card abitudine è translucida per design (~10% tint): l'opacità del ghost deve venire dallo
        // sfondo OPACO del Border, non dal contenuto, o il ghost sembra trasparente.
        var frame = new Border
        {
            Background = Resolve("Brush.SurfaceElevated", "Brush.Background") ?? Brushes.White,
            BorderBrush = Resolve("Brush.Border", "Brush.Accent") ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(CornerRadius),
            Opacity = GhostOpacity,
            SnapsToDevicePixels = true,
            Child = image,
        };

        _popup = new Popup
        {
            AllowsTransparency = true,
            IsHitTestVisible = false,
            AllowDrop = false,
            StaysOpen = true,
            Placement = PlacementMode.Absolute,
            PopupAnimation = PopupAnimation.None,
            Child = frame,
        };
    }

    /// <summary>True quando il popup è aperto (cioè dopo la prima <see cref="SetScreenPosition"/>).</summary>
    internal bool IsOpen => _popup.IsOpen;

    /// <summary>Riposiziona il ghost al cursore. <paramref name="physicalCursor"/> è in pixel fisici
    /// dello schermo (da <c>GetCursorPos</c>); viene convertito in DIU per gli offset del Popup.</summary>
    public void SetScreenPosition(Point physicalCursor)
    {
        if (_disposed) return;

        var transform = PresentationSource.FromVisual(_source)?.CompositionTarget?.TransformFromDevice
                        ?? Matrix.Identity;
        var dip = PhysicalToDip(physicalCursor, transform);

        _popup.HorizontalOffset = dip.X + CursorOffset;
        _popup.VerticalOffset = dip.Y + CursorOffset;

        if (!_opened)
        {
            _popup.IsOpen = true; // ritardato alla prima posizione per evitare un flash a (0,0)
            _opened = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _popup.IsOpen = false;
        _popup.Child = null;
    }

    /// <summary>Converte pixel fisici dello schermo in unità WPF (DIU) data la matrice
    /// <c>TransformFromDevice</c> della sorgente di presentazione.</summary>
    internal static Point PhysicalToDip(Point physical, Matrix transformFromDevice)
        => transformFromDevice.Transform(physical);

    private static ImageSource Snapshot(FrameworkElement source)
    {
        var dpi = VisualTreeHelper.GetDpi(source);
        var bmp = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(source.ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(source.ActualHeight * dpi.DpiScaleY)),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bmp.Render(source);
        bmp.Freeze();
        return bmp;
    }

    private static Brush? Resolve(params string[] keys)
    {
        foreach (var key in keys)
            if (Application.Current?.TryFindResource(key) is Brush b)
                return b;
        return null;
    }
}
```

- [ ] **Step 4: Esegui i test, verifica che passino**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~DragGhostTests"`
Expected: PASS (4 test verdi: 2 di Task 2 + 2 smoke).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Behaviors/DragGhost.cs tests/Recordum.WPF.Tests/Behaviors/DragGhostTests.cs
git commit -m "feat(wpf): DragGhost completo — Popup topmost con snapshot card opaca

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: `FindAncestorWithStyle` (risalita alla card)

**Files:**
- Modify: `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs`
- Test: `tests/Recordum.WPF.Tests/Behaviors/TreeDragDropBehaviorTests.cs`

Il bug "il riquadro cambia dimensione / si sgrana" nasce dal fotografare `OriginalSource` (un sotto-elemento). Serve risalire al `Border Style="Recordum.HabitBlock"` (la card). Estraggo il cuore della risalita — "primo antenato `FrameworkElement` con questo `Style`" — come metodo `internal` puro, testabile con un albero costruito a mano (passando lo `Style`, senza dipendere dalle risorse dell'app).

- [ ] **Step 1: Scrivi i test che falliscono**

```csharp
// Aggiungi questi using in cima a TreeDragDropBehaviorTests.cs:
//   using System.Windows;
//   using System.Windows.Controls;

[WpfFact]
public void Find_ancestor_with_style_returns_the_card_not_the_inner_element()
{
    var cardStyle = new Style(typeof(Border));
    var inner = new TextBlock();
    var card = new Border { Style = cardStyle, Child = inner };
    var root = new Border { Child = card };
    root.Measure(new Size(500, 500));
    root.Arrange(new Rect(0, 0, 500, 500));

    var found = TreeDragDropBehavior.FindAncestorWithStyle(inner, cardStyle);

    found.Should().BeSameAs(card);
}

[WpfFact]
public void Find_ancestor_with_style_returns_null_when_no_ancestor_matches()
{
    var cardStyle = new Style(typeof(Border));
    var inner = new TextBlock();
    var root = new Border { Child = inner }; // nessuno ha cardStyle
    root.Measure(new Size(100, 100));
    root.Arrange(new Rect(0, 0, 100, 100));

    var found = TreeDragDropBehavior.FindAncestorWithStyle(inner, cardStyle);

    found.Should().BeNull();
}
```

- [ ] **Step 2: Esegui i test, verifica che falliscano**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~TreeDragDropBehaviorTests"`
Expected: FAIL in compilazione — `FindAncestorWithStyle` non esiste.

- [ ] **Step 3: Aggiungi il metodo a `TreeDragDropBehavior`**

In cima al file, assicurati che ci siano gli using (`System.Windows.Media` per `VisualTreeHelper`):

```csharp
using System.Windows.Media;
```

Aggiungi il metodo dentro la classe `TreeDragDropBehavior` (sotto `ResolveDrop`):

```csharp
/// <summary>Risale l'albero visuale dal punto dato al primo <see cref="FrameworkElement"/> il cui
/// <c>Style</c> è (reference-equal) lo <paramref name="style"/> dato. Usato per trovare la card di
/// riga (Border "Recordum.HabitBlock") partendo dal sotto-elemento sotto il cursore.</summary>
internal static FrameworkElement? FindAncestorWithStyle(DependencyObject? origin, Style style)
{
    for (var d = origin; d is not null; d = VisualTreeHelper.GetParent(d))
        if (d is FrameworkElement fe && ReferenceEquals(fe.Style, style))
            return fe;
    return null;
}
```

- [ ] **Step 4: Esegui i test, verifica che passino**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~TreeDragDropBehaviorTests"`
Expected: PASS (i 5 test esistenti + 2 nuovi).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs tests/Recordum.WPF.Tests/Behaviors/TreeDragDropBehaviorTests.cs
git commit -m "feat(wpf): FindAncestorWithStyle — risale alla card di riga per lo snapshot

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Cablaggio in `TreeDragDropBehavior` + rimozione `DragAdorner`

**Files:**
- Modify: `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs`
- Delete: `src/Recordum.WPF/Behaviors/DragAdorner.cs`

Sostituisce l'`Adorner` con il `DragGhost`: `OnMove` risolve la card, crea il ghost, lo muove su `QueryContinueDrag`; `OnDragOver` non muove più il ghost. Il flusso OLE di drop è coperto solo da verifica manuale (Task 6); qui la rete di sicurezza è: suite WPF verde + build pulita.

- [ ] **Step 1: Aggiungi `FindRowCard` (private) a `TreeDragDropBehavior`**

Sotto `FindAncestorWithStyle`, aggiungi:

```csharp
/// <summary>La card di riga da fotografare: il Border "Recordum.HabitBlock" antenato del punto
/// afferrato. Fallback al container della riga (ContentPresenter) se lo Style non si risolve — comunque
/// a livello-riga, mai un sotto-elemento. Null solo in casi difensivi non attesi.</summary>
private static FrameworkElement? FindRowCard(DependencyObject? origin, ItemsControl ic)
{
    if (Application.Current?.TryFindResource("Recordum.HabitBlock") is Style cardStyle
        && FindAncestorWithStyle(origin, cardStyle) is { } card)
        return card;
    return ItemsControl.ContainerFromElement(ic, origin) as FrameworkElement;
}
```

- [ ] **Step 2: Sostituisci il campo `_dragAdorner` con `_ghost`**

Trova:

```csharp
    private static DragAdorner? _dragAdorner;
```

Sostituisci con:

```csharp
    private static DragGhost? _ghost;
```

- [ ] **Step 3: Riscrivi `OnMove`**

Sostituisci interamente il metodo `OnMove` con:

```csharp
private static void OnMove(object sender, MouseEventArgs e)
{
    if (!_armed || e.LeftButton != MouseButtonState.Pressed) return;
    var p = e.GetPosition(sender as IInputElement);
    if ((p - _start).Length < 5) return;
    if (sender is ItemsControl ic
        && e.OriginalSource is FrameworkElement fe
        && fe.DataContext is HomeRowViewModel src)
    {
        _armed = false;
        var card = FindRowCard(fe, ic);
        _ghost = card is not null ? new DragGhost(card) : null;
        src.IsBeingDragged = true;

        // QueryContinueDrag scatta a ogni movimento del mouse (molto più fitto di DragOver) e ovunque
        // sullo schermo; i suoi args non portano la posizione, quindi la leggiamo da GetCursorPos.
        void OnQueryContinue(object? s, QueryContinueDragEventArgs args)
        {
            if (_ghost is not null && NativeMethods.TryGetCursorPos() is { } pos)
                _ghost.SetScreenPosition(pos);
        }
        fe.QueryContinueDrag += OnQueryContinue;
        try
        {
            DragDrop.DoDragDrop(fe, src, DragDropEffects.Move);
        }
        finally
        {
            // DoDragDrop è bloccante; questo gira al drop O all'annullamento, ovunque finisca.
            fe.QueryContinueDrag -= OnQueryContinue;
            _ghost?.Dispose();
            _ghost = null;
            src.IsBeingDragged = false;
            SetIndicator(null, false, RowDropEdge.None);
        }
    }
}
```

- [ ] **Step 4: Ripulisci `OnDragOver`**

Trova la prima riga del corpo di `OnDragOver`:

```csharp
        _dragAdorner?.SetPosition(e.GetPosition(sender as IInputElement));
```

Eliminala (il ghost ora è mosso da `QueryContinueDrag`; `DragOver` torna a guidare solo gli indicatori).

- [ ] **Step 5: Aggiusta gli using**

In cima a `TreeDragDropBehavior.cs`:
- Aggiungi `using Recordum.WPF.Interop;`
- Rimuovi `using System.Windows.Documents;` se non più referenziato (era per `AdornerLayer`; ora non si usa più).

Mantieni `using System.Windows.Media;` (aggiunto nel Task 4, serve a `VisualTreeHelper`).

- [ ] **Step 6: Elimina `DragAdorner.cs`**

```bash
git rm src/Recordum.WPF/Behaviors/DragAdorner.cs
```

(Verificato: `DragAdorner` è referenziato solo da `TreeDragDropBehavior`, che ora non lo usa più.)

- [ ] **Step 7: Build + suite WPF completa**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: 0 errori, 0 warning nuovi.

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj`
Expected: PASS — tutti verdi (i 555 esistenti + i nuovi di Task 2–4). Nessun riferimento residuo a `DragAdorner`.

- [ ] **Step 8: Commit**

```bash
git add src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs
git commit -m "feat(wpf): drag ghost via Popup+QueryContinueDrag; rimosso DragAdorner

Risolve R-dragGhostVisual: snapshot della card intera (nitido, dimensione fissa),
riposizionamento sul cursore reale a ogni QueryContinueDrag (fluido), Popup topmost
sempre visibile fuori dalla lista. DragOver torna a guidare solo gli indicatori.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: Verifica visiva + aggiornamento tracker

**Files:**
- Modify: `docs/phase-4-issues.md`

Questo è il checkpoint umano: il comportamento drag/ghost si valida solo a vista. Esegui l'app e percorri la checklist §7 dello spec.

- [ ] **Step 1: Avvia l'app**

Usa la skill `run` (o `dotnet run --project src/Recordum.WPF`). Tema scuro **e** chiaro.

- [ ] **Step 2: Percorri la checklist visiva (spec §7)**

1. Afferra la riga in punti diversi (nome, icona, accent, spazio vuoto) → il ghost ha **sempre** la stessa dimensione (= card intera) ed è **nitido**.
2. Il ghost segue il cursore **fluido**, senza scatti, anche con movimenti rapidi.
3. Trascinando **fuori dalla lista** (header / search box / bordo finestra) il ghost **resta visibile** e segue.
4. **Esc / rilascio fuori da un bersaglio valido** → ghost e dimming spariscono puliti, nessun residuo.
5. Gli **indicatori** (linea sopra/sotto, highlight gruppo) compaiono ancora, uno alla volta.
6. Il **drop** riordina/sposta e persiste; auto-switch a Manuale invariato.
7. (Se disponibile) multi-monitor con **DPI diversi**: nessuna deriva del ghost dal cursore.

- [ ] **Step 3: Se il ghost non traccia / scatta (quirk Popup noto)**

Solo se la via offset non basta (deciso ora, in verifica, non a priori): in `DragGhost.SetScreenPosition`, dopo aver impostato gli offset, muovi direttamente l'HWND del popup:

```csharp
// Fallback: alcune versioni di WPF non riposizionano un Popup aperto al solo cambio di offset.
if (PresentationSource.FromVisual(_popup.Child) is System.Windows.Interop.HwndSource hwnd)
    NativeMethods.MoveWindowTopLeft(hwnd.Handle, (int)physicalCursor.X + 8, (int)physicalCursor.Y + 8);
```

e aggiungi a `NativeMethods`:

```csharp
[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool SetWindowPos(
    nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

/// <summary>Muove la finestra (HWND) all'angolo dato in pixel fisici, senza ridimensionare né cambiare Z-order.</summary>
public static void MoveWindowTopLeft(nint hWnd, int x, int y)
{
    const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
    SetWindowPos(hWnd, 0, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
}
```

Poi ri-verifica la checklist e committa il fallback separatamente. Se la via offset basta, **salta questo step**.

- [ ] **Step 4: Aggiorna il tracker**

In `docs/phase-4-issues.md`, marca `R-dragGhostVisual` come `fixed (verifica visiva utente OK)` con il SHA del commit di Task 5 (e dell'eventuale fallback). Annota l'esito della checklist e se il fallback `SetWindowPos` è stato necessario.

- [ ] **Step 5: Commit**

```bash
git add docs/phase-4-issues.md
git commit -m "docs: tracker — R-dragGhostVisual risolto (ghost Popup verificato a vista)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Note di esecuzione

- **Ordine consigliato**: 1 → 6 in sequenza (Task 3 dipende da Task 2 stesso file; Task 5 dipende da 1/3/4).
- **STA**: i test che costruiscono visual WPF usano `[WpfFact]` (già in uso nel progetto, es. `TrendChartControlSmokeTests`). I test puri (`PhysicalToDip`, `ResolveZone`) restano `[Fact]`/`[Theory]`.
- **Nessuna migrazione, nessun tocco a Core/Data/VM/XAML delle righe.** La card si trova via reference dello Style condiviso — niente marker da aggiungere ai template.
- **Suite di riferimento da tenere verde**: WPF 555 (+ nuovi) / Data 332 / Core 55 / Plugins 66 / Export 17 / Chart 18.
```
