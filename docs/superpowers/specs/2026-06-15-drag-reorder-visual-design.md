# Drag-and-drop visivo + riordino manuale delle abitudini

> **Data**: 2026-06-15 · **Branch**: `fase-4-polish` · **Stato**: design approvato, pronto per il piano.

## 1. Problema

Il drag-and-drop dell'albero Home oggi è "muto":

- L'unico feedback durante il trascinamento è l'evidenziazione del **gruppo bersaglio** (`HomeRowViewModel.IsDropTarget`) quando il cursore è nella zona *Onto*. La riga che si trascina **non ha alcun feedback** — resta ferma e si vede solo il cursore di sistema di WPF.
- Le zone **Before/After** non fanno nulla di utile: il commento in `TreeDragDropBehavior` dichiara *"there is no manual ordering"*; il drop in quelle zone si limita a ri-genitorare verso `target.ParentId`, senza riordinare.
- `HomeSort.Manual` è il default ma è **inerte**: `ApplySort` per Manual fa `_ => rows`, quindi nessun ordinamento; `HabitNode.SortOrder` esiste nel modello e nel DB ma **non viene mai letto né scritto** per i nodi habit (è usato solo per metriche/segmenti/chart).

L'utente vuole un drag-and-drop classico: l'abitudine "agganciata" al cursore che lo segue, con **indicatori di posizione** che mostrano dove finirà (dentro un gruppo, sopra/sotto un'abitudine), e che serva davvero a **riordinare e spostare**.

## 2. Obiettivo

1. **Fantasma agganciato al cursore**: durante il drag una copia semitrasparente della riga segue il puntatore; l'originale si sbiadisce sul posto.
2. **Indicatori di posizione**: linea di inserimento sopra/sotto un'abitudine (riordino) ed evidenziazione del gruppo (entra-dentro), mutuamente esclusivi.
3. **Riordino manuale persistente** tra fratelli, salvato su `HabitNode.SortOrder` (nessuna migrazione: la colonna esiste già).

Non-goal: animazioni elaborate, riordino fine in multi-selezione, drag tra tab diverse.

## 3. Vincoli / principi

- WPF nativo, niente librerie drag-drop esterne.
- Coerenza con il pattern esistente: lo stato visivo passa da **flag sul ViewModel** (`IsDropTarget` è il precedente), così la logica resta unit-testabile senza dipendere dal rendering.
- Motion restrained (design brief): nessun bounce/scale animato; linea di inserimento sottile, sbiadimento statico.
- Nessuna modifica di schema: `sort_order` su `habit_node` è già presente; nessun indice nuovo richiesto (N fratelli piccolo).

## 4. Design

### 4.1 Dati & ordinamento

- **`HomeTreeRow`** guadagna `SortOrder` (int). `HabitTreeQueryService` lo proietta dalla `HabitNode`.
- **`HomeSort.Manual`** smette di essere inerte: in `ApplySort`, il ramo Manual ordina per `SortOrder` con tie-break su `Name` (`OrderBy(r => r.SortOrder).ThenBy(r => r.Name)`). Name/LastCompletion invariati.
- **Servizio di riordino (Data)** — nuovo metodo, p.es. `IHabitNodeOrderingService.ReorderAsync(Guid movedId, Guid? newParentId, int targetIndex, CancellationToken)`:
  - Carica i fratelli (figli di `newParentId`, esclusa la riga spostata) ordinati per `SortOrder` correnti.
  - Inserisce `movedId` all'indice `targetIndex`.
  - Riassegna `SortOrder` contigui `0..n-1` a tutta la sequenza.
  - Aggiorna anche `ParentId` della riga spostata se cambia genitore (assorbe il caso "riordino + cambio gruppo" in un'unica operazione transazionale).
  - Tutto in una singola transazione; il `TimestampInterceptor` popola `UpdatedAt`/`UpdatedAtMs`.
  - Guardia ciclo: rifiuta lo spostamento di un nodo dentro un proprio discendente (riusa `CyclicHabitNodeMoveException`).
- **`HomeViewModel`**: quando un drop ricade in zona Before/After (riordino), se `SortOption != Manual` forza `SortOption = Manual` prima/dopo il reload, così il nuovo ordine è visibile e persiste.

> Nota: il drop **Onto** un gruppo è un riparenting che **appende in coda**: la riga spostata prende `SortOrder = max(SortOrder dei nuovi fratelli) + 1`. Si realizza passando per lo stesso `ReorderAsync` con `targetIndex = numero di fratelli` (indice oltre l'ultimo), così c'è un solo percorso di persistenza. Solo Before/After calcola un indice intermedio.

### 4.2 Comportamento drag (`TreeDragDropBehavior`)

Le tre zone (`ResolveZone`, già testata) restano: **Before** (<0.4), **Onto** (0.4–0.7), **After** (≥0.7). Onto produce un indicatore solo se il bersaglio `IsGroup`.

- **Start** (`OnMove`, soglia 5px già esistente): oltre a `DoDragDrop`, crea l'adorner ghost nel `AdornerLayer` dell'`ItemsControl` e imposta `IsBeingDragged = true` sulla riga sorgente.
- **DragOver**: aggiorna la posizione dell'adorner sul cursore; calcola la zona e imposta **un solo** indicatore:
  - Onto su gruppo → `IsDropTarget = true` sul gruppo (come oggi), nessun `DropEdge`.
  - Before/After su una riga → `DropEdge = Before|After` su quella riga, nessun `IsDropTarget`.
  - Pulisce sempre l'indicatore precedente (un solo indicatore attivo alla volta, helper `SetIndicator`).
- **Drop**: calcola genitore e indice di destinazione:
  - Onto-gruppo → `ReorderAsync(src, target.HabitNodeId, targetIndex = #figli)` (appende in coda al gruppo).
  - Before/After → `ReorderAsync(src, target.ParentId, index)` dove `index` è la posizione del bersaglio tra i suoi fratelli, +1 per After.
  - Multi-selezione: se la sorgente fa parte di una selezione multipla, resta il percorso esistente `TryBulkMoveAsync` (riparenting in blocco, niente indice fine).
  - Pulisce adorner, `IsBeingDragged`, indicatori; ricarica.
- **DragLeave / fine drag**: rimuove adorner e azzera tutti i flag (anche su drop annullato o `Esc`).

### 4.3 Visivo

- **`HomeRowViewModel`** guadagna:
  - `IsBeingDragged` (bool) → opacità ~0.4 sull'originale.
  - `DropEdge` (enum `None|Before|After`) → linea di inserimento.
- **Template `Recordum.HabitBlock`** (in `App.xaml` o resource dict pertinente): trigger su
  - `IsBeingDragged` → `Opacity` 0.4.
  - `DropEdge=Before` → bordo superiore accent 2px; `DropEdge=After` → bordo inferiore accent 2px.
  - `IsDropTarget` → evidenziazione gruppo invariata.
- **`DragAdorner`** — classe dedicata (file a sé), unico scopo: rendere un `VisualBrush` snapshot della riga sorgente come rettangolo semitrasparente offsettato rispetto al cursore, riposizionato a ogni DragOver. Nessuna logica di dominio.

### 4.4 Boundaries (unità e responsabilità)

| Unità | Responsabilità | Dipende da |
|---|---|---|
| `IHabitNodeOrderingService` (Data) | Persistere ordine/genitore dei fratelli, transazionale | DbContext, `IPluginContext` |
| `HomeSort.Manual` in `ApplySort` | Ordinare le righe per `SortOrder` | `HomeTreeRow.SortOrder` |
| `TreeDragDropBehavior` | Orchestrare zone, indicatori, spawn/teardown adorner, dispatch al servizio | VM flags, `DragAdorner`, ordering service via VM |
| `DragAdorner` | Disegnare il ghost che segue il cursore | solo WPF (visual) |
| `HomeRowViewModel` flags | Esporre `IsBeingDragged`/`DropEdge`/`IsDropTarget` | — |

## 5. Test

Unit (testabili senza rendering):
- Servizio riordino: inserimento all'indice, riassegnazione `SortOrder` contigua, cambio genitore combinato, guardia ciclo.
- `ApplySort` Manual ordina per `SortOrder` (tie-break Name).
- Auto-switch a `HomeSort.Manual` su drop di riordino.
- `TreeDragDropBehavior`: dato zona+bersaglio, calcolo dell'indice/parent corretto (estende i test `ResolveZone` esistenti); un solo indicatore attivo alla volta.

Verifica visiva manuale (non unit):
- Fantasma che segue il cursore + originale sbiadito.
- Linea di inserimento sopra/sotto; evidenziazione gruppo per Onto.
- Riordino persiste al riavvio; auto-switch a Manuale visibile.
- Due temi (chiaro/scuro).

## 6. Limiti noti (accettati)

- **Multi-selezione**: il drop di una selezione resta riparenting in blocco (nessun indice fine) — il riordino preciso vale per il drag a riga singola.
- **Pin**: le righe Global/Local restano in cima (override applicato sopra `ApplySort`); `SortOrder` ordina *dentro* il gruppo dei pin. Riordinare attraverso il confine pin può dare un risultato leggermente sorprendente — accettabile.

## 7. Fuori scope

- Animazioni di transizione delle righe.
- Drag tra tab (Attive/Archiviate/Cestino) diverse.
- Riordino fine in multi-selezione.
