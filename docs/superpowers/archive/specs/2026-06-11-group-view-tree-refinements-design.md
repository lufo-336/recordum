# Recordum — Vista gruppo (Panoramica) + rifiniture albero/colonne + selezione multipla

> Chiude nav-model §5 (`2026-05-29-navigation-model-design.md`) e un pacchetto di rifiniture
> UX segnalate dall'utente il 2026-06-11 durante la verifica dei gruppi-basi. Tre pacchetti:
> **A** colonna Panoramica per i gruppi, **B** rifiniture albero/colonne (5 punti),
> **C** selezione multipla stile file-manager. Esecuzione in tre piani separati (B → A → C).

## A. Colonna Panoramica (vista gruppo, nav-model §5)

### A.1 Chip dinamiche per tipo nodo
- `HabitColumn` guadagna **`Panoramica`**.
- Gruppo selezionato → chip `Panoramica · Note · Configura`; habit → le 4 attuali
  (`Cronologia · Dettaglio · Note · Configura`). Panoramica non compare mai per una habit.
- Le colonne **non applicabili** al tipo corrente sono nascoste anche se il loro flag è
  aperto: il set aperto si conserva intatto quando si torna a una habit (visibilità =
  flag ∧ applicabilità, nessun reset dei flag).
- Default per un gruppo senza colonne applicabili aperte → **Panoramica** (per le habit
  resta la logica di B.5).

### A.2 Contenuto
`PanoramicaViewModel` (nuovo, posseduto da `HabitScreenViewModel`):
- Conteggio **"oggi x/y"**: habit discendenti completate oggi / totale habit discendenti.
- **Riga per ogni habit discendente** (ricorsivo, esclusi i sottogruppi come righe ma
  inclusi i loro figli habit): nome, colore (barretta 4px), stato completata-oggi,
  sparkline 7gg. Dati da `IHabitTreeQueryService.GetHomeTreeAsync()` filtrati per
  discendenza — nessuna query nuova.
- **Card Goal di gruppo**: istanza propria di `GoalProgressCardViewModel` (già esistente,
  già caricabile su un gruppo).
- Gruppo vuoto (nessuna habit discendente) → empty-state testuale localizzato.

### A.3 Navigazione
Click su una riga figlia → evento `ChildSelected(Guid)` → `MainWindowViewModel` seleziona
quella habit in `Home` (il workspace si ricarica su di lei; il set colonne si conserva).

## B. Rifiniture albero/colonne

1. **Scroll dell'albero**: ScrollViewer verticale attorno all'ItemsControl delle righe
   (scrollbar flat implicita già tematizzata). Oggi le righe oltre la viewport sono
   irraggiungibili.
2. **Collasso gruppi**: il toggle ▾/▸ esiste ma è inerte (`Rebuild()` ignora
   `IsExpanded`). Fix: `Rebuild()` salta i discendenti dei gruppi collassati; lo stato
   expand/collapse vive in un set per-id in `HomeViewModel` (sessione — sopravvive ai
   reload, non ai riavvii).
3. **Click-a-vuoto sul ribbon sinistro**: lo spazio vuoto del `WorkspaceRibbon` deseleziona
   (stesso pattern del `TreeRegion`), perché con l'albero pieno non c'è più spazio vuoto
   cliccabile nella Home.
4. **Set colonne vuoto → workspace chiuso**: quando l'utente chiude l'ultima colonna
   aperta (chip/✕/Esc), la selezione si azzera e il workspace collassa.
5. **Riapertura sull'ultima colonna chiusa**: il default all'apertura di una habit non è
   più sempre Cronologia ma **l'ultima colonna chiusa**, persistita in AppSetting
   `ui.last_closed_column` (fallback Cronologia se assente/invalida; per i gruppi
   l'equivalente è Panoramica se l'ultima chiusa non è applicabile).

## C. Selezione multipla (stile file-manager)

### C.1 Interazione
- **Clic semplice** = selezione singola (comportamento attuale).
- **Ctrl+clic** = toggle della riga nel set di selezione.
- **Shift+clic** = intervallo dall'ancora (ultima riga cliccata senza modificatori)
  alla riga cliccata, nell'ordine visuale corrente dell'albero.
- Disponibile in **tutte e tre le tab** (Attive / Archiviate / Cestino).
- **Esc** con 2+ selezionate azzera la selezione; cambio tab azzera la selezione.

### C.2 Selezione e workspace
- Con **esattamente 1** riga selezionata: comportamento attuale (`SelectedRow`, workspace).
- Con **2+** selezionate: `SelectedRow = null` → il workspace si chiude (modalità bulk).
  L'ancora resta in memoria per lo Shift+clic.
- Stato: `HomeViewModel` mantiene il set di righe selezionate; il flag `IsSelected`
  per-riga (già esistente, oggi mono) riflette l'appartenenza al set.

### C.3 Azioni bulk
- **Drag-drop**: trascinare una riga che fa parte della selezione trascina **tutte** le
  selezionate; il drop in un gruppo le sposta tutte (i tentativi ciclici falliscono
  silenziosamente riga per riga, come oggi).
- **Menu contestuale**: right-click su una riga selezionata mostra le azioni applicate a
  tutta la selezione: Archivia/Ripristina-da-archivio (tab Attive/Archiviate),
  Sposta nel cestino, Ripristina e Svuota (Cestino) — le stesse visibilità per-tab di oggi.
  Right-click su una riga NON selezionata: la selezione collassa su quella riga (come nei
  file manager).
- **Tastiera**: Canc / Ctrl+E / Ctrl+R applicati a tutta la selezione (stessi tasti di oggi).
- Le azioni bulk girano in sequenza sulle righe selezionate riusando i service esistenti
  (`SetArchivedAsync`, `SoftDeleteAsync`, `RestoreAsync`, `PurgeSubtreeAsync`, `MoveAsync`)
  — un solo reload alla fine.

### C.4 Fuori scope
- Ctrl+A / selezione lasso (rubber band).
- Pin/rename bulk (rename non ha senso multiplo; pin resta singolo).
- Bulk su righe-gruppo miste a habit: consentito — le azioni esistenti già operano su
  entrambi i tipi (cascata inclusa).

## Ordine di esecuzione

Tre piani: **Piano 1 = B** (rifiniture, sblocca i fastidi quotidiani), **Piano 2 = A**
(Panoramica), **Piano 3 = C** (selezione multipla). B e A non dipendono da C; C tocca il
modello di selezione ma `SelectedRow` resta l'API verso il workspace, quindi A/B non
vanno rifatte.

## Testing

- **A**: chip set per tipo nodo (gruppo/habit), default Panoramica su gruppo, rollup
  (conteggio oggi, righe discendenti ricorsive, esclusione sottogruppi come righe),
  navigazione ChildSelected, empty-state. VM-testabile con i fake esistenti.
- **B**: Rebuild salta i discendenti collassati (e lo stato sopravvive al reload);
  set vuoto → SelectedRow null; riapertura usa `ui.last_closed_column` (round-trip,
  token invalido → Cronologia). Scroll e click-ribbon: verifica visiva.
- **C**: transizioni del set (clic/Ctrl/Shift nei 3 ordini), SelectedRow null con 2+,
  azioni bulk chiamano i service per ogni id selezionato + reload singolo, right-click
  fuori selezione collassa, Esc/cambio-tab azzerano. Drag multiplo: verifica visiva.
- Verifica visiva complessiva con skill `run` su entrambi i temi.
