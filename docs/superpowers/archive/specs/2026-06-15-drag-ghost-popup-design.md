# Drag ghost — Popup topmost guidato da QueryContinueDrag

> Spec di design — 2026-06-15 · Fase 4 Polish/UX · issue `R-dragGhostVisual`
> Segue e sostituisce la parte *ghost* di `2026-06-15-drag-reorder-visual-design.md`. La **meccanica** di drag/reorder (DropPlan, `MoveAndReorderAsync`, indicatori, auto-switch a Manuale) resta invariata e fuori scope.

## 1. Problema

Il ghost di trascinamento (riga abitudine che "si solleva" e segue il cursore) è stato spedito a `wip` su `b7fe1d1` e rinviato. La verifica visiva utente (tema scuro) ha rilevato **tre difetti distinti**, non una singola rifinitura:

1. **Snapshot dell'elemento sbagliato → "il riquadro cambia dimensione e si sgrana".**
   `TreeDragDropBehavior.OnMove` passa a `DragAdorner` l'elemento `e.OriginalSource`, cioè l'elemento *più profondo* sotto il cursore (un `TextBlock`, un `Path`, il `Rectangle` accent…), non la riga. Il ghost fotografa quel sotto-elemento: dimensione variabile a seconda di dove si afferra, e aspetto "a bassa risoluzione" quando il sotto-elemento è minuscolo. La riga reale è il `Border Style="Recordum.HabitBlock"`.

2. **Riposizionamento su `DragOver` → scatti + a volte sparisce.**
   Il ghost si muove solo dentro `OnDragOver`. Durante un drag OLE `DragOver` scatta **solo** quando il cursore è sopra un bersaglio valido dentro l'`ItemsControl`, ed è raro/throttlato. Uscendo dalla lista (o sopra la search box) niente `DragOver` → il ghost si congela.

3. **`Adorner` ancorato all'`ItemsControl` → clipping ai bordi.**
   L'`Adorner` vive nell'adorner-layer dell'`ItemsControl` e non può disegnare oltre i suoi confini: uscendo dalla lista il ghost viene tagliato → "non si vede".

## 2. Obiettivo e non-obiettivi

**Obiettivo.** Un ghost **nitido**, di **dimensione fissa** (l'intera card della riga afferrata), che segue il cursore in modo **fluido** e resta **sempre visibile** ovunque sullo schermo durante il drag.

**Non-obiettivi (restano invariati):**
- La pipeline OLE `DoDragDrop` → `DragOver` → `Drop`. `DragOver` continua a guidare **solo** gli indicatori (`IsDropTarget` / `DropEdge`) e `Drop` continua a calcolare `DropPlan` ed eseguire `ApplyDropAsync`.
- `MoveAndReorderAsync`, auto-switch a Manuale, multi-selezione (reparent in blocco), regole pin.
- `ResolveZone` / `ResolveDrop` (puri, già testati).

## 3. Approccio scelto (A)

Sostituire l'`Adorner` con un **ghost in un `Popup` topmost**, riposizionato a ogni `QueryContinueDrag` usando la posizione **reale** del cursore (`GetCursorPos`).

Perché risolve tutti e tre i difetti:
- **Nitidezza + dimensione fissa** ← snapshot della **card intera** (il `Border` HabitBlock), non di `OriginalSource`.
- **Fluidità** ← `QueryContinueDrag` scatta a *ogni* movimento del mouse (molto più fitto di `DragOver`) e legge il cursore reale, non l'ultimo `DragOver`.
- **Sempre visibile** ← un `Popup` topmost galleggia su tutta la finestra/schermo, non è clippato da alcun adorner-layer.

## 4. Componenti

### 4.1 `DragGhost` (nuovo) — sostituisce `DragAdorner`

`sealed class DragGhost : IDisposable`. Unico scopo: rendere il ghost e seguírlo. Nessuna logica di dominio.

- **Costruzione**: `DragGhost(FrameworkElement rowCard)`.
  - Cattura uno snapshot **nitido** della `rowCard` con `RenderTargetBitmap` al DPI corrente dell'elemento (come oggi in `DragAdorner`), `Freeze()`. Lo snapshot va preso **prima** che il chiamante imposti `IsBeingDragged=true` (la card non deve essere ancora sbiadita).
  - Costruisce un `Popup` con:
    - `AllowsTransparency=True`, `IsHitTestVisible=False`, `Placement=PlacementMode.Absolute`, `PopupAnimation=None`, `AllowDrop=False`.
    - `StaysOpen=True` (non si chiude su perdita di focus / click).
    - Contenuto: un `Border` opaco (`Brush.SurfaceElevated` + bordo `Brush.Border`, `CornerRadius=6`, opacità complessiva ~0.95) con dentro un `Image` dello snapshot a dimensione logica `rowCard.ActualWidth × ActualHeight` → 1:1, crisp. (La card è translucida per design ~10% tint: l'opacità deve venire dallo sfondo opaco del Border, non dal contenuto.)
  - `_popup.IsOpen` resta `false` finché non arriva la prima posizione (evita un flash a (0,0)).
- **`SetScreenPosition(Point physicalCursor)`**:
  - Converte i pixel fisici dello schermo in unità WPF (DIU) usando la matrice DPI della sorgente di presentazione (`PresentationSource.CompositionTarget.TransformFromDevice`).
  - Imposta `HorizontalOffset = dipX + 8`, `VerticalOffset = dipY + 8` (offset down-right perché il cursore resti visibile).
  - Alla prima chiamata imposta `IsOpen=true`.
  - **Quirk noto**: un `Popup` aperto a volte non riposiziona al solo cambio di `HorizontalOffset`/`VerticalOffset`. Via di default: riassegnare gli offset a ogni evento (sufficiente nelle versioni WPF correnti). **Fallback** se in verifica il tracking risulta a scatti o non aggiorna: muovere direttamente l'HWND del popup con `SetWindowPos` (ottenuto da `PresentationSource.FromVisual(_popup.Child)` → `HwndSource.Handle`). Il fallback è opzionale e si attiva solo se la via offset non basta — decisione in verifica visiva, non a priori.
- **`Dispose()`**: `_popup.IsOpen=false`, scollega il contenuto, rilascia lo snapshot. Idempotente.

### 4.2 Risoluzione della card — `FindRowCard`

Helper statico in `TreeDragDropBehavior`: data l'`OriginalSource`, risale l'albero visuale (`VisualTreeHelper.GetParent`) fino al **primo `Border` la cui `Style` è la risorsa `Recordum.HabitBlock`**. Quello è la card a riga singola (per i gruppi cattura solo la card, non lo `StackPanel` con i figli annidati).

- Confronto: `border.Style == Application.Current?.TryFindResource("Recordum.HabitBlock")` (reference-equality sullo Style condiviso).
- **Fallback** se non trovato (es. Style ridefinito): `ItemsControl.ContainerFromElement(ic, original)` → il `ContentPresenter` container della riga. Garantisce comunque uno snapshot a livello-riga invece del sotto-elemento.
- Se anche il fallback è null → non creare il ghost (drag procede senza ghost; meglio di un crash). Caso difensivo, non atteso.

### 4.3 Interop nativa — `NativeMethods.GetCursorPos`

Nuovo file `Interop/NativeMethods.cs` (o aggiunta se esiste già): `[DllImport("user32.dll")] static extern bool GetCursorPos(out POINT)`, con `POINT { int X, int Y }`. Coordinate in **pixel fisici** dello schermo virtuale (multi-monitor friendly). Nessun'altra P/Invoke necessaria salvo il fallback `SetWindowPos`.

### 4.4 Conversione fisico→DIU — helper puro testabile

`static Point PhysicalToDip(Point physical, Matrix transformFromDevice)` (o equivalente): `transformFromDevice.Transform(physical)`. Estratto come funzione pura per poterlo coprire con uno unit test (input pixel + matrice DPI nota → DIU attesi), dato che il resto del ghost è verificabile solo a vista.

## 5. Modifiche a `TreeDragDropBehavior`

- **`OnMove`**:
  1. `var card = FindRowCard(originalSource, ic)`. Se non null, crea `_ghost = new DragGhost(card)` **prima** di `src.IsBeingDragged = true`. Se null (caso difensivo non atteso), si prosegue **senza** ghost: il drag OLE parte comunque.
  2. Aggancia il handler `QueryContinueDrag` su `fe` (la sorgente passata a `DoDragDrop`): a ogni evento, se `_ghost != null`, `GetCursorPos` → `_ghost.SetScreenPosition(...)`.
  3. `src.IsBeingDragged = true`, poi `DragDrop.DoDragDrop(fe, src, Move)` come oggi.
  4. **`finally`**: scollega `QueryContinueDrag`, `_ghost?.Dispose()`, `_ghost=null`, `src.IsBeingDragged=false`, `SetIndicator(null,…)`. (Teardown deterministico anche su Esc/annullamento, come l'attuale.)
- **`OnDragOver`**: **rimuovere** la riga `_dragAdorner?.SetPosition(...)`. `DragOver` torna a fare *solo* indicatori. Il ghost ora è mosso da `QueryContinueDrag`.
- Rimuovere il campo `_dragAdorner` e la dipendenza da `AdornerLayer`. Eliminare `DragAdorner.cs`.

`QueryContinueDrag` non porta la posizione del mouse negli args: per questo leggiamo `GetCursorPos`. L'evento scatta sul thread UI, quindi l'aggiornamento del `Popup` è thread-safe.

## 6. Testing

La maggior parte del ghost è verificabile **solo a vista** (rendering, fluidità, posizione). Copriamo con unit test ciò che è puro:

- **Esistente, invariato**: `ResolveZone`, `ResolveDrop` (già testati). Confermare verdi.
- **Nuovo**: test su `PhysicalToDip` — con una `Matrix` DPI nota (es. 1.5×) un punto fisico noto mappa al DIU atteso.
- **Nuovo (se isolabile)**: `FindRowCard` su un piccolo albero visuale costruito in test STA (sotto-elemento annidato → risale alla card; nessuna card → fallback container). Se il costo STA è sproporzionato, declassare a verifica manuale e documentarlo.

Suite di riferimento da mantenere verde: **WPF 555 / Data 332 / Core 55 / Plugins 66 / Export 17 / Chart 18**.

## 7. Verifica visiva manuale (checklist)

Tema **scuro e chiaro**:
1. Afferrando la riga in punti diversi (nome, icona, accent, spazio vuoto) il ghost ha **sempre la stessa dimensione** = la card intera, e resta **nitido**.
2. Il ghost segue il cursore **fluido**, senza scatti percepibili, anche con movimenti rapidi.
3. Trascinando **fuori dalla lista** (sopra header/search box / bordo finestra) il ghost **resta visibile** e continua a seguire.
4. Su **Esc / rilascio fuori da un bersaglio valido**: ghost e flag spariscono puliti, nessun residuo.
5. Gli **indicatori** (linea sopra/sotto, highlight gruppo) compaiono ancora correttamente e uno alla volta.
6. Il **drop** riordina/sposta e persiste come prima; auto-switch a Manuale invariato.
7. Multi-monitor con **DPI diversi** (se disponibile): il ghost resta agganciato al cursore senza deriva.

## 8. Rischi e mitigazioni

- **Popup che non traccia al cambio offset** (quirk WPF noto) → fallback `SetWindowPos` sull'HWND del popup; decisione in verifica.
- **Multi-monitor / DPI misti** → uso di `TransformFromDevice` della sorgente corretta; `GetCursorPos` dà coordinate dello schermo virtuale. Da validare a vista (checklist §7.7).
- **`Popup` con `AllowsTransparency`** richiede `WindowStyle`/airspace corretti; essendo `IsHitTestVisible=False` e `AllowDrop=False` non interferisce con il drop OLE sottostante.
- **Blast radius**: un solo file nuovo (`DragGhost`), un file interop, modifiche locali a `TreeDragDropBehavior`, eliminazione di `DragAdorner`. Nessuna modifica a Data/VM/XAML delle righe (la card si trova via Style reference, niente marker da aggiungere).
```
