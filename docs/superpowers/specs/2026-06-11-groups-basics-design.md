# Recordum — Gruppi-basi: ＋Nuova▾ + drag-drop evidente (nav-model parte 2)

> Attua §3.2 dello spec `2026-05-29-navigation-model-design.md`. Decisioni utente 2026-06-11:
> "Nuovo gruppo" crea subito e apre Configura; con una habit selezionata il nuovo nodo nasce
> fratello (stesso parent); feedback drag = solo highlight del gruppo bersaglio.

## 1. Problema

Creare un gruppo oggi richiede di scovare "Gruppo vuoto" dentro il picker template, la
creazione ignora la selezione corrente (tutto nasce alla radice), e il drag-drop per
annidare — pur esistente in `TreeDragDropBehavior` — è invisibile: nessun feedback su
dove cadrà il nodo.

## 2. Decisione

Quattro interventi minimi, nessun cambiamento di schema:

### 2.1 Menu ＋ Nuova ▾
Il bottone "Nuova" in `HomeView.xaml` apre un `ContextMenu` con due voci — **Nuova habit**
e **Nuovo gruppo** (stringhe resx nuove, EN-first) — stesso pattern del menu Backup nel
ribbon. Niente SplitButton custom.

### 2.2 Risoluzione del parent (entrambe le voci)
- Gruppo selezionato → il nuovo nodo nasce **dentro** quel gruppo.
- Habit selezionata → nasce **fratello** (stesso `ParentId` della habit).
- Nessuna selezione → radice (`null`).

"Nuova habit" passa il parent risolto a `IHabitTemplatePickerLauncher.PickAsync(parentId)`
(plumbing già esistente, oggi mai usato dalla Home). Il picker mantiene la voce
"Gruppo vuoto" — nessuna rimozione.

### 2.3 Nuovo gruppo — creazione diretta
Command `CreateGroupAsync` in `HomeViewModel`: crea via `IHabitNodeService` un
`HabitNode { NodeType = Group, Name = Strings.Editor_Default_NewGroup, Color = "#808080",
ParentId = <risolto come sopra> }`, ricarica l'albero, seleziona il nuovo nodo e chiede
l'apertura della colonna **Configura** per la rinomina immediata. Il glue selezione→
workspace vive in `MainWindowViewModel` (già orchestratore di `Home.SelectedRow` →
`HabitScreen.LoadAsync`); l'apertura di Configura usa `HabitScreen.IsConfiguraOpen = true`
dopo il load. L'editor gestisce già `NodeType.Group` (sezioni filtrate da `IsApplicable`).

### 2.4 Drag-drop evidente
`TreeDragDropBehavior` aggancia anche `DragOver` e `DragLeave`: quando il cursore è nella
zona `Onto` (40–70% dell'altezza riga) di una riga **gruppo**, il behavior setta
`HomeRowViewModel.IsDropTarget = true` (e lo azzera sulla riga precedente, su leave e su
drop). Il template riga in `HomeView.xaml` evidenzia la riga con `IsDropTarget` (bordo
`Brush.Accent`). Nessun indicatore per le zone Before/After: non esiste ordinamento
manuale, un segnaposto posizionale mentirebbe.

## 3. Fuori scope

- Vista di dettaglio del gruppo (set colonne dedicato, rollup figli, goal aggregato) —
  spec nav-model §5, giro dedicato. Alla selezione di un gruppo resta il comportamento
  attuale.
- Ghost/adorner del nodo trascinato.
- Ordinamento manuale dei fratelli.

## 4. Testing

- Parent resolution: 3 casi (gruppo → dentro; habit → fratello; nulla → radice) per
  entrambe le voci del menu.
- `CreateGroupAsync`: crea `NodeType.Group` col parent risolto, ricarica, seleziona il
  nuovo nodo; il workspace riceve la richiesta di aprire Configura.
- "Nuova habit": `PickAsync` riceve il parentId risolto (fake launcher).
- `IsDropTarget`: la logica di transizione del behavior (set su Onto+gruppo, clear su
  cambio riga/leave/drop) nella parte testabile senza UI; il resto in verifica visiva.
- Verifica visiva (skill `run`): menu ＋Nuova▾, creazione nei 3 contesti di selezione,
  rinomina immediata in Configura, highlight del gruppo durante il drag, due temi.
