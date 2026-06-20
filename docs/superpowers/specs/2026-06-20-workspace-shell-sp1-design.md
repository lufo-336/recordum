# Recordum — Shell di navigazione a tab (Workspace SP1)

- **Data**: 2026-06-20
- **Stato**: design approvato in brainstorming, in attesa di review dello spec
- **Fase**: 4 → transizione. Questo lavoro **sblocca il freeze v1.0** (decisione autore 2026-06-20): la shell workspace diventa un milestone vero. Roadmap/CLAUDE.md vanno aggiornati in fase di piano (vedi §10).
- **Branch**: `fase-4-polish` (o un branch dedicato `workspace-shell` se si preferisce isolarlo)
- **Sostituisce**: `docs/superpowers/specs/2026-05-29-navigation-model-design.md` (modello "A evoluto" a colonne toggle). Quel documento resta come storia della decisione; questo spec ne è l'evoluzione.

> **Decomposizione.** La visione completa (shell + pannelli componibili + editor diario gradevole) è troppo grande per un solo spec. È scomposta in tre sotto-progetti, ognuno con il proprio ciclo spec→piano→build:
> - **SP1 — Shell di navigazione (questo spec):** sidebar albero ricca + tab in alto + pannelli per-tab come colonne ridimensionabili + modalità focus + pannello Diario dedicato + persistenza leggera.
> - **SP2 — Pannelli componibili veri:** split arbitrari orizzontali/verticali, drag-to-dock, layout per-tab. *Fuori scope qui.*
> - **SP3 — Editor diario gradevole:** tipografia, modalità zen, pulizia chrome. *Fuori scope qui.*
> Ordine di build: **1 → 3 → 2**.

---

## 1. Problema

Origine: revisione UX del diario (2026-06-20). Dopo aver cablato la feature Journal/Diario (modello B), usarla risulta sgradevole su tre fronti dichiarati dall'autore:

1. **Spazio di scrittura angusto** — l'editor diario è un box da ~280px schiacciato nella colonna Abitudine, accanto ad albero + calendario.
2. **Layout rigido** — le viste di una habit sono colonne a ordine fisso; non si possono comporre/riorganizzare come in un'app stile Obsidian.
3. **Editor poco gradevole** — la superficie di scrittura non è curata per la scrittura riflessiva (affrontato in SP3, non qui).

L'ipotesi YAGNI del 2026-05-29 ("tab persistenti stile Obsidian non servono a un tracker") è **superata dai fatti**: con il diario come attività di prima classe, la scrittura multi-documento diventa un caso d'uso reale, ed è esattamente dove i tab hanno senso.

**Vincolo dell'autore:** la Home ricca **non** va buttata e l'albero delle abitudini **non** deve degradare a una lista piatta.

## 2. Decisione

Adottare una **shell workspace a tab** (modello "Y" del brainstorming):

- A sinistra, una **sidebar con l'albero ricco di oggi** (stesso componente, piena fedeltà), sempre presente e **collassabile**.
- In alto, una **barra di tab** stile browser: ogni tab è un *soggetto aperto* (habit, gruppo o la Panoramica). La Panoramica è il tab di default quando non c'è nulla aperto.
- Dentro un tab, i pannelli-vista (riuso di quelli esistenti) come **colonne affiancate e ridimensionabili**, con barra di **chip toggle** per aprirli/chiuderli e una **modalità focus** (⤢) che espande un pannello a tutto il tab.
- Per le habit **journal**, un **pannello editor "✎ Diario" dedicato**, separato da Cronologia e Note, che in focus diventa una superficie di scrittura ampia e pulita.

## 3. Componenti

### 3.1 Sidebar albero (sinistra)
- **Stesso componente dell'attuale Home tree**: gruppi espandibili (▾/▸) con conteggio figli, habit annidate con barretta colore a 4px, habit standalone. Filtro **Attive / Archiviate / Cestino** in cima. ＋ Nuova ▾ (Habit / Gruppo) come da spec 2026-05-29 §3.2 (se non già a posto).
- **Collassabile** via il toggle ≡ nella topbar: collassata mostra solo un rail sottile (riespandibile con un clic). Lo stato (espansa/collassata) + larghezza è persistito (§6).
- **Non** è una lista piatta: mantiene tutta la struttura ad albero.

### 3.2 Barra dei tab (in alto)
- Sequenza orizzontale di tab + un pulsante `＋` (apre un menu "Nuova habit / Nuovo gruppo" oppure un selettore rapido — dettaglio in piano; il minimo è coerente con ＋ Nuova ▾).
- Ogni tab: icona/colore del soggetto + nome + `✕` per chiudere.
- **Riordino con drag**. **Cambio** con clic o `Ctrl+Tab`. **Chiusura** con `✕`, click centrale, o `Ctrl+W`.
- La **Panoramica** è il tab di default/àncora: presente quando nessun soggetto è aperto; riappare se si chiude l'ultimo tab. Non duplicabile.

### 3.3 Modello di apertura tab — anteprima + pin (decisione "B")
- **Singolo clic** su un nodo dell'albero → apre un **tab di anteprima** (reso in *corsivo*). Un successivo singolo clic su un altro nodo **rimpiazza** il tab di anteprima invece di accumularne uno nuovo → esplorazione senza esplosione di schede.
- **Promozione a tab fissato** (normale, non rimpiazzabile) quando: doppio-clic sul nodo/tab, **oppure** la prima azione che modifica/registra qualcosa nel soggetto (es. salvare una voce di diario, registrare un completamento, editare config).
- Al più **un** tab di anteprima per volta.
- Solo i **tab fissati** sono persistiti (§6); l'anteprima è transitoria di sessione.

### 3.4 Pannelli dentro un tab
- Una **barra di chip toggle** in cima al contenuto del tab accende/spegne i pannelli; il pannello aperto è evidenziato in accent (riuso del meccanismo `ToggleColumn` esistente, ora con scope per-tab).
- I pannelli aperti sono **colonne affiancate ridimensionabili** (riuso delle min-width e dello scroll orizzontale dell'attuale `HabitScreen`).
- **Pannelli disponibili per tipo di soggetto** (riuso delle regole `IsColumnApplicable` esistenti, estese):
  - **Habit (non-journal):** `Cronologia` · `Dettaglio` · `Note` · `⚙ Configura`.
  - **Habit journal:** `✎ Diario` · `Cronologia` · `Dettaglio` · `Note` · `⚙ Configura`.
  - **Gruppo:** `Panoramica` · `Note` · `⚙ Configura`.
- **Pannello di default** all'apertura di un soggetto (da zero pannelli):
  - habit non-journal → **Cronologia**
  - habit journal → **✎ Diario**
  - gruppo → **Panoramica**

### 3.5 Pannello "✎ Diario" dedicato (solo habit journal)
- Contiene l'editor markdown della voce di diario (l'attuale `JournalQuickAddViewModel` → `IDiaryService`), **estratto** dalla colonna Cronologia: Cronologia resta calendario + lista + (per i non-journal) quick-add; il Diario è la sola superficie di scrittura.
- È la base strutturale su cui SP3 innesterà tipografia/zen. SP1 si limita a dargli un pannello proprio focalizzabile.

### 3.6 Modalità focus (⤢)
- Ogni header di pannello ha un toggle **⤢**: espande quel pannello a riempire tutto il tab (gli altri pannelli e la chip-bar si riducono a un ritorno "↩ Esc").
- **Esc** o il toggle **⤡** ripristina il layout multi-pannello precedente.
- Lo stato focus è **di sessione**, non persistito (persistenza leggera, §6).

### 3.7 Affordance di chiusura / navigazione
- **✕** su ogni tab; **Ctrl+W** chiude il tab attivo; chiudere l'ultimo tab → torna a Panoramica.
- **✕** su ogni header di pannello (= ri-toggle della chip).
- **Esc** esce dal focus se attivo, altrimenti chiude l'ultimo pannello aperto del tab.
- **I tab sono indipendenti dalla selezione dell'albero**: cliccare un nodo apre/attiva il suo tab; non "riassocia" i pannelli di un tab esistente. Questo **supera** il meccanismo di rebind del modello 2026-05-29 §3.6. (Clic a vuoto nell'albero deseleziona la riga ma **non** chiude i tab.)

## 4. Architettura (mappatura sul codice attuale)

- **`MainWindow.xaml`**: da `[Home][splitter][HabitScreen][splitter][EditorPane]` a **`[SidebarTree][splitter][TabHost]`**. `EditorPane` non è più una colonna fissa: la `⚙ Configura` diventa un pannello dentro un tab.
- **Nuovo `WorkspaceViewModel`** (estensione/erede di `MainWindowViewModel`): possiede
  - la collezione osservabile di **tab aperti** (`ObservableCollection<SubjectTabViewModel>`),
  - il **tab attivo**, il **tab di anteprima** corrente,
  - i comandi `OpenSubject(node, pin:false)`, `PinTab`, `CloseTab`, `ActivateTab`, `ReorderTab`,
  - lo stato/azione della **sidebar** (collasso, larghezza) e il riuso del tree navigator esistente.
- **`SubjectTabViewModel`**: incapsula un `HabitScreenViewModel` (riuso) per il soggetto del tab, più lo stato per-tab dei pannelli aperti (`HabitColumn` set, ora locale al tab) e il pannello in focus. I `DataTemplate` esistenti (QuickAdd, CompletionList, Detail, Notes, Editor) si riusano dentro le colonne.
- **Pannello Diario**: si introduce un `HabitColumn.Diario` (o equivalente) applicabile solo alle habit journal; il template ospita `JournalQuickAddViewModel`. La Cronologia per le journal **non** mostra più l'editor di scrittura (resta calendario + lista).
- **Selezione**: `SidebarTree.SelectedRow` → `WorkspaceViewModel.OpenSubject(...)` (preview), non più un caricamento diretto di `HabitScreen`.
- **Persistenza**: nuova chiave `AppSetting` **`ui.workspace`** (JSON) che **sostituisce** `ui.open_columns`; vedi §6. Caricata in `WorkspaceViewModel.InitializeAsync`. Prevedere migrazione/ignore-graceful del vecchio `ui.open_columns` (se presente, lo si può scartare: lo stato per-tab non è retro-mappabile).
- **Chrome**: tab, chip e header pannello usano i brush di tema esistenti (`Brush.Surface`/`Border`/`Accent`).

## 5. Tipi di soggetto e Panoramica

- **Habit** e **Gruppo** aprono come tab con i pannelli applicabili (§3.4).
- **Panoramica**: tab speciale di default. In SP1 mostra ciò che la Home mostra oggi a livello di panoramica (riuso `PanoramicaViewModel`); l'albero **non** vive dentro la Panoramica (vive nella sidebar). Quindi la "vecchia Home" si scinde: **albero → sidebar permanente**, **panoramica → tab di default**.
- La **vista di dettaglio dei gruppi** (quali pannelli/rollup mostra un gruppo) resta come da 2026-05-29 §5: **rinviata** a un giro dedicato. In SP1 un gruppo mostra il set ridotto di §3.4.

## 6. Persistenza (leggera — decisione utente)

Chiave `AppSetting` `ui.workspace`, JSON con:
- `sidebar`: `{ collapsed: bool, width: int }`
- `tabs`: lista ordinata dei **soli tab fissati**, ciascuno `{ subjectId: GUID, kind: "Habit"|"Group" }`. La **Panoramica è sempre presente e implicita**: non viene serializzata nella lista.
- `activeTabId`: `GUID` di un tab fissato, oppure la stringa sentinella `"panoramica"` se all'ultima chiusura era attiva la Panoramica. Se l'`activeTabId` salvato non risolve più (soggetto cancellato), si ripiega su Panoramica.

**Non** si persistono: il tab di anteprima, il set di pannelli aperti per-tab, le larghezze dei pannelli, lo stato di focus. Alla riapertura ogni tab riparte con il **pannello di default** del suo tipo (§3.4). Questo è il compromesso "Leggera" scelto: la sessione di navigazione (quali soggetti, quale attivo, sidebar) torna; il micro-layout no.

## 7. Fuori scope (di SP1)

- **Split arbitrari orizzontali/verticali + drag-to-dock + layout per-tab** → **SP2**.
- **Tipografia / modalità zen / pulizia chrome dell'editor diario** → **SP3**.
- **Persistenza piena del layout per-tab** (pannelli/larghezze/focus) → eventuale estensione futura.
- **Vista di dettaglio dei gruppi** (rollup figli + goal aggregato) → giro dedicato.
- **Tab di tipo "layout salvato/workspace"** (il "modello 2" del brainstorming) → eventuale estensione futura; SP1 fa solo tab = soggetto aperto.
- **Confronto affiancato di due habit diverse** in un tab → arriva con SP2.

## 8. Testing

ViewModel (`WorkspaceViewModel` / `SubjectTabViewModel`) su fake services:
- **Apertura/anteprima**: singolo clic apre un tab preview; un secondo singolo clic lo rimpiazza (non accumula); doppio-clic o prima modifica promuove a fissato.
- **Default pannello**: prima apertura di journal → Diario; non-journal → Cronologia; gruppo → Panoramica.
- **Pannelli per-tab**: `ToggleColumn` idempotente e **locale al tab** (aprire Note nel tab A non lo apre nel tab B).
- **Focus**: ⤢ porta un pannello a "solo", Esc/⤡ ripristina il set precedente.
- **Chiusura**: `CloseTab` rimuove il tab; chiudere l'ultimo → Panoramica attiva; clic a vuoto nell'albero non chiude i tab.
- **Indipendenza dalla selezione**: cambiare nodo nell'albero apre/attiva il suo tab senza riassociare i pannelli di un altro tab.
- **Persistenza**: round-trip di `ui.workspace` su `AppSetting` fake (tab fissati + attivo + sidebar); il tab di anteprima e il layout per-tab **non** vengono salvati.
- **Diario estratto**: per una journal, l'editor di scrittura è nel pannello Diario, **non** in Cronologia.

Verifica visiva (skill `run`): tab preview/pin (corsivo→normale), riordino drag, chip toggle per-tab, focus ⤢/Esc, sidebar collassabile che resta albero ricco, pannello Diario in focus come superficie ampia.

## 9. Domande aperte / rinviate

- Comportamento esatto del pulsante `＋` nella tab-bar (menu Nuova vs selettore rapido) — da fissare in piano.
- Migrazione del vecchio `ui.open_columns`: scartare vs tentativo di mappatura (proposta: scartare).
- Vista di dettaglio dei gruppi (§5) e tab "layout salvato" (§7) — sotto-progetti/giri dedicati.
- Se SP1 introduca già scorciatoie complete (`Ctrl+W`, `Ctrl+Tab`, click-centrale) o solo le essenziali — proposta: includere `Ctrl+W`/`Ctrl+Tab`, click-centrale opzionale.

## 10. Impatto su roadmap / freeze

Questo spec **sblocca formalmente il freeze "v1.0 prima delle feature grandi"** (deciso 2026-06-16, ribaltato 2026-06-20 dall'autore). In fase di piano vanno aggiornati:
- `CLAUDE.md` §7 (Fase 4): annotare la shell workspace come milestone in corso e il riferimento a questo spec + ai sotto-progetti SP1/SP2/SP3.
- Il tracker `docs/phase-4-issues.md`: collegare la revisione UX diario a questo lavoro.
- La memoria di progetto (`recordum-fase-4-polish-ux`, `recordum-journal-diary-feature`).
