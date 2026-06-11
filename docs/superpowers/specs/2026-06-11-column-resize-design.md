# Recordum — Larghezze colonna ridimensionabili (nav-model §8)

> Chiude la domanda aperta §8 dello spec `2026-05-29-navigation-model-design.md`: larghezze
> colonna del workspace ridimensionabili dal confine verticale e persistite per-colonna.
> Richiesta utente durante la verifica visiva del workspace a colonne (2026-06-11).

## 1. Problema

Le 4 colonne del workspace (Cronologia/Dettaglio/Note/Configura) hanno larghezza fissa
(460/520/460/480). L'utente vuole ridimensionarle trascinando il loro confine verticale,
come ci si aspetta da qualsiasi pannello desktop.

## 2. Decisione

**Thumb di drag sovrapposto al bordo destro di ogni colonna** (approccio A). Il layout
attuale — `StackPanel` orizzontale dentro `ScrollViewer` — resta intatto; trascinando la
maniglia cambia la larghezza della sola colonna interessata e le successive scorrono.

Scartati: Grid+`GridSplitter` (dentro uno ScrollViewer a misura infinita si comporta male,
e ogni colonna toggle-abile richiederebbe di collassare anche la colonna-splitter adiacente);
resize della sola Configura (non risponde alla richiesta).

## 3. Comportamento

- **Drag** della maniglia (~6px, cursore ↔, sovrapposta al bordo destro della colonna):
  la larghezza segue il mouse, clamp **min 320 / max 1000** px.
- **Doppio click** sulla maniglia: reset alla larghezza di default della colonna
  (Cronologia 460, Dettaglio 520, Note 460, Configura 480).
- **Persistenza**: le larghezze sono globali (non per-habit) e sopravvivono al riavvio.
  Scrittura **a fine drag** (`DragCompleted`) e al reset, non a ogni pixel.
- **Hover**: la maniglia evidenzia leggermente (`Brush.Accent`) per affordance.

## 4. Architettura

- **`HabitScreenViewModel`**: 4 proprietà osservabili `CronologiaWidth` / `DettaglioWidth`
  / `NoteWidth` / `ConfiguraWidth` (double, default come sopra, clamp nel setter).
  Persistenza su AppSetting **`ui.column_widths`** in CSV `Cronologia=460,Dettaglio=520,…`
  via `PersistColumnWidthsAsync()` invocata dalla view a fine drag; restore allo startup
  dentro `RestoreOpenColumnsAsync` (stessa finestra di caricamento di `ui.open_columns`),
  token malformati o fuori range ignorati → default. Nessuna migrazione (chiave creata
  on-write, pattern di `ui.open_columns`).
- **`HabitScreen.xaml`**: `Width` dei 4 `Border` colonna bindata alle proprietà
  (`Mode=OneWay`); dentro ogni colonna un `Thumb` (stile condiviso
  `HabitScreen.ColumnResizeThumb`, `HorizontalAlignment=Right`, larghezza 6,
  `Cursor=SizeWE`, sfondo trasparente, hover accent) sopra il contenuto.
- **`HabitScreen.xaml.cs`**: handler `DragDelta` (aggiorna la proprietà della colonna dal
  `Tag`/parametro), `DragCompleted` (persiste), `MouseDoubleClick` (reset + persiste).
  Il code-behind individua la colonna via `Thumb.Tag` = `HabitColumn`.

## 5. Fuori scope

- Larghezze per-habit (sono globali).
- Resize da tastiera / accessibilità della maniglia (rientra in R5 audit accessibilità).
- Ridimensionare la colonna Home/albero (ha già il suo splitter in `MainWindow`).
- Preferenze UI per i default (gancio §3.7 dello spec nav-model, invariato).

## 6. Testing

- VM: clamp min/max nel setter; round-trip CSV su `FakeAppSettingService`
  (set → persist → nuovo SUT → restore → stesse larghezze); token corrotti/fuori range
  ignorati (restano i default); reset riporta al default della singola colonna.
- Drag reale non testabile a VM → verifica visiva (skill `run`): drag su ogni confine,
  doppio click reset, persistenza tra riavvii, comportamento con colonne chiuse/riaperte
  e con scroll orizzontale attivo, due temi.
