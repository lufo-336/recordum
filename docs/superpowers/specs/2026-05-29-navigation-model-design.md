# Recordum — Modello d'interazione "A evoluto" (multi-pannello)

- **Data**: 2026-05-29
- **Stato**: design approvato in brainstorming, in attesa di review dello spec
- **Fase**: 4 (Polish/UX) — issue di riferimento **B8** (navigazione) + scopribilità gruppi
- **Branch**: `fase-4-polish`

---

## 1. Problema

Dal test della build M3.10b (report utente "Recordum problemi 2"):

- Aprendo il dettaglio di una habit non c'è un modo evidente di **chiuderlo** o di **tornare alla Home**; l'interfaccia "si incasina" man mano che si aprono pannelli.
- Cliccare "a vuoto" nella lista **non** deseleziona (i pannelli restano aperti).
- Le viste di una habit (Cronologia / Dettaglio / Note) sono **tab mutuamente esclusive**: non puoi vedere i dati *mentre* scrivi le note.
- I **gruppi** sembrano assenti: in realtà esistono nel motore (rendering albero, opzione "Blank group" nel picker, drag-drop per annidare) ma sono **non scopribili**.

## 2. Decisione

Adottare un master–detail multi-pannello ("**Modello A evoluto**"):

- A sinistra un **albero persistente** di habit e gruppi (il navigatore, sempre presente).
- A destra un **workspace del soggetto** selezionato, in cui le viste della habit sono **pannelli a interruttore** (toggle) che si aprono come **colonne affiancate** e si richiudono ricliccando.

Modelli scartati: **tab persistenti stile Obsidian** (potenza da editor multi-documento, non serve a un tracker — YAGNI) e **pagina piena + Indietro** (fa perdere la vista d'insieme dell'albero).

## 3. Componenti

### 3.1 Albero persistente (sinistra)
- Gruppi espandibili (▾/▸) con conteggio figli; habit annidate con barretta colore a 4px; habit standalone.
- Resta il filtro **Attive / Archiviate / Cestino** in cima.
- È sempre visibile nel workspace Home: è la lista delle "cose aperte". Cambiare habit = un clic.

### 3.2 ＋ Nuova ▾ (Habit / Gruppo)
- Il bottone "Nuova" diventa un menu split con due voci: **Nuova habit** e **Nuovo gruppo**.
- Fa emergere il path "Blank group" oggi sepolto nel picker template.
- Creando con un **gruppo selezionato**, il nuovo nodo nasce *dentro* quel gruppo.
- Il drag-drop per annidare (già esistente in `TreeDragDropBehavior`) viene reso evidente: highlight del gruppo bersaglio all'hover.

### 3.3 Workspace del soggetto (destra dell'albero)
Quando un nodo è selezionato:
- una **barra** in alto col nome del nodo + un insieme di **chip toggle**;
- sotto, le **colonne-vista aperte**, affiancate orizzontalmente, ciascuna con header e pulsante **✕**.

Le chip sono toggle: clic apre la colonna, riclic la chiude (stato "aperta" evidenziato in accent).

### 3.4 Colonne disponibili
Per una **habit**: `Cronologia` · `Dettaglio` · `Note` · `⚙ Configura`.
- **Cronologia** = ex tab Completion: heatmap mini + quick-add + timer + lista completamenti (dove si *registra*).
- **Dettaglio** = analytics: year-heatmap, streak, trend, distribuzione oraria, goal.
- **Note** = editor markdown (`NotesTabView`).
- **Configura** = l'editor habit (oggi `EditorPane`), portato come colonna anziché pannello separato (min-width più larga per via della progressive disclosure a 3 livelli).

### 3.5 Affordance di chiusura
- **✕** sull'header di ogni colonna (equivale a ri-toggle della chip).
- **Esc** chiude l'ultima colonna aperta.
- **Clic a vuoto** nell'albero deseleziona → chiude tutte le colonne → si torna alla sola Home.

### 3.6 Rebind al cambio selezione
Cambiando nodo nell'albero, **il set di colonne aperte resta** e il contenuto si **riassocia** alla nuova selezione (es.: tieni "Note" aperto e salti tra habit per annotarle in fila).

### 3.7 Colonna di default e persistenza
- All'apertura di una habit (da nessuna colonna aperta) si apre di default **Cronologia**.
- Il **set di colonne aperte** è persistito tra un avvio e l'altro in un `AppSetting` (es. `ui.open_columns`).
- **Estensione prevista** (non bloccante): preferenza utente per scegliere quale/quali colonne aprire di default. Lo spec lascia il gancio; la UI di scelta può arrivare in un secondo momento.

### 3.8 Layout e overflow
- Le colonne hanno una **min-width** sensata; quando eccedono la larghezza disponibile → **scroll orizzontale** del workspace soggetto (l'albero resta fisso).

## 4. Architettura (mappatura sul codice attuale)

- **`MainWindow.xaml`**: oggi colonne fisse `[Home][splitter][HabitScreen][splitter][EditorPane]`. Diventa `[Home][splitter][SubjectWorkspace]`, dove `SubjectWorkspace` ospita chip-bar + una sequenza dinamica di colonne in `ScrollViewer` orizzontale. L'`EditorPane` non è più una colonna fissa di MainWindow: diventa una delle colonne gestite dal workspace.
- **`HabitScreen.xaml`**: il `TabControl` a 3 tab viene sostituito da: header con chip-bar + un host di colonne (es. `ItemsControl` orizzontale legato a una collezione di "colonne aperte"). I `DataTemplate` esistenti (Heatmap, QuickAdd, CompletionList, Detail, Notes) si riusano dentro le rispettive colonne.
- **Stato**: una collezione osservabile di colonne aperte (enum `HabitColumn { Cronologia, Dettaglio, Note, Configura }`) con comando `ToggleColumn(HabitColumn)`. Vive nel ViewModel del workspace soggetto (estensione di `HabitScreenViewModel`/`MainWindowViewModel`). `HabitScreenViewModel` già espone `Heatmap/CompletionList/TimerControl/Detail/Notes`; va aggiunto il binding dell'editor (`HabitEditorViewModel`) come colonna.
- **Selezione**: resta guidata da `Home.SelectedRow` → `HabitScreen.LoadAsync`. Aggiungere la deselezione su clic a vuoto (vedi 3.5) e il rebind che mantiene il set di colonne.
- **Persistenza**: serializzare il set di colonne aperte su `AppSetting` (`IAppSettingService`), caricato in `MainWindowViewModel.InitializeAsync`.
- **Chrome**: le chip e gli header colonna usano i brush di tema (`Brush.Surface`/`Border`/`Accent`) già introdotti in `BaseStyles.xaml` (Pass 2).

## 5. Gruppi

- **In scope ora** (sono "basi"): presenza dei gruppi nell'albero, creazione via **＋ Nuova ▾ → Nuovo gruppo**, annidamento via drag-drop reso evidente.
- **Rinviato** (decisione utente "prima le basi"): il **design della vista di dettaglio di un gruppo** (quali colonne/chip mostra un gruppo selezionato, rollup figli + goal aggregato). Per ora un gruppo selezionato può mostrare un set ridotto o nessuna colonna; lo definiremo in un giro dedicato.

## 6. Fuori scope (di questo spec)

Tracciati separatamente nel report e in `docs/phase-4-issues.md`:
- **B4** refresh lista al "Sposta nel cestino".
- **B5** registrazione valore non funziona (scala 1–5, "+" inerte).
- **B6** template indistinguibili / nessun template personalizzato.
- **B7** DatePicker/Calendar chiaro-su-chiaro (theming).
- **B9** i18n: finestra template in inglese.
- Confronto **affiancato di due habit diverse** (split detail) — esplicitamente rimandato.
- Vista di dettaglio dei **gruppi** (vedi §5).

## 7. Testing

- ViewModel del workspace: `ToggleColumn` apre/chiude idempotente; default = Cronologia alla prima apertura; il set sopravvive al cambio selezione (rebind); persistenza round-trip su `AppSetting` fake.
- Deselezione: clic a vuoto → `SelectedRow == null` → nessuna colonna aperta.
- Creazione gruppo: `＋ Nuova ▾ → Nuovo gruppo` crea un `NodeType.Group`; con gruppo selezionato il nuovo nodo ha `ParentId` corretto.
- Verifica visiva (skill `run`): chip toggle, colonne affiancate, ✕/Esc/clic-a-vuoto, scroll orizzontale, rebind.

## 8. Domande aperte / rinviate

- Vista di dettaglio dei gruppi (§5).
- UI per scegliere le colonne di default (§3.7) — gancio previsto, UI dopo.
- Larghezze colonna ridimensionabili e persistite per-colonna (oggi esistono `EditorPaneWidth`/`HabitScreenWidth`; valutare se generalizzare).
