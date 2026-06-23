# Recordum — Fase 4 issue tracker

> **Scopo**: lista vivente dei bug e delle rifiniture aperte in Fase 4. Sostituisce le sezioni "Bloccanti / Rifinitura" che erano in `CLAUDE.md` §7. CLAUDE.md mantiene solo il pointer.
>
> **Stati**: `reported` (utente lo segnala, non ancora investigato) → `verified` (riprodotto da claude / utente conferma stato attuale) → `triaged` (root cause ipotizzata + scope chiaro) → `in-progress` (fix in lavorazione su un branch) → `fixed` (commit fatto, build verde) → `verified-fixed` (utente conferma sul build).
>
> Quando un issue raggiunge `verified-fixed`, spostalo in fondo nella sezione "Risolti" con il commit SHA del fix.

## Verifica visiva v1.0 — in attesa di resoconto utente (2026-06-19)

App lanciata via skill `run` (build win-x64 verde). Aree da controllare a schermo, da consolidare nel "resoconto stato app" (agenda punto 3) e da triare contro i CR:
1. **Grafici** — sappiamo rotti: sintomo esatto (errore / area vuota / crash) e in quale schermata.
2. **Export** (PDF/HTML/Markdown/JSON/CSV) — parte? produce file leggibile?
3. **Plugin** — qualcosa di visibile/funzionante?
4. **Backup/Restore** — area dei findings gravi CR1–CR4; testare su dati di prova.
5. **Basics**: 5 tipi habit, registrazione, calendario/heatmap, streak, gruppi, note.
6. **UX/theming** — cosa non sembra "da v1.0".

## Stato al 2026-06-23 — CR1–CR4 chiusi + appearance pass (font Palatino), sessione da macOS

Sessione svolta da Mac (vincoli e checklist di verifica VM: `docs/dev-environment-sync.md` §"Log sessione 2026-06-23"). 3 commit su `fase-4-polish`.

**Sicurezza / perdita-dati — CR1–CR4 `fixed` (commit `24c0e60`)**: restore atomico + rollback (CR1), guard `schema_version` reale letto dal DB con confronto major.minor (CR2), bound sui parametri Argon2id non fidati + cap anti zip-bomb (CR3), shred dei temp in chiaro (CR4). Nuovi helper `SchemaVersionGuard`, `SecureWipe`. Test `SchemaVersionGuardTests` + casi CR3 di `AesGcmBackupCipherTests` verdi (eseguiti via `osx-arm64`). **Restano da `verified-fixed` sulla VM**: suite Data completa + restore-corrotto-a-metà (CR1) + reject schema più recente (CR2). Vedi le righe CR1–CR4 qui sotto.

**Appearance pass — `fixed` (commit `3497021`), verifica visiva VM**:
- **Editor diario** (Toast UI/WebView2): segue il tema app **a caldo** (tripwire `SetResourceReference` su `Brush.Surface`) e ora anche il **font** (secondo tripwire su `Font.Default` + push C#); superficie di scrittura in **serif**. Chiude il sotto-bucket "tema editor" di **B2**.
- **R7. Rinnovamento textbox — `fixed`**: lo stile implicito underline (Obsidian-like) era già su `TextBox`; allineati i due residui — **`PasswordBox`** (Impostazioni/Backup/Restore) e il **preview read-only di `NotesTabView`**. I 4 campi volutamente borderless (titolo/descrizione editor, inline settings) restano per scelta. Audit: 44 TextBox → 39 ereditano l'underline, 5 con override locale (4 voluti + NotesTabView ora sistemato). Disegno di riferimento: `docs/diario-ui-preview.html` §2 ("vale per tutte").
- **R8. Font UI Palatino + switcher — `fixed`**: font classico/serif richiesto dall'utente. `Tokens.xaml` → `Font.System`/`Font.Serif`/`Font.Default`; `Window`/`Page` lo ereditano (DynamicResource); `Consolas` (timer/diagnostica) resta monospace. **Switch utente** in **Impostazioni → Aspetto** via `AppFontService` (mirror di `ThemeService`: persiste `ui.font`, default Serif, evento `FontChanged` → `App.ApplyFont`). Il diario segue via push. Test `AppFontServiceTests` (compile-only su Mac; run su VM).

**AppSetting nuovo**: `ui.font` (runtime, non seedato, come `theme.mode`) — non cambia il conteggio dei seed.

**Anteprime browser** (per giudicare il look lato Mac, non sono UI spedita): `docs/editor-theme-preview.html`, `docs/font-preview.html`, `docs/diario-ui-preview.html`.

### Continuazione 2026-06-23 (da Mac) — chiusi CR5, CR10, CR11; CR8 triato→VM

Sessione da Mac (VM non accessibile). Triage dei CR Data-layer: **i numeri di riga di CR5–CR24 sono stale** (review pre-refactor; es. `HabitTemplateService` ora 263 righe non 1349) — corretti man mano. **Scoperta**: lo schema reale a HEAD è **2.11** (`NoteCategoryV211`, 2026-06-19), non 2.10 — CLAUDE.md §1 va aggiornata (note v2.11: colonne `category`/`completion_id` su `notes`, check `ck_note_category`).

- **CR5** `fixed` (Data verificato osx-arm64 + WPF compila; **resa WPF da verificare a vista VM**).
- **CR10** `fixed` (refactor `ValidateAndReparentAsync`, 19/19 verdi).
- **CR11** `fixed` (ancoraggio a `createdDate` + filtro `End>=fromDate`; 7/7 verdi). Si è rivelato **non solo latente**: tocca `ExportQueryService.BuildSummaryAsync`.
- **CR8** `triaged`→**VM**: il fix richiede rigenerare il ModelSnapshot (`dotnet ef`, non su Mac); impatto runtime zero.

Suite Data: **338 pass**, 17 fail = solo Backup/Security (DPAPI/SQLCipher Windows-only, `PlatformNotSupportedException`, pre-esistenti). Aree toccate tutte verdi. Primo commit `fe6cb9d`.

**Secondo giro — CR6/CR7/CR9** (stessa sessione):
- **CR7** `fixed`: `MaterializeMetricsSegmentsGoalAsync` su singolo context+transazione (da N+3 a 1), rimossi 2 servizi inutilizzati dal costruttore.
- **CR9** `fixed`: goal eredita il nome dell'habit invece del literal "Goal" (Data layer senza `.resx`, ereditare è la via pulita).
- **CR6** `triaged`→**WONTFIX proposto**: interpretazione dubbia (Counter è la riclassificazione voluta per resistance con metrica misurata), nessun dato first-party colpito, non fixabile su Mac né a posteriori.
- `HabitTemplateServiceTests` 9/9 verdi (osx-arm64).

**Terzo giro — CR13/CR20/CR22 (WPF) + fix compile test pre-esistente** (stessa sessione):
- **CR13** `fixed` (compile-only): ramo `Seconds` in `CompletionListViewModel.FormatValue` → durate visibili in Cronologia.
- **CR20** `fixed` (compile-only): `IClock` iniettato in `BackupDialogViewModel` (no più `DateTime.UtcNow`).
- **CR22** `fixed` (compile-only): year-heatmap a 54 settimane (era 53) → il 31 dic non sparisce più nei bisestili col bordo-settimana. L'ExportEngine **non** condivideva il bug (finestra rolling, non anno-solare).
- **Fix collaterale**: il progetto test WPF **non compilava** dalla sessione `3497021` (il costruttore di `SettingsViewModel` aveva guadagnato `IAppFontService` ma 2 file di test non erano stati aggiornati). Creato `FakeAppFontService`, aggiornati i 4 siti → `Recordum.WPF.Tests` ora **compila** (`-p:EnableWindowsTargeting=true`).
- I test WPF sono `net10.0-windows` → **compile-verified su Mac, RED→GREEN + verifica visiva da fare sulla VM**.

**Quarto giro — R-contextMenu: restyle menù tasto-destro** (richiesta utente, compile-only):
- **Problema**: `ContextMenu`/`MenuItem` avevano **solo setter di colore**, nessun `ControlTemplate` → resa col default Aero di Windows (colonna-icone grigia inutile, angoli squadrati, bordo spesso, hover blu di sistema = look datato).
- **Fix** (`Resources/BaseStyles.xaml`): template tematizzati. Pannello flottante arrotondato (raggio 8) su `Brush.SurfaceElevated` + **ombra morbida** (`DropShadowEffect`); **niente colonna-icone**; righe raggio 5 con hover/focus = **riempimento `Brush.Accent` al 14%** su layer separato (testo nitido); submenu con chevron Lucide (accento su hover) nello stesso pannello; separatori 1px (`MenuItem.SeparatorStyleKey`) invece della linea 3D. Tutti i menù app sono `ContextMenu` → ruoli `SubmenuItem`/`SubmenuHeader`, un solo template `Menu.Item` con `Popup` condiviso.
- **Anteprima approvata dall'utente**: `docs/context-menu-preview.html` (light+dark, submenu aperto, confronto col default). Build WPF verde.
- **Da verificare a vista sulla VM**: in particolare la **trasparenza degli angoli del popup** (`HasDropShadow=True` deve mantenere `AllowsTransparency` sul popup interno) e l'apertura submenu.

**Quinto giro — altri ritocchi UI datati** (richiesta utente, compile-only, anteprime approvate):
- **R-slider** (`BaseStyles.xaml`): lo `Slider` della scala State (unico nell'app, `HabitScreen.xaml:333`) non aveva stile → thumb appuntito Aero. Nuovo stile flat: traccia 4px arrotondata (`Brush.Border`), porzione raggiunta in `Brush.Accent`, thumb circolare 16px con anello morbido su hover/drag. RepeatButton/Thumb **keyed** (non impliciti) → non toccano ScrollBar/Expander. Anteprima `docs/slider-preview.html`.
- **R-brandMark** (`MainWindow.xaml`): il simbolo del programma nella title bar era l'icona Lucide `Icon.Activity` (linea-impulso). Sostituita con una **R serif Palatino** bold 18px (agganciata a `Font.Serif` → resta serif anche con UI font System), colore **`Brush.Text`** (scelta utente, non accento). `Icon.Activity` ora non più referenziata. Anteprima `docs/brand-mark-preview.html`.
- **R-comboDropdown** (`BaseStyles.xaml`): il dropdown del ComboBox era arrotondato (raggio 4) ma **senza ombra** → stonava accanto ai nuovi menù. Allineato alla stessa famiglia "popup flottante": raggio 8, padding 4, `DropShadowEffect` morbido (margine trasparente esterno + offset Popup `-6/-4` per restare allineato sotto la casella). `ComboBoxItem` ora con righe arrotondate e **hover accento 14%** su layer separato (era grigio pieno `Brush.Border`) + tinta tenue 8% sul valore selezionato. Anteprima `docs/combobox-preview.html`.

### R-columnScroll — scorrimento orizzontale colonne (regressione + ibrido fill-then-scroll)
- **Stato**: `fixed` (compile-only su Mac, run+verifica VM) · richiesta utente ("lo scorrimento delle colonne è andato perdendosi … fluida e contemporanea")
- **Root cause** (systematic-debugging): il commit **`f08fba9`** ("tab panes fill & split width") ha sostituito il modello originale (colonne a larghezza fissa dentro uno ScrollViewer orizzontale, `8bf7a56`) con **star-split** puro → i pannelli si spartiscono il tab e non sfondano mai, quindi **niente scroll** e schiacciamento con molte colonne aperte. Infrastruttura larghezze nel VM (`GetColumnWidth`, min 320/max 1000, `ui.column_widths`) + thumb resize rimasti orfani da allora.
- **Fix scelto dall'utente: ibrido "riempi-poi-scorri"** (anteprima interattiva `docs/column-scroll-preview.html`). `HabitScreen.xaml`: la griglia colonne è avvolta in uno `ScrollViewer` orizzontale (`x:Name="ColumnsScroll"`, vertical disabled); `Grid.Width="{Binding ViewportWidth, ElementName=ColumnsScroll}"` (riempie il tab → star-split senza buco) e `Grid.MinWidth="{Binding OpenColumnsMinWidth}"` (= colonne aperte × 320 → oltre quella soglia lo ScrollViewer scrolla invece di schiacciare). Nuova proprietà VM `OpenColumnsMinWidth` (notificata in `NotifyShowFlags`). I GridSplitter star restano per il resize fine. Test `OpenColumnsMinWidth_tracks_the_open_applicable_columns` (compile-only). "Fluido" = scroll fisico a pixel + trackpad/Shift-rotella + scrollbar flat tematizzata.
- **Nota**: `RestoreColumnWidthsAsync`/`GetColumnWidth`/`HabitScreen.ColumnResizeThumb`/`OnColumnResizeDelta` restano orfani (lo erano già da `f08fba9`); cleanup separato, non toccato qui.

### R-tooltip — ToolTip arrotondato + ombra (compile-only)
- **Stato**: `fixed` (compile-only su Mac, verifica VM) · gravità rifinitura
- **Fix** (`BaseStyles.xaml`): il `ToolTip` aveva solo setter di colore → template default Aero (rettangolo squadrato, bordo grigio scuro). Aggiunto un `ControlTemplate` nella stessa famiglia flottante: angoli **raggio 6**, bordo tema 1px, `DropShadowEffect` morbido (opacity 0.16, blur 12, depth 2), `HasDropShadow=True` per la trasparenza degli angoli + margine trasparente esterno per il bleed, `MaxWidth 320`, font Small. Anteprima `docs/tooltip-preview.html`.

### R-columnSplitter — divisori colonne come sash (compile-only)
- **Stato**: `fixed` (compile-only su Mac, verifica VM) · gravità rifinitura
- **Fix** (`HabitScreen.xaml`): i 5 `GridSplitter` tra le colonne erano barre **piene 4px `Brush.Border`** (grigie, pesanti, sempre visibili). Nuovo stile condiviso `HabitScreen.ColumnSplitter`: area di presa 7px **trasparente** a riposo (resta visibile solo la hairline 1px del bordo colonna), una sottile **barra accento 2px** che compare al 50% su hover e va piena durante il drag, cursore `SizeWE`. Stesso linguaggio del thumb dello slider. Anteprima `docs/column-splitter-preview.html`.

### R-emptyState — stati vuoti con icona + titolo (compile-only)
- **Stato**: `fixed` (compile-only su Mac, verifica VM) · gravità rifinitura
- **Fix**: gli empty-state erano **una sola riga grigia** al 60% opacità. **`EmptyState.xaml`** (Home) ora è una composizione centrata **icona line + titolo**, con icona e testo per-tab via DataTrigger su `ActiveTab`: Attive → `Icon.Sprout` ("pianta un seme"), Archiviate → `Icon.Moon`, Cestino → `Icon.Sparkles`. **`WorkspaceView.xaml`** welcome allineato allo stesso pattern (`Icon.BookOpen` + testo). Nessuna nuova stringa (riuso `Home_Empty_*` / `Workspace_Empty`). Anteprima `docs/empty-state-preview.html`.
- **Possibile next**: una CTA "Nuova abitudine" nell'empty-state Attive (richiede un command nel Home VM raggiungibile da qui) — non incluso per ora.

**Da fare sulla VM**: suite Data completa (incl. i 17 DPAPI/SQLCipher) + **suite WPF completa** (mai eseguita dalla rottura `3497021`); verifica visiva CR5 (cella State-0) + CR13/CR20/CR22 + **R-contextMenu** (menù tasto-destro, angoli popup); CR8 (regen snapshot).

---

## Code review 2026-06-19 — sottosistema 1: Data layer

> Review mirata (multi-agente, recall-biased) sul codice scritto a mano in `src/Recordum.Data` (backup/restore, migrazioni, services) — diff `fase-4-polish` vs `main`, esclusi i file EF generati. Parte di un giro di review per sottosistema (ordine: 1. Data layer ✓ → 2. motore streak → builder/altri). Tutti `reported`, da triagiare/fixare.

### CR1. Restore non atomico → distruzione del DB se il re-encrypt fallisce
- **Stato**: `fixed` (commit `24c0e60`, 2026-06-23) — **runtime verify VM** · gravità **alta (perdita dati)**
- **Fix (working tree, non committato)**: `RestoreAsync` costruisce DB+attachment in un'area di **staging** accanto ai target e fa lo **swap atomico** (`CommitSwap`) tenendo copie `.old` da cui fa **rollback** se la sostituzione fallisce a metà; lo snapshot pre-restore resta come rete finale. Build verde. **Da verificare a runtime sulla VM** (swap file + re-encrypt SQLCipher: non eseguibile su macOS).
- **Dove**: `src/Recordum.Data/Backup/RestoreService.cs:93`
- **Cosa**: `RestoreAsync` fa `File.Delete(dbPath)` + cancella gli attachment (93-95) e *poi* chiama `ReEncryptDbAsync` (100). Se il dump plaintext è corrotto/troncato, disco pieno, o crash in mezzo → DB live perso e nuovo assente/parziale. Lo snapshot pre-restore esiste ma **non viene ripristinato in automatico** sul fallimento. Manca swap atomico (scrivi su temp → rename) e rollback dello snapshot.

### CR2. Guard di compatibilità restore inefficace (costante stale + confronto solo major)
- **Stato**: `fixed` (commit `24c0e60`, 2026-06-23) · gravità **alta**
- **Fix**: la guard legge lo `schema_version` **reale dal DB vivo** via `dbFactory` (`GetCurrentSchemaVersionAsync`, niente più costante "2.7") e confronta major.minor **numericamente** (`SchemaVersionGuard`: "2.10" > "2.7"). Unit test `SchemaVersionGuardTests` verdi (7 casi, eseguiti su osx-arm64).
- **Dove**: `src/Recordum.Data/Backup/RestoreService.cs:21,39,140`
- **Cosa**: `CurrentSchemaVersion = "2.7"` hardcoded mentre lo schema reale è 2.10; `CompareMajor` confronta solo il major. La guard non scatta mai per i 2.x e non rifiuterebbe un backup con schema-minore più recente → EF mapping failure / perdita dati invece del messaggio "Update Recordum to restore it". Leggere lo schema_version reale e confrontare major.minor.

### CR3. Input backup non fidato senza bound → DoS all'apertura
- **Stato**: `fixed` (commit `24c0e60`, 2026-06-23) · gravità **alta (sicurezza)**
- **Fix**: `AesGcmBackupCipher.DecryptAsync` **limita** i parametri Argon2id non fidati (mem ≤1 GiB, iters ≤24, parallel ≤8) **prima** di derivare, e legge il ciphertext con cap a 2 GiB; `BackupArchiveReader` estrae DB/attachment con cap cumulativo 4 GiB (anti zip-bomb). Unit test CR3 (header forgiato → `invalid_format`) verdi su osx-arm64.
- **Dove**: `src/Recordum.Data/Backup/AesGcmBackupCipher.cs:78,121` (+ `:85`)
- **Cosa**: `DecryptAsync` legge i parametri Argon2id (`memMib` fino a 65535, `iters`, `parallel`) dall'header non fidato → `MemorySize = memMib*1024` ⇒ ~64 GiB allocati aprendo un `.recbak` malevolo (già in `PreviewAsync`). In più ciphertext interamente in RAM + entry ZIP estratte senza tetto (bomba di decompressione). Mancano bound su parametri/dimensioni da fonte esterna. *(OK invece: nonce random, nessun riuso GCM, tag verificato.)*

### CR4. Dump DB in chiaro non distrutto (resta recuperabile)
- **Stato**: `fixed` (commit `24c0e60`, 2026-06-23) — **runtime verify VM** · gravità **media (sicurezza)**
- **Fix (working tree, non committato)**: il dump in chiaro (`tempPlainDb`) e lo zip decifrato (`tempZip`) sono **shreddati** (`SecureWipe`, overwrite-zeri + unlink, condiviso con `BackupService.ShredDirectory`) invece di `File.Delete`. Build verde. **Da verificare a runtime sulla VM**.
- **Dove**: `src/Recordum.Data/Backup/RestoreService.cs:105`
- **Cosa**: `tempPlainDb` è copia in chiaro dell'intero DB in `%TEMP%`; rimosso con `File.Delete` (solo unlink). `BackupService` usa `ShredDirectory`, il restore no → DB decifrato forense-recuperabile dopo ogni ripristino. Usare lo stesso shred del backup.

### CR5. State con valore 0 sparisce dal calendario (falsy-zero)
- **Stato**: `fixed` (2026-06-23 cont.) — **verifica visiva VM** (parte WPF) · gravità **media (correttezza dati)**
- **Fix**: cross-layer. **Data** (`CompletionService.GetDailyMagnitudeAsync`): rimosso il filtro `if (mag > 0)` — ogni gruppo deriva da una registrazione reale, quindi il giorno è sempre riportato (no-op per Counter/Resistance floor-1 e Action/Journal Count≥1; sblocca State a valore 0). Test `Magnitude_state_includes_zero_valued_day` verde (osx-arm64). **WPF** (`HabitScreenViewModel`): `TintStepFor`/`NumberFor` ricevono ora un flag `hasCompletion` (presenza in `magByDate`) distinto dal valore → un giorno State registrato a 0 prende lo step di tinta minimo e mostra il numero "0"; un giorno con sola nota resta senza tinta. `PushDaySummaryAsync` usa la presenza (`dayMag is not null`) invece di `mag > 0`. **Compila su Mac**; resa da verificare a vista sulla VM (scelta design: cella State-0 = tinta presenza minima + numero "0").
- **Dove**: `src/Recordum.Data/Services/CompletionService.cs` (`GetDailyMagnitudeAsync`, ex riga 222) · `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (`TintStepFor`/`NumberFor`/`PushDaySummaryAsync`)
- **Cosa**: `GetDailyMagnitudeAsync` per `HabitType.State` usa il valore numerico della metrica primaria, poi `if (mag > 0) result.Add(...)`. Con una scala **0-based** (es. NRS dolore, valore più basso = 0) il giorno dà `mag=0` ed è escluso → cella calendario vuota pur con registrazione. Counter/Resistance hanno floor `Math.Max(1,…)`, Action/Journal `Count()≥1`; solo State è vulnerabile. Distinguere "nessuna registrazione" da "valore 0".

### CR6. CounterReclassV210 riclassifica resistance a metriche miste
- **Stato**: `wontfix` (confermato utente 2026-06-23) · gravità **bassa** (declassata da media)
- **Triage 2026-06-23 cont.**: (1) **interpretazione dubbia** — per la grammatica multivalue una resistance con *qualsiasi* metrica misurata (Integer/Decimal/Seconds) è una "serie" → Counter è la riclassificazione **voluta**; "scale-only resistances stay" significa metriche *esclusivamente* scala, e una resistance scala+misurata non è scale-only. Non è chiaro che sia un bug. (2) **Nessun dato first-party colpito**: l'unico template Resistance ("Stop smoking") ha `metrics:[]`; il set teorico (resistance con scala **e** misurata) è solo da vecchio builder utente. (3) **Non fixabile su Mac né a posteriori**: migrazione già applicata (editarla è bloccato/inutile); una forward-migration correttiva richiede `dotnet ef` (VM) e **non può distinguere** un Counter creato nativamente da uno riclassato. → Raccomando **WONTFIX**; eventuale forward-migration solo se emergono dati reali colpiti.
- **Dove**: `src/Recordum.Data/Migrations/20260613210206_CounterReclassV210.cs:19`
- **Cosa**: il subquery matcha **per riga di metrica**: una resistance con sia metrica scala sia misurata entra in `id IN (...)` per la seconda → l'intero habit diventa Counter. Down irreversibile.

### CR7. Materializzazione template = N+3 aperture di connessione (hazard SQLITE_BUSY)
- **Stato**: `fixed` (2026-06-23 cont.) — verificato (osx-arm64) · gravità **bassa (efficienza/robustezza)**
- **Fix**: `MaterializeMetricsSegmentsGoalAsync` riscritto su **singolo `DbContext` + transazione** (pattern `ComposedHabitService`): un unico lookup units/scales, le metriche/segmenti/goal aggiunti direttamente con `Id` pre-assegnati e una sola `SaveChanges`+`Commit`. Da N+3 aperture a 1. Rimossi `IMetricDefinitionService`/`IGoalService` dal costruttore (ora inutilizzati; DI invariato). L'interceptor timestamp/provenance scatta comunque sulla SaveChanges. `HabitTemplateServiceTests` 9/9 verdi (materializzazione metrica-scala/unità-goal/segmenti coperta).
- **Dove**: `src/Recordum.Data/Services/HabitTemplateService.cs` (`MaterializeMetricsSegmentsGoalAsync`; era indicato `:1287`, riga stale)
- **Cosa**: apriva un context per i lookup, poi `metrics.CreateAsync` per ogni metrica (ciascuna apre+salva), poi context per i segmenti, poi `goals.CreateAsync`. Ogni apertura ri-esegue `PRAGMA key` → hazard SQLITE_BUSY (già attutito da `busy_timeout`, ora eliminato alla radice).

### CR8. Seed `schema_version = "2.9"` stale rispetto a HEAD (2.11)
- **Stato**: `triaged` — **rimandato alla VM** (serve `dotnet ef`) · gravità **bassa**
- **Dove**: `src/Recordum.Data/SeedData/AppSettingSeed.cs:31` (era indicato `:925`, riga stale)
- **Triage 2026-06-23 cont.**: il valore `2.9` è in `HasData` → finisce nel **ModelSnapshot** (`RecordumDbContextModelSnapshot.cs:262`). `InitialSchema` semina `2.3` (riga 678); fu bumpato a `2.9` con `NoteHistoryV29` ma `CounterReclassV210`/`NoteCategoryV211` hanno bumpato solo via `UpdateData`, lasciando il costante a `2.9` mentre **HEAD reale è 2.11** (`NoteCategoryV211`, non 2.10 come dice CR/CLAUDE.md). **Impatto runtime = zero**: il guard CR2 legge lo `schema_version` reale dal DB vivo. Allinearlo richiede rigenerare lo snapshot (`dotnet ef migrations add`), non installato su Mac e file sotto guardrail → **da fare sulla VM**.

### CR9. Stringa "Goal" hardcoded (viola regola .resx)
- **Stato**: `fixed` (2026-06-23 cont.) — verificato (osx-arm64) · gravità **bassa (convenzioni)**
- **Fix**: eliminato il literal. `MaterializeMetricsSegmentsGoalAsync` riceve ora il nome risolto dell'habit (`node.Name`, già `payload.Node?.Name ?? tmpl.Name`) e lo usa come nome del goal — il goal appartiene all'habit, quindi ne eredita il nome (default sensato, mai una stringa inglese fissa). Nota: il Data layer **non ha `.resx`** (verificato: nessun resource in `Recordum.Data`/`Recordum.Core`), quindi localizzare lì non era un'opzione pulita; ereditare il nome è la soluzione corretta. Test `InstantiateAsync_goal_inherits_the_habit_name_not_a_hardcoded_default` verde.
- **Dove**: `src/Recordum.Data/Services/HabitTemplateService.cs` (`MaterializeMetricsSegmentsGoalAsync`; era indicato `:1349`, riga stale)
- **Cosa**: `Name = payload.Node?.Name ?? "Goal"`. CLAUDE.md §8: «Stringhe utente in .resx … Niente hardcoded.» Il fallback scattava solo con `Node.Name` assente.

### CR10. `MoveAndReorderAsync` duplica la validazione di `MoveAsync`
- **Stato**: `fixed` (2026-06-23 cont.) — verificato (osx-arm64) · gravità **bassa (manutenibilità)**
- **Fix**: estratto `private static ValidateAndReparentAsync(db, id, newParentId, ct)` che carica il nodo, rifiuta i soft-deleted, valida ciclo + parent-existence e riassegna `ParentId`, ritornando il nodo tracciato così `MoveAndReorderAsync` continua col riordino. Aggiunto test di regressione `Reorder_into_missing_parent_throws_HabitNodeNotFoundException`. `HabitNodeServiceTests` 19/19 verdi.
- **Dove**: `src/Recordum.Data/Services/HabitNodeService.cs` (`MoveAsync`/`MoveAndReorderAsync`/`ValidateAndReparentAsync`; era indicato `:1217`, riga stale)
- **Cosa**: cycle-check + parent-existence copiati verbatim da `MoveAsync`. Un futuro fix va fatto in due punti o divergono. Estrarre helper condiviso `ValidateAndReparentAsync`.

### Sottosistema 2 — motore streak (delta astinenza). Esito: solido, 2 note minori
> Delta `fase-4-polish` (il core schedule×mode×recovery è in `main`, già rivisto a M3.8). Verificato OK: ordinamento ricaduta più recente (`OrderByDescending(CompletedAtMs)`), storia piena in `allTime`, coerenza UTC, `DurationFormat.TryParse` robusto. Nessun bug bloccante.

### CR11. Streak astinenza: `length` del run corrente troncato alla finestra di query
- **Stato**: `fixed` (2026-06-23 cont.) — verificato (osx-arm64) · gravità **bassa** *(non solo latente: tocca l'export)*
- **Fix**: `ComputeAbstinenceAsync` ancora ora il conteggio dei giorni puliti a `createdDate` (la `fromDate` non tronca più la run corrente), poi filtra le run restituite a quelle con `End >= fromDate` — i caller con finestra non vedono run antiche chiuse, ma la run corrente/sovrapposta porta la lunghezza vera. Test `Current_run_length_counts_from_the_real_last_lapse_not_the_query_window` verde; `AbstinenceStreakTests` 7/7.
- **Nota triage**: **non del tutto latente** — `ExportQueryService.BuildSummaryAsync` calcola `current = runs.First(IsCurrent).Length` da `GetHistoryAsync(request.From,…)`, quindi un export Resistance su finestra recente troncava la streak corrente. `StreakSummaryViewModel` invece passa `createdDate` (nessun cambio di comportamento per lui).
- **Dove**: `src/Recordum.Data/Services/Aggregators/SchedulingAwareStreakAggregator.cs` (`ComputeAbstinenceAsync`; era indicato `:186`, riga stale)
- **Cosa**: se chiamato con `fromDate` dopo l'ultima ricaduta, `length` conta solo dentro [start,toDate]. Calcolare il `length` dall'ultima ricaduta reale.

### CR12. `CurrentLength` numerico stale subito dopo una ricaduta in giornata (cosmetico)
- **Stato**: `reported` · gravità **bassa (cosmetico)**
- **Dove**: `src/Recordum.WPF/ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs` (selezione `current` + `CurrentLength`)
- **Cosa**: ricaduta *oggi* → headline corretto ("appena ora" via elapsed), ma `CurrentLength` resta sul run che finisce ieri (clausola `r.End == today.AddDays(-1)`). Innocuo finché `CurrentLength` non è mostrato per Resistance; verificare il binding del timeline.

### Sottosistema 3 — ViewModels WPF (HabitScreen/Home/drag-drop/builder). 12 findings
> Delta `fase-4-polish` (~3.700 righe, 69 VM + behaviors). Review multi-agente recall-biased; i CONFIRMED sotto sono stati verificati leggendo il codice.

### CR13. Metriche durata invisibili nelle liste registrazioni (CONFIRMED)
- **Stato**: `fixed` (2026-06-23 cont.) — **compile-only su Mac, run+verifica VM** · gravità **media (dato visibile mancante)**
- **Fix**: aggiunto il ramo `Seconds` in `FormatValue` (`if (v.ValueSeconds is { } secs) return DurationFormat.Format(secs);`) + `using Recordum.WPF.Formatting`. Una serie con sola durata ora mostra "1:30". Test `LoadDayAsync_renders_a_duration_field_value` (compila su Mac; **da eseguire sulla VM** — i test WPF sono `net10.0-windows`).
- **Dove**: `src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs` (`FormatValue`)
- **Cosa**: `FormatValue` controlla ScaleLabels/ValueText/ValueDecimal/ValueInteger ma **non `ValueSeconds`** → una serie con sola durata (es. 1:30) appare con riga vuota in Cronologia/Records. Dato salvato ma invisibile.

### CR14. `HabitEditorViewModel.LoadAsync` senza generation guard → stato misto/salvataggio su habit sbagliato (CONFIRMED)
- **Stato**: `reported` · gravità **media (integrità)**
- **Dove**: `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs:181`
- **Cosa**: `LoadAsync` setta `_loading` ma non ha token di generazione (HabitScreen sì). Con Configura aperta e cambio rapido di habit i load si interlacciano: sezioni miste A/B, `Node` = ultimo writer, edit successivo persiste sul Node corrente potenzialmente errato. Aggiungere generation guard come in `HabitScreenViewModel`.

### CR15. Handler `async void` non protetti → crash su errore transitorio
- **Stato**: `reported` · gravità **media (robustezza)**
- **Dove**: `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs` (OnHomePropertyChanged + 3 handler) · `…/HabitScreen/HabitScreenViewModel.cs` (handler bus/calendario)
- **Cosa**: `await` su chiamate DB/launcher senza try/catch in handler `async void`. Errore SQLite transitorio reagendo a un evento → eccezione non osservata → `DispatcherUnhandledException` → crash. Avvolgere in try/catch con logging.

### CR16. Drag-drop: cattura solo `CyclicHabitNodeMoveException` → altre eccezioni crashano
- **Stato**: `reported` · gravità **media (crash su gesto)** — *era follow-up sotto R-dragReorder, promosso a CR*
- **Dove**: `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs` (`OnDrop`) → `HomeViewModel.ApplyDropAsync` / `TryBulkMoveAsync`
- **Cosa**: `HabitNodeNotFoundException`/`AlreadySoftDeleted` (parent rimosso da reload/cleanup concorrente) sfuggono dall'`async void` → crash. try/catch completo in `ApplyDropAsync`/`TryBulkMoveAsync`.

### CR17. Zona di drop calcolata sul sotto-elemento, non sulla card riga
- **Stato**: `reported` · gravità **bassa (fragilità)** — verifica visiva da fare (drag già "funziona alla grande" lato utente)
- **Dove**: `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs` (`ResolveZone`/`OnDragOver`)
- **Cosa**: il bordo Before/Onto/After deriva da `e.OriginalSource.ActualHeight` (spesso un figlio: testo/sparkline), non dalla card; il rapporto Y/altezza non mappa il vero terzo della riga → indicatore/SortOrder potenzialmente errati. Il ghost usa già `FindRowCard`: applicarlo anche alla zona.

### CR18. `HomeViewModel`: reload fire-and-forget senza generation guard
- **Stato**: `reported` · gravità **bassa**
- **Dove**: `src/Recordum.WPF/ViewModels/HomeViewModel.cs` (`ReloadPreservingSelectionAsync`/`OnRowChanged`)
- **Cosa**: eventi bus sovrapposti possono interlacciare due `Rebuild` sulla stessa `Rows` (duplicati transitori, handler doppi). HabitScreen/Detail hanno il guard; Home no.

### CR19. `HabitRecordsViewModel`: cambio periodo/modalità senza guard
- **Stato**: `reported` · gravità **bassa**
- **Dove**: `src/Recordum.WPF/ViewModels/HabitScreen/HabitRecordsViewModel.cs`
- **Cosa**: handler `OnModeChanged`/`OnSelectedPeriodChanged` fanno `_ = ReloadAsync()` sovrapponibili; toggle rapido Giorno↔Periodo può lasciare in lista i risultati della query più vecchia.

### CR20. `BackupDialogViewModel` usa `DateTime.UtcNow` diretto (CONFIRMED, convenzioni)
- **Stato**: `fixed` (2026-06-23 cont.) — **compile-only su Mac, run VM** · gravità **bassa (convenzioni)**
- **Fix**: iniettato `IClock clock` nel costruttore; `Browse` usa `clock.UtcNow.UtcDateTime` per il nome file. DI invariato (AddTransient risolve `IClock`). `FakeBackupSaveFileDialog` ora cattura `LastSuggestedName`; test `Browse_derives_the_default_filename_from_the_injected_clock_not_DateTime_UtcNow` (compila su Mac, run VM). Aggiornate le 3 costruzioni esistenti del VM nei test.
- **Dove**: `src/Recordum.WPF/ViewModels/Backup/BackupDialogViewModel.cs`
- **Cosa**: nome file backup da `DateTime.UtcNow`. CLAUDE.md §4: «Mai DateTime.UtcNow/Now diretti. Tutto via IClock/TimeProvider.»

### CR21. QuickAdd: eccezioni grezze in UI + duplicazione
- **Stato**: `reported` · gravità **bassa (convenzioni/UX/reuse)**
- **Dove**: `…/QuickAdd/MeasuredQuickAddViewModel.cs`, `CounterSeriesQuickAddViewModel.cs`, `CounterTapQuickAddViewModel.cs`
- **Cosa**: `ErrorMessage = ex.Message` → stringa non localizzata (es. "UNIQUE constraint failed") mostrata all'utente (viola §8 .resx); usare messaggio localizzato + log. Inoltre `ToggleChrono` e lo skeleton Save sono duplicati tra le 3 VM → estrarre base condivisa.

### CR22. Heatmap anno perde il 31 dic di un bisestile che inizia di domenica (CONFIRMED)
- **Stato**: `fixed` (2026-06-23 cont.) — **compile-only su Mac, run VM** · gravità **bassa (raro, display)**
- **Fix**: `BuildYearHeatmap` ora costruisce **54 settimane** (378 celle) invece di 53/371 — con `leading==6` in un bisestile l'ultima cella è `jan1+371 ≥ 31 dic`. Aggiornati i doc-comment (Cells 378, YearWeeks 54). Test esistente aggiornato a 54/378 + nuovo `YearHeatmap_includes_Dec_31_of_a_leap_year_whose_Jan1_is_on_the_week_edge` (sceglie l'anno bisestile col bordo-settimana per la cultura corrente → deterministico). Compila su Mac, run VM.
- **Nota**: l'**ExportEngine NON condivide il bug** — il suo heatmap è una **finestra rolling di 53 settimane fino a oggi** (`ExportQueryService`: `anchor = mondayOfTodayWeek - 52*7`), non una griglia anno-solare Jan1→Dec31. Il cross-reference del CR era impreciso. Nessun cambio a `HeatmapImageRenderer`.
- **Dove**: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs` (`BuildYearHeatmap`)
- **Cosa**: con `leading==6` (Jan 1 sull'ultima colonna della settimana in anno bisestile, es. 2028 cultura domenica-prima / 2012-2040 lunedì-prima) l'ultima cella era `jan1+364 < 31 dic = jan1+365` → il 31 dic non renderizzato.

### CR23. `HabitEditorViewModel.OnColorChanged` inquina la MRU colori a ogni tasto
- **Stato**: `reported` · gravità **bassa**
- **Dove**: `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs` (`OnColorChanged`)
- **Cosa**: `_recentColors.AddAsync` su ogni colore valido (anche intermedi mentre si digita l'hex), fire-and-forget. MRU riempita di colori transitori, condivisa con builder e picker note. Salvare solo sul commit.

### CR24. `LoadMarkersAsync`: 2×N query sequenziali per mese (perf)
- **Stato**: `reported` · gravità **bassa (perf)**
- **Dove**: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (`LoadMarkersAsync`)
- **Cosa**: per ogni mese del range due `await` DB sequenziali (`GetDailyMagnitudeAsync` + `ListNoteCountsInMonthAsync`), ri-eseguiti a ogni completamento/nota via `Calendar.JumpAsync`. Contro il budget home <500ms. Parallelizzare (`Task.WhenAll`) o query a range unica.

## Stato al 2026-06-16 — lista inserimenti filtrabile per periodo (Feature B)

### B-entriesPeriod. Lista registrazioni commutabile Giorno/Periodo nella colonna Cronologia
- **Stato**: `verified-fixed` (utente 2026-06-16: «vedo le note in cronologia col toggle in base al periodo») — eseguito 2026-06-16, 6 task TDD (executing-plans). Spec `docs/superpowers/specs/2026-06-15-entries-list-period-filter-design.md`, piano `docs/superpowers/plans/2026-06-15-entries-list-period-filter.md`.
- **Cosa**: nella colonna **Cronologia** dello HabitScreen il pannello "Registrazioni" ora è commutabile **Giorno / Periodo** (RadioButton). In modalità Periodo un ComboBox sceglie **Settimana / Mese / Anno / Tutto / Personalizzato**; con Personalizzato compaiono due `DatePicker` Da/A. Default invariato: **Giorno**, guidato dal giorno selezionato sul calendario (nessuna regressione del comportamento esistente).
- **Architettura**: resolver puro `EntriesPeriodRange.Resolve(period, today, customFrom?, customTo?)` in Core (Settimana = settimana ISO lun→dom; Mese/Anno = calendario; Tutto = illimitato; Personalizzato = estremi passati). `CompletionListViewModel.LoadRangeAsync(from?, to?)` riusa l'overload range già esistente su `ICompletionService` (`LoadDayAsync` ora è un wrapper `from==to`). Nuovo `HabitRecordsViewModel` incapsula modalità+filtro+lista, alleggerendo `HabitScreenViewModel` (`Records` rimpiazza `DayCompletions`). UI additiva sulla colonna Cronologia + 2 converter (`EntriesPeriodLabel`, `DateOnlyToDateTime`) + `ObjectDataProvider EntriesPeriodValues` + i18n EN/IT (10 stringhe). **Nessuna migrazione, nessun cambio a `ICompletionService`/schema.**
- **Commit**: `a17a776` (Task 1 Core resolver + 6 test) · `64945bb` (Task 2 LoadRangeAsync) · `29708df` (Task 3 HabitRecordsViewModel + 4 test) · `7d28349` (Task 4 cablaggio, comportamento invariato) · `1059da3` (Task 5 UI+i18n).
- **Test**: Core **61** verdi (+6), WPF **567** verdi (+2 LoadRange, +4 HabitRecords), build 0 errori.
- **Verifica visiva pendente** (tema scuro+chiaro): default Giorno = nessuna regressione; toggle Periodo → Mese corrente; preset Settimana/Anno/Tutto; Personalizzato Da/A (Da>A ⇒ lista vuota, nessun crash); selezione giorno-su-calendario in Periodo NON cambia la lista (QuickAdd/DayNote sì); ritorno a Giorno; refresh post-completamento nella modalità corrente; righe ancora editabili/eliminabili.
- **Resta aperto il ciclo successivo**: **Feature A** (tab Note cronologia pagine+note) — non ancora pianificata.

## Stato al 2026-06-15 — drag-and-drop visivo + riordino manuale

### R-dragReorder. Drag-and-drop visivo con fantasma + riordino manuale persistente
- **Stato**: `fixed` (verifica visiva utente da fare) — eseguito subagent-driven 2026-06-15, 6 task TDD (+ Task 7 verifica). Per ogni task: implementer → spec-review → code-quality-review.
- **Richiesta utente**: «inserisci il drag-n-drop visivo. Voglio vedere la casella dell'abitudine "sollevarsi" quando la trascino». In chiarimento: fantasma agganciato al cursore (stile file-manager) + indicatori di posizione (dentro un gruppo, sopra/sotto un'abitudine) per **riordinare e spostare**. Decisione UX: riordinando in un ordinamento non-Manuale, l'app **passa a Manuale in automatico**.
- **Spec**: `docs/superpowers/specs/2026-06-15-drag-reorder-visual-design.md`; piano `docs/superpowers/plans/2026-06-15-drag-reorder-visual.md`. **Nessuna migrazione**: `HabitNode.SortOrder` esisteva già nel modello/DB ma non era né letto né scritto per i nodi habit, e `HomeSort.Manual` era inerte (`_ => rows`).
- **Eseguito** (commit `7f6251f`→`e0784f3`, + rifiniture review `a23292e`/`a7f1195`/`5676505`):
  1. **Data** `IHabitNodeService.MoveAndReorderAsync(id, newParent, anchorSiblingId, placeAfter)` — riordino **anchor-based** (più robusto dell'indice intero): riassegna `SortOrder` contigui 0..n-1 ai fratelli inserendo il nodo accanto all'ancora (null/non trovata = append in coda); cycle guard riusato da `MoveAsync`; transazionale.
  2. **Sort** `SortOrder` esposto in `HomeTreeRow` (param opzionale in coda, zero blast-radius) + proiettato nella query; `HomeSort.Manual` ora ordina per `SortOrder` (tie-break Name).
  3. **VM** flag riga `IsBeingDragged`/`DropEdge` (enum `RowDropEdge`), `DropPlan` value type, `HomeViewModel.ApplyDropAsync` (bulk reparent vs single `MoveAndReorderAsync`; auto-switch a Manuale solo su reorder fine).
  4. **Behavior** `TreeDragDropBehavior.ResolveDrop` puro (zona+bersaglio→DropPlan, testato); indicatore **singolo** alla volta via `SetIndicator` (highlight gruppo *oppure* edge inserimento); `OnDrop`→`ApplyDropAsync`.
  5. **Ghost** `DragAdorner` (snapshot `VisualBrush` ~0.55 opacità che segue il cursore); spawn/teardown deterministico nel `finally` di `DoDragDrop` (pulisce ghost+flag+indicatore anche su annullamento/Esc).
  6. **XAML** trigger in `Recordum.HabitBlock`: sorgente trascinata sbiadita (opacità 0.4), linea di inserimento accent 2px sopra (Before)/sotto (After).
- **Catch della review olistica finale** (`2eb4d03`): l'auto-switch a Manuale faceva `Rebuild` sul **cache `_lastFetched` stantio** (la nuova `SortOrder` era nel DB, non nello snapshot in memoria) → il riordino si vedeva solo al load successivo. Fix: dopo un riordino a riga singola si **ricarica sempre** (sopprimendo il Rebuild ridondante durante la transizione di sort). Test di regressione asserisce un fetch fresco. *(Le review per-task non l'avevano colto: il seam era tra Task 3 e l'integrazione.)*
- Suite finali: **Data 332 / WPF 555 / Core 55 / Plugins 66 / Export 17 / Chart 18** verdi, build 0/0.
- **Limiti noti (accettati, da spec §6/§7)**: multi-selezione = reparent in blocco senza indice fine (il riordino preciso vale per il drag a riga singola); i pin (Global/Local) restano in cima, `SortOrder` ordina *dentro* il gruppo dei pin.
- **Follow-up (non bloccante, da review Task 4)**: `OnDrop` è `async void` e `ApplyDropAsync` cattura solo `CyclicHabitNodeMoveException`; un'eccezione diversa (DB error…) propagherebbe come unhandled. **Pre-esistente** (anche il vecchio `OnDrop` catturava solo quella) — valutare un try/catch con logging in `ApplyDropAsync` per non far cadere il processo su un gesto utente.
- **Verifica visiva utente (2026-06-15, tema scuro)**: la **meccanica funziona** ("sembra funzionare"). Resta aperto solo il **visual del ghost** (`R-dragGhostVisual`): inizialmente troppo trasparente perché la riga abitudine è translucida per design (~10% tint) e il `VisualBrush` live ne catturava la versione sbiadita da `IsBeingDragged`. Iterato a: snapshot `RenderTargetBitmap` congelato → poi su **card opaca** (`Brush.SurfaceElevated`+bordo, angoli arrotondati). Stato utente: «più o meno ci siamo, ancora da rivedere» → checkpoint `b7fe1d1` (`wip`), **rifinitura rinviata**. Possibile causa residua di "poco fluido": il ghost si riposiziona sugli eventi `DragOver` del drag OLE (più radi dei movimenti reali del mouse) → eventuale fix Win32 (posizione mouse real-time) se servisse. Restano da verificare a vista: indicatori sopra/sotto vs highlight gruppo, persistenza riordino al riavvio, auto-switch a Manuale, tema chiaro.
- **R-dragGhostVisual → `verified-fixed` (2026-06-15, commit `db8af0d` + `92f7675`)**: riscritto il ghost da `Adorner` a **`DragGhost` = Popup topmost** con snapshot `RenderTargetBitmap` della **card intera** (risolve dimensione variabile + sgranatura: prima si fotografava `OriginalSource`, un sotto-elemento), riposizionato a ogni **`QueryContinueDrag`** via `NativeMethods.TryGetCursorPos` (Win32 `GetCursorPos`, `LibraryImport`) → fluido; il Popup galleggia su tutto → mai più clippato ai bordi lista. `DragOver` torna a guidare solo gli indicatori. Eseguito subagent-driven 6 task TDD (interop → `PhysicalToDip` → `DragGhost` → `FindAncestorWithStyle` → cablaggio+rimozione `DragAdorner` → verifica). Per-task review + holistic finale ("ready to ship"). Suite: **WPF 561** (+6) / Data 332 / Core 55 / Plugins 66 / Export 17 / Chart 18, build 0/0 con `-warnaserror`. Spec `docs/superpowers/specs/2026-06-15-drag-ghost-popup-design.md`, piano `…/plans/2026-06-15-drag-ghost-popup.md`. **Verifica utente: «Funziona alla grande»** (fallback `SetWindowPos` previsto ma NON necessario). Limiti noti (accettati): fallback `FindRowCard` per righe in gruppi annidati dipende dallo style `Recordum.HabitBlock` (style-match); se quel resource-key cambia, le righe annidate resterebbero senza ghost (mai crash).

## Stato al 2026-06-14 — crash "database is locked" al Lancia builder

### B-dbLock. Crash `SQLITE_BUSY` su scrittura concorrente (verifica visiva R-multiValue)
- **Stato**: `fixed` (verifica visiva utente da fare)
- **Quando**: 2026-06-14, durante la verifica visiva di R-multiValue. App lanciata via skill `run`, partita ok; **premendo "Lancia" nel costruttore** → crash terminating (`AppDomain.UnhandledException`), `Microsoft.Data.Sqlite.SqliteException: SQLite Error 5: 'database is locked'`.
- **Catena**: `HabitBuilderViewModel.LaunchAsync` → `RecentColorStore.AddAsync` → `AppSettingService.SetAsync` → `SaveChangesAsync`. La habit **era già creata** (`CreateComposedHabitAsync` riuscito); a fallire è la scrittura MRU del colore.
- **Root cause** (systematic-debugging, Phase 1): l'app usa `AddDbContextFactory` → ogni servizio apre la **propria** `SqliteConnection`. I default SQLite (`busy_timeout=0` + journal rollback) fanno fallire **all'istante** un writer che trova il file lockato da un'altra connessione (con ogni probabilità il `BackupSchedulerHost`, InitialDelay 2 min, o un reload concorrente) invece di attendere. Non è un bug del builder né di RecentColorStore: è **fragilità di configurazione connessione** che può colpire qualunque write concorrente.
- **Fix** (TDD, 3 test nuovi):
  1. **Root cause**: `PRAGMA busy_timeout = 5000` su **ogni** apertura connessione, nei due punti dove nasce — `SqlCipherKeyConnectionInterceptor` (chokepoint runtime, combinato con `PRAGMA key` che resta primo statement; path sync+async) e `SqlCipherConnectionFactory.Open` (primitivo per init/migration/backup). Costante `BusyTimeoutMs`. Un writer ora **attende** il lock transitorio invece di morire. Test `SqlCipherBusyTimeoutTests` (factory + interceptor → `PRAGMA busy_timeout > 0`).
  2. **Defense-in-depth**: la scrittura MRU colore in `LaunchAsync` è **best-effort** — `try/catch` swallow: un hiccup transitorio non deve crashare l'app né annullare la habit già creata. Test `Launch_does_not_crash_when_recording_recent_color_fails`.
- **Scelta**: `busy_timeout` (non WAL) — minimale, niente file sidecar `-wal`/`-shm`, niente impatto su backup/restore. WAL resta opzione futura per più concorrenza se servisse.
- Suite: **Data 322 / WPF 535** verdi.

### B-uiThread. Crash cross-thread su `ObservableCollection` legata a CollectionView (verifica visiva R-multiValue)
- **Stato**: `fixed` (verifica visiva utente da fare)
- **Quando**: 2026-06-14, subito dopo il fix B-dbLock. Registrando un completamento → crash terminating, `System.NotSupportedException: ...CollectionView non supporta modifiche al SourceCollection ... da un thread diverso dal thread del dispatcher`. Due stack: `YearHeatmapViewModel.LoadAsync` (via `DetailViewModel.OnCompletionEvent`) e `CalendarNavigatorViewModel.BuildMonthGrid` (via `HabitScreenViewModel.OnCompletionEvent`).
- **Root cause** (systematic-debugging): `EventBus.Publish` invoca gli handler **sincronamente sul thread del publisher**; le registrazioni completion/note girano con `ConfigureAwait(false)` → thread-pool. Gli handler VM sono `async void` e mutano `ObservableCollection` legate alla view **off-UI-thread** → crash. Il `.ConfigureAwait(true)` negli handler non aiutava (nessun SyncContext da catturare sul thread-pool). `EventBus` sta in `Recordum.Plugins` (platform-agnostic, riusato web/MAUI) → il marshaling va fatto nel WPF al punto di subscription.
- **Fix** (TDD): nuovo helper `IEventBus.SubscribeOnUi` (`src/Recordum.WPF/Services/UiEventSubscriptionExtensions.cs`) che marshala l'handler sul dispatcher **catturato al momento della subscription** (`Dispatcher.CurrentDispatcher` — i VM nascono sul thread UI). Adottato da `HabitScreenViewModel` (7 sub), `DetailViewModel` (6), `NotesViewModel` (3) e **`HomeViewModel`** (consolidato: rimosso il marshaling inline duplicato basato su `Application.Current.Dispatcher`). Test `UiEventSubscriptionExtensionsTests` (forwarding + dispose-unsubscribe).
- **Bonus**: catturare il dispatcher alla subscription (invece del global `Application.Current.Dispatcher`) **de-flaka** il test noto `CompletionRecorded_event_reloads_the_list_for_visual_feedback` — la flakiness era proprio il marshaling verso un dispatcher straniero/non-pompante leaked da `IconResourceAlignmentTests` (`new Application()`). Suite **WPF 535** verde 2× di fila.

### B-abstinence. La funzione d'astinenza (Resistance) non è implementata nel motore streak
- **Stato**: `fixed` (verifica visiva utente da fare) — eseguito subagent-driven 2026-06-14, commit `6133c08`→`c6d60d3` (6 task, per-task spec+quality review + review olistica finale: READY TO MERGE)
- **Quando**: 2026-06-14, durante la verifica visiva (utente: "il template stop smoking non funziona"). Indagine: non è il template — **il motore streak non ha logica per Resistance**. `SchedulingAwareStreakAggregator` è completion-positivo (un giorno conta solo se ha un completamento; giorno schedulato senza completamento = miss che spezza), mentre per l'astinenza la semantica è invertita (completamento = ricaduta, streak = tempo dall'ultima ricaduta). L'unica logica resistance-aware è in `CompletionService` (magnitudine = occorrenze; "records abstinence lapses") e nel display (`StreakSummaryViewModel` formatta come tempo trascorso) — ma lo streak sottostante è invertito → privo di senso. Repo con 3 modelli contraddittori (template zero-metriche / demo check-in ~70% / seed-habit ricadute rare).
- **Decisione utente**: fix vero nel motore (non aggirare via template/seed). Modello: streak = giorni puliti dall'ultima ricaduta (da creazione se mai); corrente a **precisione fine** (ore/min via `CompletedAtMs`, "2a 41g 6h"); percorso dedicato che ignora schedule/mode/recovery, onora le pause.
- **Spec**: `docs/superpowers/specs/2026-06-14-abstinence-streak-engine-design.md`; piano `docs/superpowers/plans/2026-06-14-abstinence-streak-engine.md`. Template "Stop smoking" **non si tocca** (lo fixa il motore); demo "Niente fumo" allineato al modello ricadute. Nessun cambio schema.
- **Eseguito**: (1) `StreakBreakReason.Lapse`; (2) ramo `ComputeAbstinenceAsync` nell'aggregator (run di giorni puliti dall'ultima ricaduta, pause congelate, ignora schedule/mode/recovery); (3) `DurationFormat.FormatElapsed` ("2a 41g"/"41g 6h"/"6h 12m"/"12m"/"appena ora"); (4) `StreakSummaryViewModel` Resistance = elapsed fine dall'istante dell'ultima ricaduta + stat "Ricadute (90gg)"; (5) editor nasconde Scheduling/Streak per Resistance (matrice aggiornata); (6) demo seeder "Niente fumo" registra ricadute rare. Suite **Data 328 / WPF 546** verdi.
- **Limitazioni note** (spec §5): il display fine corrente non sottrae i giorni di pausa (i run a giorni sì) → divergenza solo per Resistance con pause; `DurationFormat` resta hardcoded come `FormatDays`. Verifica visiva utente: "Niente fumo" mostra streak pulito ~21g + 3 marker ricaduta; "Stop smoking" (template invariato) ora funziona; editor Resistance senza Scheduling/Streak.

## Stato al 2026-06-11 (notte) — nav-model parte 1: workspace a colonne-toggle eseguito

**Nav-model parte 1 — workspace a colonne** (spec `2026-05-29-navigation-model-design.md` §3–§4/§7, piano `docs/superpowers/plans/2026-06-11-column-workspace.md`, 6 task, commit `0c1304d`→`cae0ad0`): le viste habit (Cronologia/Dettaglio/Note/Configura) sono **colonne affiancate** aperte/chiuse da una **chip-bar** (stile `Recordum.SegmentCheck`, toggle indipendenti) al posto delle 3 tab + pannello editor separato. Stato in `HabitScreenViewModel` (4 flag `Is*Open` + ordine-di-apertura per Esc), persistenza CSV su AppSetting `ui.open_columns` (creata on-write, nessuna migrazione), set mantenuto al cambio habit, default Cronologia. `HabitEditorViewModel` ora di proprietà di `HabitScreenViewModel` (in `MainWindowViewModel` resta un passthrough); **eliminati** da `MainWindow` EditorPane standalone, splitter e larghezza persistita `ui.editor_pane_width`. ✕ per colonna + **Esc chiude l'ultima colonna aperta**; scroll orizzontale quando le colonne sfondano. Suite WPF **458 verdi**. **Stato: `verified-fixed`** (utente 2026-06-11: "sembra andare"). Parte 2 (piano successivo): gruppi-basi ＋Nuova▾ + drag-drop evidente (spec §3.2/§5).

### R-multiValue. Revisione abitudini multivalore (eseguita 2026-06-13)
- **Stato**: `fixed` (verifica visiva utente da fare)
- **Contesto**: backfill di "flessioni" (habit multi-metrica) con `--seed-habit`: il seeder ha popolato **solo la prima metrica**. L'utente fa notare che le metriche hanno semantiche diverse: il "contatore" di una serie indica *se la serie è stata eseguita* (quindi dovrebbe essere un **check**, non un numero), le "ripetizioni" sono una **quantità**, il "tempo" è una **durata** — e quest'ultimo è rimasto vuoto.
- **Eseguito** (spec `2026-06-12-multivalue-habits-revision-design.md`, piano omonimo, 15 task, commit `c891deb`→`bd99622`):
  1. **Grammatica v2** (`HabitCompositionRules`): contatore = occorrenze (mattone +1 a tela vuota), serie come proprietà del Counter, astinenza = mattone dedicato → Resistance; campo Count ritirato; unità quantità opzionale (Integer senza unità, Decimal con).
  2. **Builder**: palette con CheckBox Contatore/Astinenza al posto del toggle Serie; anteprima tap vs righe-serie.
  3. **Quick-add in tre forme** (routing per tipo + shape in `HabitScreenViewModel`): counter nudo = `CounterTapQuickAddViewModel` (+1/⌫, conteggio del giorno); counter con campi = `CounterSeriesQuickAddViewModel` (righe-serie con ✓/○ di validità, reindex senza buchi, "Registra N serie"); Resistance/State = `MeasuredQuickAddViewModel`.
  4. **Cella durata** mm:ss (`DurationFormat` + `SeriesCellViewModel.IsDuration`) che scrive `ValueSeconds`; **crono-cella** ⏱ su `active_timer` (`IActiveTimerService.StopWithoutRecordingAsync` — ferma senza registrare, riversa i secondi nella cella).
  5. **+1 da Home** (`CounterQuickActionHandler`): counter nudo → occorrenza nuda; con campi → apre la schermata.
  6. **Dati**: migration `CounterReclassV210` (resistance-serie → counter, schema_version 2.10); magnitudine = occorrenze per counter/resistance; `ValidatePerType` ammette completion nude per Counter/Resistance (solo State richiede un valore); streak Resistance mostrata in tempo trascorso ("2a 41g").
  7. **Seeder** `--seed-habit` natura-aware: riempie TUTTE le metriche (quantità con trend, durata, scala pesata), 1–4 serie/giorno con ~10% di campi vuoti; counter nudo → N occorrenze nude.
  - Suite: WPF 531, Data 320, Core 55, Plugins 66, Export 17, Chart 18 (flaky noto `CompletionRecorded_event_reloads_the_list` verde isolato).
- **Successivo collegato**: il filone grande **grafici + dati + correlazioni tra abitudini** (anticipa parte della Fase 9, brainstorm dedicato) resta da affrontare.

**Panoramica gerarchica** (appunto utente in verifica; commit `f139ca1`): il rollup della colonna Panoramica ora mostra anche i **sottogruppi come righe** (nome semibold + conteggio figli, niente sparkline/✓) con le loro habit **indentate** sotto (`PanoramicaChildRow.IsGroup/Indent/ChildCount`, converter `IndentToMargin` nuovo, globale); click su un sottogruppo naviga al sottogruppo. Il contatore "Oggi x/y" continua a contare solo le habit. Suite WPF **495**. **Stato: `fixed`, verifica visiva con gli altri item.**

**Seeder demo `--seed-demo`** (richiesta utente; commit `04eefeb`): `DemoDataSeeder` in Bootstrap, flag command-line sull'app — popola via i servizi reali (stessa pipeline SQLCipher/interceptor) una libreria credibile: 3 gruppi (Salute con sottogruppo Sonno, Mente) + 8 habit su tutti e 5 i tipi, colori/icone Lucide varie, ~100 giorni di completamenti con pattern realistici (Allenamento Lun/Mer/Ven con minuti, Acqua bicchieri, Mood scala 1-5 pesata, Diario journal con testi brevi, Niente fumo resistance), goal rolling 150 min/settimana, 2 tag, 3 note generali + 12 day-note sparse. Idempotente (marker AppSetting `dev.demo_seeded`), deterministico (Random(42)), additivo. Riuso futuro: `dotnet run --project src/Recordum.WPF -- --seed-demo`.

**Restyle riga gruppo — cornice BC3** (richiesta utente post-verifica con screenshot; spec `2026-06-12-group-row-restyle-design.md`, piano `2026-06-12-group-row-restyle.md`, commit `3739b2f`→ vedi sotto; scelta visiva via visual companion): **chevron ▸/▾** (swap geometria Lucide, niente più ToggleButton quadrato), nome gruppo in grassetto; **gruppo aperto = cornice arrotondata** nel colore del gruppo (bordo alpha 80 + velo alpha 10 via `HabitTint`) che racchiude i **figli indentati**; gruppi annidati = cornici annidate. Architettura: view ricorsiva su **`Roots`/`Children`** costruite da `Rebuild` in parallelo alla flat `Rows` (che resta la fonte di verità per selezione/shift/drag — eventi per bubbling, behavior intatto); `RowTemplateSelector` ora risolve per chiave via `FindResource` (autoreferenza dei gruppi annidati). Suite WPF **494** (3 test nuovi). **Stato: `fixed`, verifica visiva utente da fare.**

**Selezione multipla stile file-manager (pacchetto C)** (piano `2026-06-12-multi-selection.md`, commit `16e99d0`→`deffb33`): **Ctrl+clic** toggle, **Shift+clic** intervallo dall'ancora, clic semplice = singola; set in `HomeViewModel` (`_selectedIds`+ancora, `SelectedRow` resta l'API workspace e si annulla con 2+ → workspace chiuso); **azioni bulk** via hook `IRowSelectionHost`: i command di riga (menu contestuale + Canc/Ctrl+E/Ctrl+R) deviano su `TryBulkAsync` quando la riga è in multi-selezione (un solo reload, selezione azzerata dopo); **drag multiplo** (`TryBulkMoveAsync` nel behavior, cicli skippati per-riga); right-click fuori selezione collassa a quella riga; **Esc** azzera la multi-selezione; reload/quick-action preservano il set; attiva in tutte e tre le tab. Suite WPF **491** (8 test nuovi; flaky noto verde in seconda run). **Stato: `verified-fixed`** (utente 2026-06-12, insieme ad A e B). **La spec combinata A+B+C è completata.**

**Colonna Panoramica per i gruppi (pacchetto A, nav-model §5)** (piano `2026-06-11-group-panoramica.md`, commit `bcb0906`→`f890084`): `HabitColumn.Panoramica` + **chip dinamiche per tipo nodo** (gruppo → Panoramica·Note·Configura; habit → le 4 solite; visibilità = flag ∧ applicabilità, i flag sopravvivono allo switch habit↔gruppo); `PanoramicaViewModel` (rollup "Oggi x/y", righe habit discendenti ricorsive con `HeatmapStrip` e ✓, card Goal di gruppo, empty-state); **click su riga figlia naviga** alla habit (`ChildSelected` → `HomeViewModel.SelectById`, che riespande gli antenati collassati); default gruppo → Panoramica; `AllColumnsClosed` e default ora applicabilità-aware; colonna ridimensionabile come le altre (`ui.column_widths` guadagna il token `Panoramica`). Suite WPF **483** (9 test nuovi). **Stato: `fixed`, verifica visiva utente da fare.** Con questo il nav-model §5 (vista gruppo) è chiuso nella sua prima forma; resta piano 3 = selezione multipla (§C).

**Rifiniture albero/colonne (pacchetto B)** (spec `2026-06-11-group-view-tree-refinements-design.md` §B, piano `2026-06-11-tree-column-refinements.md`, commit `423ad22`→ vedi sotto): **B.1** scroll verticale dell'albero (prima le righe oltre la viewport erano irraggiungibili); **B.2** collasso gruppi funzionante (il toggle ▾/▸ era inerte — `Rebuild` ora salta i discendenti collassati, stato per-sessione in `_collapsedGroupIds`); **B.3** click-a-vuoto sul ribbon deseleziona; **B.4** chiudere l'ultima colonna aperta collassa il workspace (evento `AllColumnsClosed` → deselezione); **B.5** riapertura sull'**ultima colonna chiusa** (AppSetting `ui.last_closed_column`, fallback Cronologia). Suite WPF **474** (5 test nuovi). **Stato: `fixed`, verifica visiva utente da fare.** Prossimi: piano 2 (Panoramica gruppo §A), piano 3 (selezione multipla §C).

**Gruppi-basi (nav-model parte 2, §3.2)** (spec `2026-06-11-groups-basics-design.md`, piano `2026-06-11-groups-basics.md`, commit `a62e286`→`2b6e3e9`): bottone Home → menu **＋Nuova▾** (Nuova abitudine / Nuovo gruppo); **parent context-aware** per entrambe le voci (gruppo selezionato → dentro; habit → fratello; nulla → radice; `PickAsync(parentId)` finalmente usato); **"Nuovo gruppo" crea subito** (`HomeViewModel.CreateGroupAsync` → evento `GroupCreated` → MainWindowViewModel apre Configura per la rinomina); **drag-drop evidente**: highlight accent del gruppo bersaglio nella zona "dentro" (`HomeRowViewModel.IsDropTarget` + DragOver/DragLeave in `TreeDragDropBehavior` + trigger in `Recordum.HabitBlock`). **Bonus fix**: rebind dell'editor al cambio habit con colonna Configura aperta (gap della parte 1 — `ConfigureRequested` scatta solo sulla transizione chiuso→aperto). Suite WPF **469** (6 test nuovi; flaky noto verde isolato), build 0/0. **Stato: `verified-fixed`** (utente 2026-06-11). Resta del nav-model: §5 vista di dettaglio del gruppo (giro dedicato).

**Colonne ridimensionabili** (chiude nav-model §8 "larghezze"; spec `2026-06-11-column-resize-design.md`, piano `2026-06-11-column-resize.md`, commit `0a5e48e`→`7a79cdd`): richiesta utente durante la verifica visiva del workspace. `Thumb` di 6px sul bordo destro di ogni colonna (cursore ↔, hover/drag accent), drag → larghezza per-colonna in `HabitScreenViewModel` (clamp 320–1000), **doppio-click reset** al default (460/520/460/480), persistenza CSV su AppSetting `ui.column_widths` scritta a fine drag, restore allo startup insieme a `ui.open_columns`, token malformati/fuori range ignorati. Suite WPF **463** (5 test nuovi; 1 flaky noto verde isolato), build 0/0. **Stato: `verified-fixed`** (utente 2026-06-11).

---

## Stato al 2026-06-11 (sera) — rimodernizzazione UI: Fasi A+B+C eseguite

**Fase C — Passata estetica**: piano `docs/superpowers/plans/2026-06-11-aesthetics-pass.md` eseguito (4 task, commit `7251c25`→`a0d2f31`). Stile condiviso **`Recordum.Segment`** (RadioButton→pill senza pallino, checked=elevated+bordo, focus=accent) adottato da Home tabs (Attive/Archiviate/Cestino in pill-group) e calendario (Settimana/Mese/Anno + Mesi/Heatmap); **ScrollBar flat** implicita a tema (thumb arrotondato, niente frecce, page-click ok); **ricerca Home** con icona Lucide + watermark funzionante (il vecchio `Tag` era inerte). Suite 457 verdi, build 0/0. **Stato: `fixed`, verifica visiva utente da fare (A+B+C insieme).**



**Fase B — Guscio (title bar custom + ribbon + status bar)**: piano `docs/superpowers/plans/2026-06-11-shell-ribbon-titlebar.md` eseguito (5 task, commit `fb8ffdc`→`59cc46b`). `WindowChrome` (caption 36px, snap nativi, min/max/close flat, close hover rosso, fix overflow maximize via RootGrid margin); `WorkspaceRibbon` 44px (Home/Grafici con stato attivo accent | menu Backup Crea/Ripristina/Cronologia + Esporta libreria | Aiuto→Diagnostica, ⚙→Impostazioni); **eliminata la barra superiore testuale + Menu**; `Icon.Settings` completato a ingranaggio pieno + 3 icone nuove; TimerStatusBar compattata. Deviazione documentata: icona Backup apre un ContextMenu (restore/cronologia non avevano altra casa dopo la rimozione del menu). Ctrl+1..5 invariati. Suite WPF 456+1 flaky (verde isolato), build 0/0. **Stato: `fixed`, verifica visiva utente da fare.** Prossimo: **Fase C** (Home toolbar pill, scrollbar, hover audit).



**Piano master**: spec `docs/superpowers/specs/2026-06-11-ui-modernization-plan-design.md` (riferimento Obsidian; decisioni: solo guscio ora, ribbon variante B, categorie future disabilitate = deroga approvata al no-placeholder, title bar custom, ordine A→B→C; nav-model resta parcheggiato come fase 4 del piano).

**Fase A — Finestra Impostazioni unificata**: piano `docs/superpowers/plans/2026-06-11-settings-window.md` eseguito (8 task, commit `b6d5231`→`02a0a54`). `SettingsWindow` stile Obsidian (categorie sx: Aspetto/Generale/Backup/Diagnostica + Sync/Plugin/AI disabled "In arrivo"); nuova UI per `daily_reset_time` (`GeneralSettingsViewModel`); launcher `ISettingsWindowLauncher.ShowAsync(categoria?)`; menu ponte "Impostazioni"; **eliminati** SettingsPopup (orfano), BackupSettingsDialog, DiagnosticInfoDialog, IDiagnosticInfoLauncher, `IBackupDialogLauncher.ShowSettingsAsync`. 7 icone Lucide chrome nuove. Suite WPF **457 verdi** (incl. flaky), build 0/0. **Stato: `fixed`, verifica visiva utente in corso.** Prossimo: **Fase B** (title bar custom + ribbon + status bar).

**Picker template (#8 parte 1)** — `fixed` (commit `20916a6`): sezioni "Modelli predefiniti"/"I tuoi modelli" collassabili (Expander, conteggio in header, azioni pinnate in alto, niente persistenza stato). Spec `2026-06-11-template-picker-collapsible-sections-design.md`. **Verifica visiva utente: da fare.**

---

## Stato al 2026-06-11 (continuazione) — calendario chiuso + rifiniture minori esaurite

**Calendario (filone #3) — chiuso.** Commit `3c97ead`:
- **B-calYearRender** `verified-fixed`: celle Anno collassate per mancanza di `HorizontalContentAlignment/VerticalContentAlignment="Stretch"` sui Button (ereditavano Left/Top → ContentPresenter dimensionava a 0×0 il Grid solo-background). Aggiunto Stretch a `YearCellTemplate`+`HeatCellTemplate`.
- **Settimana/Mese**: dimensione cella **fissa** via `CalendarNavigatorViewModel.CellSize` (40 Mese / 56 Settimana) come unica sorgente; header e body derivano la stessa larghezza colonna (7×(CellSize+4)), left-aligned → allineati senza trigger e **senza ingigantirsi** quando la colonna si allarga.
- **Base cella**: aggiunta base grigia (`Brush.Border`) come sfondo del Button anche in Settimana/Mese (come Anno).
- **Splitter workspace**: niente più rimbalzo — `MaxHabitScreenWidth` 800→1100 + `MaxWidth` sulla ColumnDefinition (`MainWindow.xaml`) così lo splitter si ferma al cap invece di clampare al rilascio.

**Rifiniture minori — tutte chiuse:**
- **RB6c-4** `fixed`: anteprima leggibile delle etichette scala nella sezione Scale dell'editor (`ScalesSectionViewModel.ScalePreview` + riquadro in `ScalesSectionView.xaml`).
- **R-scaleUI** `fixed`: cella scala in registrazione ridisegnata — ≤10 step ora **bottoni segmentati** (ListBox stilizzato in `HabitScreen.xaml`, selezione accent/`Brush.OnAccent`) invece del dropdown; slider >10 mostra l'**etichetta** (`SeriesCellViewModel.SelectedScaleLabel`) invece dell'indice grezzo.
- **R-statePlus**, **R1** (theming flat), **R2** (banner WebView2), **R3** (toolbar Toast), **R4** (backup delay configurabile/reattivo): già implementate in sessioni precedenti, tracker allineato qui.

RB6c-4 + R-scaleUI: **verifica visiva utente da fare** (l'utente ha chiesto di fare tutte le limature e verificare dopo). Suite WPF calendario/scala verde.

**Restano (NON limature minori — richiedono design/brainstorm):** B6b template custom, #8 finestra nuova-abitudine a fisarmonica + crea gruppo, #9 gruppi, R-responsiveUI, R5 audit accessibilità app-wide, nav-model a colonne. Bloccanti vaghi: B2 (personalizzazione note, serve repro), B3 (placeholder).

---

## Come segnalare un nuovo bug

Quando ne incontri uno, copia il template qui sotto in una nuova voce `B<N>` (bloccante) o `R<N>` (rifinitura) sopra. Più dettagli = meno round-trip di domande.

```markdown
### B<N>. <Titolo breve, < 60 caratteri>
- **Stato**: `reported`
- **Quando**: 2026-MM-DD
- **Steps**:
  1. <ogni passaggio per riprodurre, dall'avvio dell'app>
  2. ...
- **Atteso**: <cosa dovrebbe succedere>
- **Osservato**: <cosa succede davvero — sintomo visibile>
- **Screenshot**: <path o "incolla in chat"> (opzionale ma molto utile per bug visuali)
- **Ambiente**:
  - Theme: <System | Light | Dark>
  - WebView2 Runtime installato: <yes/no, vedi Help → Diagnostic info>
  - Schema version: <da Diagnostic info>
  - Branch + SHA: <da Diagnostic info>
- **Note**: <qualsiasi info collaterale: prima funzionava? è successo dopo un'azione specifica? log/stack trace allegata?>
```

**Diagnostic info** (Help → Diagnostic info, dialog modale): copia-incolla l'output e hai l'intera sezione "Ambiente" già compilata.

**Log di crash**: se l'app cracha, `%LocalAppData%\Recordum\logs\recordum.log` ha lo stack trace appeso. Incollalo nella sezione "Note".

---

## Stato al 2026-05-29 (giornata estesa)

**Risolti/verificati questa sessione** (dettaglio in fondo + commit):
- **B1** "Save note" → era **theming chrome (R1)** + WebView2 falso negativo, non un bug di Save. Bottoni invisibili (chiaro-su-chiaro) risolti con chrome flat; editor markdown ripristinato.
- **WebView2 falso negativo** → probe spostato sul Dispatcher UI + logging. `verified-fixed` (editor ricco visibile).
- **R1** chrome flat Button/TabItem/ComboBox → `verified-fixed` (utente).
- **B4** sposta-nel-cestino/archivia non aggiornava la lista → `verified-fixed` (utente).
- **B5** "+"/registrazione + refresh Home su ogni completamento → `verified` (utente: "il riquadro si accende").
- **B6a** instanziazione template scartava metriche/scala/goal/segmenti → fixed (test verdi); scala 1-5 ora registra (`verified` utente).
- **B6c** UI sezioni editor (Scheduling/Streak/Metrics/Scales/Timer/Goal/Notifications/Tags/AI/ResetTime) costruita → `verified` funzionale (utente). Spec `docs/superpowers/specs/2026-05-29-editor-sections-ui-design.md`, piano `docs/superpowers/plans/2026-05-29-editor-sections-ui.md`.

**Risolti nel blocco "limatura" (2026-05-29 sera)** — `verified` da fare dall'utente al prossimo giro:
- **Theming Pass 3** (commit `4c6bac5`): CheckBox flat (implicito), toggle "Advanced" a pillola (stile keyed `Recordum.PillToggle`), DatePicker/Calendar tematizzati (template flat per CalendarDayButton/CalendarButton). Chiude **B7** (calendario chiaro-su-chiaro). NB minore: il navigatore mese del Calendar non persiste l'highlight selezione (usa IsPressed; `CalendarButton.IsSelected` non è trigger valido).
- **B9 i18n** (commit `3b42d2f`): 25 stringhe del'editor + picker localizzate (EN+IT). Restano in inglese solo i **nomi display dei template seed** (dati DB, non stringhe — localizzazione separata da decidere).
- **RB6c-1 day-picker** + **RB6c-2 chip Tag selezionate** (commit `50ef22d`): row di toggle giorni per schedule `SpecificDays` (convenzione `["Mon".."Sun"]` da `ScheduleEvaluator`); chip Tag ora mostrano stato selezionato (accent).
- Fix test culture-independent picker (commit `c0c515b`). Suite: WPF 335, Data 296 verdi.

**Ancora aperte**:
- **B6b** template personalizzato — **feature, richiede design** (brainstorming) prima di implementare. NON fatto di proposito.
- **RB6c-4** picker scala: il dropdown scala c'è, ma rifinitura labels/anteprima da fare.
- **R-scaleUI** layout scala State sgradevole (Cronologia).
- **R-statePlus** "+" su State non fa nulla se l'habit è già selezionata.
- **Theming**: `ToggleButton` generico resta Foreground-only (solo l'Advanced ha il pill keyed) — ok per ora.
- **Nav model** spec `2026-05-29-navigation-model-design.md` parcheggiato (colonne toggle) — dopo le basi.

---

## Stato al 2026-06-01

**Brainstorm "Recordum problemi 2" → spec + esecuzione**:
- **Spec** `docs/superpowers/specs/2026-06-01-abitudine-column-calendar-design.md` (commit `2519edb`): 4 pilastri — colonna Abitudine + `CalendarNavigator` riusabile, note unificate (`Note.TargetDate`), timer Session ibrido, modello a colonne (Abitudine rimpiazza Cronologia nei tab attuali; refactor colonne-chip rimandato).
- **B10** (Configura non personalizza) → `fixed` `3f4dce6` (vedi Risolti).
- **Piano 1 — fondamenta dati schema v2.8** (`docs/superpowers/plans/2026-06-01-schema-v28-data-foundation.md`, commit `3555f45`): **eseguito** subagent-driven, 5 task TDD. Schema **v2.8** additivo: `note.target_date` (+indice), `habit_config.note_marker_color` (excluded) + `session_repeat_count` (versioned+mirror), `segment_definition.duration_seconds`; migration `AbitudineCalendarV28`. Query `INoteService` note-per-giorno/mese. Suite **Data 301 / WPF 337** verdi.
- **Piano 2a — `CalendarNavigator` + tab Abitudine** (`docs/superpowers/plans/2026-06-01-calendar-navigator-abitudine-tab.md`, commit `c2b0370`): **eseguito** subagent-driven, 4 task TDD. Widget calendario riusabile (`CalendarNavigatorViewModel` + controllo, marker provider delegate) che rimpiazza la striscia heatmap nella prima tab, ora **Abitudine**; marcatori completato=colore habit (`GetHeatmapAsync`) / nota=`NoteMarkerColor` (`ListNoteDaysInMonthAsync`); giorno selezionato → `QuickAdd.CompletionDate`. Suite **WPF 345 / Data 301** verdi. **Verifica visiva utente: da fare.**
- **Piano 2b — note-del-giorno unificate** (`docs/superpowers/plans/2026-06-01-day-notes-abitudine.md`, commit `633d750`): **eseguito** (Task 1-2 subagent-driven, Task 3 inline per sovraccarico API). `DayNoteViewModel` (editor breve legato al giorno selezionato, scrive/aggiorna `Note.TargetDate`) nella tab Abitudine; `HabitScreenViewModel` si sottoscrive agli eventi nota → `Calendar.JumpAsync` (refresh pallini-nota); badge data nella timeline della colonna Note (`NoteRowViewModel.DateBadge`, "d MMM" o "Generale"). Suite **WPF 351 / Data 301** verdi. **Verifica visiva utente: da fare.**
- **Prossimo**: Piano 3 (timer Session ibrido — motore auto-avanzamento + ripeti ciclo, UI Timer in Configura, + picker colore-nota `NoteMarkerColor` in Configura). Poi i bug minori residui del report.

**Follow-up aperti dalle code-review del Piano 2a** (non bloccanti):
- **R-onAccent**: introdurre token `Brush.OnAccent` (foreground su accento) nei temi Light/Dark e usarlo al posto di `Foreground="White"` hardcoded (cella selezionata calendario + backfill `PillToggle`/`CalendarDayButton` in BaseStyles) — contrasto dark insufficiente. Pass di theming.
- **R-heatmapDead**: dopo lo swap, `HeatmapViewModel` è ancora costruito e ricaricato in `HabitScreenViewModel` ma non più mostrato nella prima tab (la year-heatmap del Dettaglio è un controllo separato). Rimuovere field/load/sottoscrizione + il `DataTemplate` morto (cleanup dedicato, tocca più superficie).
- **R-fakeWebView2-dup**: `FakeWebView2Availability` duplicata in `HabitScreenViewModelFactory` e `MainWindowViewModelFactory` — estrarre in un fixture condiviso.

---

## Stato al 2026-06-02

**Verifica visiva Piano 2a+2b (utente)** — feedback raccolto sulla tab Abitudine:
- ✓ Note del giorno: funzionano (calendario riflette i pallini, editor del giorno ok).
- **A — range anni navigatore troppo stretto**: dropdown solo 2021..2028 (`SelectableYears = Range(today-5, 8)`). I pulsanti ‹ › sono illimitati. → in Piano cell-markers Task 5.
- **B — DatePicker QuickAdd ridondante**: il `<DatePicker>` nei 5 template QuickAdd ha popup ancora chiaro-su-chiaro **ed** è ridondante (il calendario principale già pilota `CompletionDate`). → rimpiazzare con label read-only "Registrando per:", Piano cell-markers Task 9.
- **C — pallino completato si sovrappone al numero**: triage → brainstorming "marcatori cella calendario".

**Brainstorm "marcatori cella calendario" → spec + piano** (companion visivo):
- **Spec** `docs/superpowers/specs/2026-06-02-calendar-cell-markers-design.md` (commit `35926a8`): cella-giorno ridisegnata — **sfondo tinto** type-aware (heat, intensità relativa) = fatto a colpo d'occhio + quanto; **numero conteggio** type-aware (risolve "3 serie = 1 completamento": resistance conta `SeriesIndex` distinti); **angolino nota** colore per-abitudine (`note_marker_color` già in v2.8) + conteggio se >1; **striscia dettaglio** del giorno selezionato. Niente migrazione (solo due query nuove). Nessuna modalità configurabile (una regola automatica per tipo). **Idea "figura geometrica = goal" parcheggiata** (il modello non ha goal per-giorno: `GoalPeriodType ∈ {Fixed,Rolling,Open}`) — §9 della spec. Note correttive: "session" non è un `HabitType` (counter in `timer_mode='session'`, trattato come counter per ora); tint State relativo (assoluto su scaleMax = refinement).
- **Piano** `docs/superpowers/plans/2026-06-02-calendar-cell-markers.md`: 10 task TDD. Data (magnitudine type-aware, conteggio note) → host (calcola TintStep/Number/NoteCount) → CalendarNavigator muto (render) → converter tint → XAML cella → striscia dettaglio → picker colore-nota in Configura. Quick-fix A (Task 5) e B (Task 9) inclusi.
- **Scope shift**: il picker colore-nota (`NoteMarkerColor`) era in Piano 3; si sposta qui (riguarda il marcatore calendario). **Piano 3 resta solo il timer Session ibrido.**
- **ESEGUITO** subagent-driven (sessione 2026-06-02, branch `fase-4-polish`). 10 task + cleanup; per ogni task implementer → spec-review → code-quality-review. Commit `4a7e6a8`..`b22f09a` (14 feat/refactor + cleanup resx morta). Schema invariato (v2.8, due sole query nuove: `ICompletionService.GetDailyMagnitudeAsync` type-aware + `INoteService.ListNoteCountsInMonthAsync`). `NumberFor` riusato da cella calendario **e** striscia riepilogo (coerenti). `HabitColorTintConverter` (multi-binding hex+step→alpha). Nuova section editor `NotesSectionViewModel` (config-backed) per `NoteMarkerColor`. Quick-fix A (anni 15 indietro) + B (DatePicker QuickAdd → label `Abitudine.QuickAdd.RecordingFor`, vecchia chiave `HabitScreen.QuickAdd.Date` rimossa). Suite **Data 307 / WPF 362** verdi, build 0/0. Review finale olistica: **READY TO MERGE**. **Verifica visiva utente: da fare** (skill `run`).
  - **Catch della review**: la formula resistance "somma per-completamento dei SeriesIndex distinti" del piano è **corretta** per dominio (spec §riga 55) — un reviewer l'aveva flaggata per una parafrasi imprecisa del prompt, confermata sulla spec invece di propagare una falsa fix che avrebbe introdotto un bug.
- **Follow-up aperti (polish-debt, non bloccanti, dalla review finale)**:
  - **R-daySummaryUnits**: la striscia riepilogo mostra numeri nudi (es. State "✓ Done · 4"); la spec §5.5 voleva unità/scala ("Umore 4/5", "100 pagine"). Riusare nome metrica/scala primaria. Per State il "4" senza scala è opaco.
  - **R-noteCountContrast**: l'overlay conteggio-note in cella usa `Foreground="#1c1d22"` hardcoded sopra un colore-nota ora scelto dall'utente → rischio contrasto su colori scuri. Tematizzare (lega a `R-onAccent`).
  - **R-summaryGlyphs**: "✓"/"—" nella striscia sono glyph testuali (spec-acknowledged) ma in tensione col brief "no emoji nella chrome". Valutare icone Lucide.
  - `ListNoteDaysInMonthAsync` ora senza chiamanti di produzione (intenzionalmente tenuta in `INoteService` per usi futuri, spec §3.2); `CompletedColorHex` nome lievemente stale (è la sorgente del tint) — cosmetico.
  - **R-tintRamp** (verifica visiva 2026-06-02): l'effetto heat "è un po' meccanico" (utente). La ramp alpha fissa `{0x38,0x66,0x9e,0xd8}` su 4 step rende i salti netti/duri. Da rivedere (curva più morbida / più step / mescolanza con la surface invece di alpha puro). Non bloccante.
  - **Fix visivi post-verifica (commit `696d450`, verified-fixed utente)**: (1) il tint appariva come "pillola" attorno al numero — il `ControlTemplate` flat del Button centra il contenuto di default → `HorizontalContentAlignment/VerticalContentAlignment="Stretch"` sul Button cella; (2) il tint restava grigio default — `CalendarDayCellViewModel.TintColorHex` era `{get;set;}` non-observable → in `MultiBinding` il child-binding teneva il valore stale letto all'init; reso `[ObservableProperty]`. **Lezione**: ogni sorgente di un `MultiBinding` settata dopo l'init del binding DEVE essere observable, altrimenti resta al default.

**Brainstorm "creazione abitudini funzionale" → decomposizione 3 spec + Spec 1 eseguita** (companion visivo):
- **Decisione di fondo**: creazione = **costruttore block-first** (tela vuota → assembli mattoni → tipo dedotto → Lancia atomico, bozza fino al Lancia). I template restano l'on-ramp type-first (istanziano subito). Aggiunta utente: **template personalizzati**. Decomposto in **Spec 1** (builder non-sessione + draft/launch + template custom), **Spec 2** (modo sessione + runtime timer — assorbe il vecchio "Piano 3"), **Spec 3** (builder come "Configura" su abitudini esistenti con dati + unificazione draft↔live).
- **Spec** `docs/superpowers/specs/2026-06-02-modular-habit-builder-design.md` + **Piano** `docs/superpowers/plans/2026-06-02-modular-habit-builder.md` (14 task TDD). **ESEGUITO** subagent-driven (commit `d94ed8f`..`2af4529`). Grammatica pura `HabitCompositionRules` (mattoni→tipo + guardrail) in Core; `IComposedHabitService.CreateComposedHabitAsync` (Data, transazionale node+config+metriche); `HabitBuilderViewModel`/`HabitBuilderView` (composizione grammar-driven + anteprima); picker "Costruisci da zero" → builder (niente più nodo Action orfano); salva-come-template + sezione "I tuoi template" (rinomina/elimina). Suite **Core 59 / Data 309 / WPF 370** verdi, build 0/0. Review finale: **READY** dopo fix i18n. **Verifica visiva utente: da fare**.
  - **Catch della review (falso positivo evitato)**: in Spec 2 cell-markers la formula resistance del piano era corretta — confermata sulla spec invece di propagare la "fix". Stesso atteggiamento qui.
  - **Fix i18n post-review (commit `2af4529`)**: la view builder hardcodava ~20 stringhe italiane (violava English-first) e `DefaultName` italiano finiva nel `PayloadJson` persistito → localizzato tutto via resx (EN+IT+Designer), test reso culture-independent.
- **Follow-up aperti Spec 1 (deferiti, in questo tracker)**:
  - **R-builderFieldPickers** → `fixed` (2026-06-03, su `fase-4-polish`): aggiunti i ComboBox unità/scala nel field row del builder, visibili condizionalmente per `Nature` (Quantity→unità, Scale→scala) via `DataTrigger`, legati a `HabitBuilderViewModel.AvailableUnits`/`AvailableScales` (caricate da `InitializeAsync` via `IReferenceDataQueryService`, chiamata dal launcher prima di `ShowDialog`). Ora l'utente vede il picker e capisce cosa serve per abilitare il Lancia. **Verifica visiva utente: da fare.**
  - **R-builderMode**: `RecordingMode` (cronometro/countdown) è nella grammatica ma **non esposto** nella view e **non persistito** (`BuildConfig`→null). Cablare `RecordingMode`→`HabitConfig.TimerMode` + controllo Mode quando si fa la Spec 2.
  - **R-templateFidelity** → `fixed` parziale (2026-06-03): il writer (`BuildTemplatePayloadJson`) ora scrive `metrics[].unit`/`scale_code` (i **code**, non i GUID) mappando da `AvailableUnits`/`AvailableScales`, così il reader — che già risolve unità/scala per code — round-trippa unità e scala al re-instantiate. **Resta a Spec 2**: `series_count` (nessun target config nel builder non-sessione, la serie è a tempo di completamento) e `mode`→`TimerMode` (vedi R-builderMode).
  - **R-builderSeriesGuard** (già fixato): CheckBox serie `IsEnabled={Binding CanEnableSeries}` + i mattoni invalidanti bloccati da `CanAddField` → composizione invalida non raggiungibile (verificato in review).

---

## Stato al 2026-06-03

**Fix R-builderFieldPickers + R-templateFidelity (commit `8a7efad`)** — vedi sezione 2026-06-02 (follow-up Spec 1 aggiornati). Poi **verifica visiva utente del builder** (app lanciata via skill `run`) → feedback ricco, triagiato qui sotto. Un bloccante (B11) + 5 rifiniture/feature builder.

**Verifica visiva B11 (2026-06-03, 2ª tornata)** — multi-metrica registra (ok), ma emersi:
- **Fix UI fatti** (commit dopo `89ffcaa`): celle metrica **in verticale** `[Nome] [input] [unità]` (prima WrapPanel orizzontale confuso, unità miste); **rimosso box Note ridondante** del QuickAdd Measured (restava duplicato con "Nota del giorno"/`DayNote` sotto → tenuto solo quello).
- **B11-launch** → **`fixed`** (2026-06-03, commit `b7d82dd`): NON era un'unità mancante — **"g" (Gram) È tra le seed**. Era un **bug**: `HabitBuilderViewModel` non si iscriveva al `PropertyChanged` dei field, quindi scegliere unità/scala nel picker non rifaceva `LaunchCommand.NotifyCanExecuteChanged()` → il bottone restava grigio anche dopo aver scelto. Fix: `AddField` si iscrive (RemoveField si disiscrive), `RaiseLaunchState()` su cambio `UnitOfMeasureId`/`ScaleTemplateId`; aggiunto hint `LaunchBlocked` accanto al Lancia che spiega cosa manca. WPF 380 verdi. **Verifica visiva: da fare.**
  - **R-builderCustomUnit/CustomScale** restano aperte ma **declassate**: i seed coprono i casi comuni (km/m/kg/g/ml/l/kcal/steps/hours/minutes/seconds/pages/reps/sets + scala mood-5). La creazione di unità/scale *nuove* (es. scala 1-100) è una feature a sé, da fare se l'utente ne ha bisogno dopo la ri-verifica.
- **B12** → **`fixed`** (2026-06-03, commit `12ebf88`): rivedere/modificare una registrazione passata. Aggiunta sezione **"Registrazioni del giorno"** nella tab Abitudine: `CompletionListViewModel.LoadDayAsync` filtra le completion del giorno selezionato e formatta ogni `CompletionValue` in `FieldDisplays` ("Nome metrica: valore + unità" o etichetta-scala, **non** l'indice 0-based). Nuova lista `DayCompletions` (rimpiazza la lista ultimi-20 che c'era nella tab), ricaricata al cambio giorno e sugli eventi completion, con modifica/elimina per riga (comandi esistenti) + stato vuoto. Nuovo converter `ZeroToVisible`. WPF 382 verdi. **Verifica visiva: da fare.**
  - **Bug nota** ("compare ma non si edita" nella tab Note): l'editor markdown della tab Note non propaga le modifiche (Save resta disabilitato) — è il vecchio **B1** che riemerge. Da investigare a sé (serve riprodurre WebView2/Toast UI change-event). Il path affidabile di modifica resta `DayNoteView` in Abitudine.
- **B13 (needs-investigation)**: un'abitudine costruita "con varie serie" appare come **Measured a riga singola** (un solo Salva, niente "aggiungi serie") invece che Resistance — e i nomi metrica nello screenshot sono strani ("Serie", "Flessioni reps", "Flessioni"). Sospetto: round-trip template che **perde `series_count`** (R-templateFidelity, metà non risolta) → niente serie → tipo risolve a Counter; oppure naming metrica nel builder. Da investigare: il builder/template con serie deve produrre Resistance (con "+ aggiungi serie" già esistente in `ResistanceQuickAddViewModel`).
- **Counter scala-sola non "accende" il giorno** (caso 1): registrando solo la scala su un Counter (Quantità+Scala), il giorno/cella non si accende perché la magnitudine cella è **type-aware sulla metrica primaria** (la quantità). È **Opzione 2** (visualizzazione multi-metrica), rinviata.
- **R-daySummaryUnits** confermato (caso 3: scala "neutral" → cella mostra "2" nudo, indice 0-based senza etichetta).

**Feedback verifica (2026-06-03)**:
- **B11 (bloccante)**: l'abitudine costruita **non riflette la configurazione** nell'abitudine "funzionale" — vedi voce dedicata sotto. È il nodo critico: il builder produce composizioni multi-metrica che lo strato di registrazione (M3.5, mono-metrica per tipo) non renderizza.
- **R-builderColorPicker**: il color picker è solo esadecimale, il quadrante colore **non è cliccabile**. L'utente non sa gli hex. Richiesta: ruota/palette colori con **colori recenti** + hex affiancato. **Da portare anche in Configura** (stesso bisogno). → feature, richiede design.
- **R-builderIconPicker**: campo Icona accetta testo ma **non c'è un picker** per selezionare un'icona. Stesso bisogno in Configura. → feature (icone Lucide per design brief), forse rimandabile.
- **R-builderCustomUnit**: il picker Quantità elenca solo le unità seed, manca l'inserimento di un'**unità personalizzata** inline. → feature.
- **R-builderCustomScale**: idem per la Scala (scala personalizzata inline). → feature.
- **R-builderSaveFeedback**: "salva come template" non dà **alcun feedback** visivo (il template viene creato ma sembra non succedere nulla). Aggiungere un "salvato ✓" accanto al pulsante. → fix UX piccolo.
- **Nota su "non posso lanciare"**: con Count+Quantity+Time il Lancia restava disabilitato perché la Quantità non aveva unità scelta (gating ora **visibile** ma non spiegato). Con Scala+Quantità (unità+scala scelte) il Lancia funziona. Legato a R-builderCustomUnit + chiarezza del gating.

---

## Stato al 2026-06-04

**Blocco "limatura" autonomo** (utente: "applica tutte le rifiniture che ritieni opportune, poi farò un check visivo e d'esperienza"). Eseguito inline con TDD dove tocca logica. **Verifica visiva utente: da fare** su tutto il blocco.

- **B13 → `fixed`** (commit `fix(wpf): B13`): root cause **non** era il round-trip template (il reader legge `habit_type` direttamente → il path template produceva già Resistance). Il bug reale: la CheckBox Series binda `SeriesEnabled` **TwoWay**, bypassando il comando `ToggleSeries` (che faceva `Recompute`). Mancando `OnSeriesEnabledChanged`, `DerivedType` restava **Counter stale** → `BuildRequest` (usa `DerivedType`) lanciava un nodo Counter → instradato a `MeasuredQuickAdd` riga singola. Fix: `partial void OnSeriesEnabledChanged => Recompute()` + rimosso `Recompute` ridondante in `ToggleSeries`. 2 test di regressione end-to-end (path proprietà diretta). I nomi metrica strani ("Serie", "Flessioni reps") erano nomi-campo scelti dall'utente, non un bug. Stessa classe di B11-launch (stato derivato che non si propaga). `series_count` round-trip resta cosmetico/deferito a Spec 2 (ResistanceQuickAdd parte comunque da 1 serie + bottone aggiungi).
- **R-builderSaveFeedback → `fixed`**: badge "Salvato ✓" (`HabitBuilder.TemplateSaved`, EN+IT) accanto a "Salva come template", `TemplateSaved` observable settato dopo il save e azzerato su ogni edit che cambia il payload (nome/colore/icona/campi/serie/modo/unità/scala).
- **R-onAccent → `fixed`**: nuovi token `Brush.OnAccent` (Light=bianco, Dark=`#09090B`) + `Brush.Success` in entrambi i temi. Sostituiti i `Foreground` bianchi hardcoded su sfondo accent: cella calendario selezionata (`CalendarNavigator`), `CalendarDayButton`/`CalendarButton`/pill toggle (`BaseStyles` ×3).
- **R-noteCountContrast → `fixed`**: nuovo `ContrastForegroundConverter` (nero/bianco per luminanza BT.601 del colore-nota) rimpiazza l'hardcoded `#1c1d22` dell'overlay conteggio-note. NB: `Brush.OnAccent` NON era semanticamente giusto qui (lo sfondo è il colore-nota scelto dall'utente, non l'accent) → converter di contrasto dedicato.
- **R-tintRamp → `fixed`** (verifica soggettiva): ramp alpha ammorbidita `{0x26,0x4a,0x78,0xae}` (era `{0x38,0x66,0x9e,0xd8}`) — toe più dolce + ceiling più basso così il giorno pieno è una tinta, non una lastra. Una riga, facile da ritarare a vista.
- **R-daySummaryUnits → deferito** (deliberato): la striscia riepilogo deriva da una singola magnitudine type-aware che perde l'identità della metrica nei casi multi-metrica; il pannello "Registrazioni del giorno" (B12) **già** mostra i valori leggibili con unità/etichette subito sotto; farlo bene richiede una decisione di design sul riepilogo multi-metrica. Non fatto per evitare un mezzo-fix.
- Suite **WPF 385** verdi (+3), build 0/0. Data invariata (nessuna modifica Data).
- **Cleanup ancora aperti** (non visibili, non fatti questo giro): R-heatmapDead, R-fakeWebView2-dup, R-summaryGlyphs.

---

## Stato al 2026-06-05

**Sessione debugging B1 → fix + due bug di layout emersi dal primo uso reale dell'app, + brainstorm feature cronologia note.**

- **B1 → `fixed` + verified-fixed** (commit `64dfa31`): editor markdown — feedback-loop infinito (~93k change events) modificando una nota esistente. Root cause confermata con instrumentation del bridge JS↔.NET (poi rimossa): `setMarkdown()` di Toast UI emette più change sincroni, il one-shot `suppressNext` ne soppeva uno solo, il secondo rientrava dal two-way binding e la normalizzazione markdown impediva la convergenza dei guard → loop. Fix: flag a scope sincrono `applyingRemote` (sopprime tutti i change durante `setMarkdown`) + guard double-init `WebMessageReceived`. Dettaglio nella voce B1 sotto. **Metodo**: systematic-debugging — isolato il fault confrontando DayNote (TextBox semplice, funziona) vs Notes (editor WebView2, rotto), poi instrumentation ai confini dei componenti.
- **B-layout-window → `fixed` + verified** (commit `f265ad9`): la finestra apriva a 1280×760 (>work area 1280×672 su questo display) → nav alto e bottoni/status bar in basso fuori schermo. `FitToScreen()` clampa Width/Height alla work area + rilassa i minimi. Helper puro `ClampToWorkArea` unit-tested (3 casi). Schermo utente: 1280×720, DPI 96.
- **B-layout-scroll → `fixed` + verified** (commit `147b2f8`): la tab **Abitudine** non era scrollabile (a differenza di Dettaglio) → calendario+quickadd+timer+nota-del-giorno+registrazioni eccedevano l'altezza, editor "Nota del giorno" e Salva tagliati sotto. Avvolta in `ScrollViewer`. (Richiesta utente: scroll verticale + più compatto — scroll fatto; compattezza calendario rimandata, cella già 34px.)
- Suite **WPF 388** verdi (+3 layout), build 0/0. Working tree pulito.
- **Feature richiesta in verifica B1**: "la nota resta ancorata al suo giorno + cronologia modifiche con ripristino". Brainstorm completo → spec `docs/superpowers/specs/2026-06-05-note-version-history-design.md` (commit `2a93832`). Schema **v2.9** (`note_history`, mirror `HabitConfigHistory`).

**Note version history + àncora data — ESEGUITO** (piano `docs/superpowers/plans/2026-06-05-note-version-history.md`, 11 task TDD, eseguito inline executing-plans):
- **Schema v2.9** additivo: tabella `note_history` (`id, note_id, body_md, created_at`, FK cascade su `notes.id`, indice `(note_id, created_at)`); migration `NoteHistoryV29` + bump `schema_version` → "2.9". Commit `8ced678` (Core model+DTO), `1853894` (EF config), `f67414b` (migration+seed).
- **Servizio** (`NoteService`): snapshot del body ad ogni Create/Update che cambia `BodyMd` (stessa `SaveChangesAsync`); `ListVersionsAsync` (newest-first) + `RestoreVersionAsync` (ripristino non-distruttivo → passa per `UpdateAsync`, crea nuova versione); delete history esplicita in `DeleteAsync`. Ordinamento `ListByHabitAsync` per **àncora stabile** (`TargetDate ?? data(CreatedAt)`, poi `UpdatedAtMs`) → l'edit non sposta più la nota. Commit `b4a1181`, `cd3e3a4`.
- **WPF**: `NoteRowViewModel.AnchorDateDisplay` + `WasModified`/`ModifiedAtDisplay` (commit `3209f8d`); `NoteHistoryViewModel` + `NoteVersionRowViewModel` (lista versioni + anteprima read-only OneWay + restore, evento `Restored`); `NotesViewModel` show/hide history (`IsShowingHistory`, `ShowHistoryCommand`/`HideHistoryCommand`, child `History`); XAML — context menu "Cronologia" sulla riga nota + pannello cronologia inline che commuta con l'editor, riga "modificata il" condizionale all'àncora.
- Suite **Core 59 / Data 315 / WPF 396** verdi, build 0/0. **Verifica visiva utente: da fare** (skill `run`): note non saltano più in cima dopo edit; àncora data + "modificata il"; click destro → Cronologia → pannello inline; selezione versione → anteprima; Ripristina → contenuto ripristinato + torna all'editor + nuova versione in cima.

---

## Stato al 2026-06-08

**Blocco limature autorizzato** (utente: "facciamo prima le limature poi verifico"). 5 item chiusi, TDD dove tocca logica. Suite **WPF 388** verdi, build 0/0. Data/Core invariati.

- **R-statePlus → `fixed`** (commit `fix(wpf): R-statePlus`): il "+" su un'abitudine State/Resistance/Journal **già selezionata** non faceva nulla. Root cause: l'handler `OnRowQuickActionExecuted` fa solo `SelectedRow = row`; se la riga è già quella selezionata il setter `[ObservableProperty]` è un no-op → nessun `PropertyChanged` → `MainWindowViewModel` non ricarica la workspace. Fix: nuovo evento `HomeViewModel.HabitActivationRequested` alzato quando la riga è già selezionata; `MainWindowViewModel` lo gestisce ricaricando `HabitScreen.LoadAsync`. 2 test (già-selezionata→evento; non-selezionata→cambio selezione, niente evento). **Stessa classe** di B11-launch/B13: stato non propagato perché il "lavoro" è legato a un cambiamento che non avviene. **Verifica visiva: da fare.**
- **R2 → `fixed`** (commit `feat(wpf): R2`): il banner "WebView2 non disponibile" nell'editor ora ha un **link di download** del Runtime (Hyperlink → `developer.microsoft.com/microsoft-edge/webview2/`, aperto con `Process.Start(UseShellExecute)` nel code-behind di `HabitScreen.xaml.cs`). Nuova stringa `Editor.WebView2.NotAvailable.Download` (EN+IT). **Verifica visiva: da fare** (solo su macchine senza WebView2 Runtime).
- **R-heatmapDead → `fixed`** (commit `refactor(wpf): R-heatmapDead`): rimosso `HeatmapViewModel` (classe + test + `DataTemplate`) — era costruito/caricato/sottoscritto in `HabitScreenViewModel` ma **non più renderizzato** da quando `CalendarNavigator` (cell-markers) l'ha sostituito nella prima tab. La year-heatmap del Dettaglio è `YearHeatmapViewModel`, separata, intatta. Ctor `HabitScreenViewModel` invariato (`ramp`/`goals` ancora usati da `Detail`). 3 test che asserivano su `Heatmap.Cells` ri-mirati su `CompletionList.Rows` (stesso reload-on-event). Invisibile.
- **R-fakeWebView2-dup → `fixed`** (commit `test(wpf): R-fakeWebView2-dup`): estratta fixture condivisa `FakeWebView2Availability(bool available = true)` in `tests/.../Fixtures/`; rimosse 6 copie locali (con varianti `MainWindowFakeWebView2Availability`, `FakeWebView2AvailabilityDiag` per il caso `false`). Lasciata `FakeAvailability` in `MarkdownEditorControlTests` (nome diverso, fuori scope dichiarato). Solo test.
- **R4 → `fixed`** (commit `feat(wpf): R4`): `BackupSchedulerHost` aveva interval hardcoded `FromMinutes(60)` e aspettava **un intero intervallo** prima del primo tick → un backup dovuto al lancio attendeva fino a 60 min. Nuovo `BackupSchedulerOptions` (singleton, `InitialDelay`=2min default + `Interval`=30min default); il loop ora fa `Task.Delay(InitialDelay)` → primo `TickAsync` → poi `PeriodicTimer(Interval)`. Così il primo run è reattivo ma si evita ancora il race con lo startup DB. 2 test (primo tick dopo InitialDelay con timing piccolo; idempotenza). Infra, non visibile.

**Limature NON fatte** (richiedono design/brainstorm, non sono limature): R-builderColorPicker, R-builderIconPicker, R-builderCustomUnit/CustomScale, R-daySummaryUnits, R-summaryGlyphs (icone Lucide), B6b template custom, Spec 2 timer Session.

### Verifica visiva 2026-06-08 (utente) — note-history + limature

Lanciata l'app (skill `run`). Feedback + fix in sessione:
- **Note version history + limature → `verified-fixed`** (utente: "sembra funzionare tutto"). Cronologia/ripristino/àncora ok, R-statePlus ok.
- **#1 ordine note (riemerso in verifica) → `fixed`+`verified`** (commit `9f82a59`): l'ordinamento àncora usava `UpdatedAtMs` come tiebreak tra note dello **stesso giorno** → modificarne una la spostava in cima. Tiebreak cambiato a `CreatedAt` (ordine di creazione stabile): l'edit non sposta più la nota. Test `ListByHabitAsync_same_anchor_keeps_creation_order_after_edit`.
- **#3 freeze transitorio tab Note + "ripristino non faceva niente"** — freeze **non riproducibile** (transitorio, poi recuperava; exit 0). Niente root cause certa; applicati due fix mirati a cause plausibili e comunque reali, poi `verified` dall'utente:
  - **`bae4f61`**: l'anteprima cronologia era un **secondo `MarkdownEditorControl`** (con WebView2) sempre istanziato nella tab Note e — in fallback — perfino **editabile** (FallbackBox non read-only, scriveva su un binding OneWay). Sostituita con `TextBox` read-only → un solo editor nella tab.
  - **`54f656e`**: `RestoreVersionAsync` teneva aperti **due context DB sovrapposti** sullo stesso file SQLCipher (possibile `SQLITE_BUSY` in produzione; il `DbFixture` in-memory `:memory:;Cache=Shared` a connessione singola lo mascherava). Ora legge il body in un context e lo dispone **prima** di aprire il context di scrittura.
- Suite **Data 316 / WPF 388** verdi, build 0/0.

### B14. Journal — editor di completamento vs colonna Note (design)
- **Stato**: `reported` (2026-06-08) — **deferito per scelta utente** ("pensiamoci dopo al journal").
- **Sintomo**: in un'abitudine Journal la tab Abitudine mostra sia l'editor journal (QuickAdd) sia il blocco "Nota del giorno"; scrivendo nell'editor e salvando, **niente compare nella colonna Note**.
- **Non è un bug/regressione**: il Journal registra **`Completion`** con corpo markdown (visibili in Abitudine/Dettaglio), la colonna Note mostra entità **`Note`** — due archivi distinti. Serve una **decisione di design** (es. nascondere DayNote sulle abitudini Journal, o unificare journal-entry↔note). Da brainstorming quando si riprende.

---

## Stato al 2026-06-09

**Verifica visiva utente del builder multi-metrica (B11)** → core **verified-fixed**; raccolto un batch di feedback (nuove voci sotto, da triagiare/decidere ordine).

- **B11 → `verified-fixed`**: builder "da zero" → Lancia → registrazione mostra tutte le metriche con la propria unità/scala; salva e compare in "Registrazioni del giorno" con valori leggibili; Resistance con serie ha "Aggiungi serie". Spec 1 builder ora pienamente registrabile.

**Nuovi punti dal giro:**

*Limature UX chiuse (commit `f8d9238`, WPF 392 verdi) — verifica visiva utente: da fare:*
- **R-navCloseColumns → `fixed`**: `MainWindowViewModel.ShowHome` ora azzera `Home.SelectedRow`; deselezionare collassa le colonne workspace+editor (la loro visibilità/larghezza tracciano `SelectedRow`). Cliccare "Home" riporta a una Home pulita senza passare da Archiviate/Cestinate. Test `ShowHome_deselects_the_row_and_closes_the_workspace`.
- **R-seriesRecordingsFlat → `fixed`**: `CompletionListViewModel.BuildFieldDisplays` raggruppa per `SeriesIndex` — Resistance multi-serie legge "Serie 1 — Conteggio: 12 · Tempo: 30" (una riga per serie) invece della lista piatta; i campi dentro una serie sono ordinati per `MetricDefinition.SortOrder` (corregge anche l'ordine sballato visto a video). Mono-serie invariato (niente label "Serie 1"). Test `LoadDayAsync_groups_fields_by_series_for_multi_series_completion`.
- **R-builderTimeUnit → `fixed`**: il mattone **Tempo** ha un picker unità opzionale (hours/minutes/seconds) — non gate sul Lancia (l'unità tempo è facoltativa) ma persistito sul `MetricDefinition` così la registrazione/grafici mostrano "30 min" invece di "30" nudo. `HabitBuilderViewModel.AvailableTimeUnits` (sottoinsieme filtrato per code) + ComboBox condizionale su `Nature==Time`. Test `InitializeAsync_populates_time_units_subset` + `Launch_persists_picked_time_unit_on_the_metric`.

*Feature di design (richiedono brainstorm — prossimo fronte):*
- **R-builderColorPicker + R-builderIconPicker → `fixed`** (2026-06-09): brainstorm → spec `docs/superpowers/specs/2026-06-09-creation-pickers-design.md` + piano `docs/superpowers/plans/2026-06-09-creation-pickers.md` (6 task TDD), **eseguito** su `fase-4-polish`. Due picker condivisi, gradevoli e VM-agnostici, adottati in **builder e Configura**:
  - **Colore**: `ColorSwatchPicker` con popup — palette curata multi-tonalità (36 swatch) + riga **recenti** (`IRecentColorStore`/`RecentColorStore` su `IAppSettingService`, cap 8, dedup, persistiti in `ui.recent_colors`) + entry hex con anteprima. I recenti si registrano al commit reale (Launch nel builder, color-change valido nell'editor). Commit `f17dd44`→`7de996c`.
  - **Icona**: `IconCatalog` (40 chiavi Lucide curate + `Search`) → `Icons.xaml` +40 geometrie (test di allineamento catalogo↔risorse); `IconPicker` con popup — griglia cliccabile + ricerca + converter `IconKeyToGeometry`. Commit `1621a99`, `82b3631`, `171a508`.
  - Suite **WPF 406** verdi, build 0/0. **Verifica visiva utente parziale (2026-06-09)**: builder — color picker (palette/recenti/hex) + icon picker (griglia Lucide + ricerca) **verificati**. **Restano da verificare**: (4) gli stessi picker in Configura, (5) leggibilità popup nei temi chiaro/scuro.
- **R-recordingsLog** (nuovo): manca un **elenco completo delle registrazioni** "a mo' di lista" (non solo "del giorno"), con **filtro per abitudine**. L'utente propende per metterlo nella tab **Abitudine** (o Dettaglio). → feature, richiede design (dove + filtro + scope cross-giorno).

### Blocco limature 2026-06-09 (sera) — eseguito, verifica visiva utente: da fare

Suite **WPF 415** verdi (build 0/0) modulo un flaky pre-esistente (vedi sotto).

- **R-summaryGlyphs → `fixed`** (commit `2e469f7`): i glyph testuali "✓"/"—" della striscia riepilogo del giorno sostituiti da icona Lucide. Done → `Icon.CheckCircle` reso come `Path` (colore `Brush.Success`); not-done → nessuna icona, solo testo muto. `DayNoteViewModel.SummaryIconKey` observable + resx ripulite dai glyph. Sbloccata dal catalogo icone + converter del lavoro picker. Test `SetDaySummary_exposes_a_done_icon_only_when_done`.
- **R-daySummaryUnits → `fixed`** (commit `db741b3`): la striscia ora arricchisce l'aggregato con unità/scala della metrica primaria — Counter "4 km", State mappa il valore alla sua etichetta scala ("Buono") invece dell'indice nudo. Resistance/Action/Journal invariati (semantica di conteggio). Logica pura `DaySummaryFormatter` (8 unit test); l'host risolve la metrica primaria via `_metrics`/`_refData` (solo per Counter/State). **Non è il mezzo-fix temuto**: caso ambiguo multi-metrica gestito dal pannello "Registrazioni del giorno" (B12) sotto la striscia.
- **R3 → `fixed`** (commit `ce155fe`): toolbar editor markdown — aggiunti `task`/`table`/`codeblock`. Source-view già disponibile via mode-switch Markdown/WYSIWYG nativo di Toast UI (non nascosto). `image` **escluso** di proposito (senza `addImageBlobHook` l'upload file inserirebbe blob URL non persistenti).
- **Deselezione click-a-vuoto Home → `fixed`** (commit `667c623`, complemento di `R-navCloseColumns`): cliccare spazio vuoto **dentro la regione albero** (`TreeRegion`) azzera `SelectedRow` → collassa le colonne workspace/editor. Scoped all'albero così click su top-bar/filtri non deselezionano.
- **R5 (accessibility) → `fixed` parziale** (commit `c9442e4`): chiusa la **lacuna mouse-only dei due picker** appena creati — trigger e celle ora focusabili + attivazione Invio/Spazio; all'apertura il focus entra nel popup (prima cella / search box), Escape chiude e riporta il focus al trigger; `AutomationProperties.Name` su trigger e celle. **Resta aperto l'audit a11y app-wide** (focus order su tutte le view, AutomationPeers, contrasto WCAG, nav-tastiera di tutti i controlli custom) — effort separato, candidato a milestone/brainstorm dedicato.
- **Deselezione click-a-vuoto — fix hit-test** (commit `da1afc1`): la prima implementazione non scattava perché la regione albero era un `ContentControl` (il cui template di default ignora il `Background`) → lo spazio vuoto sotto le righe non era hit-testable, il click cadeva sullo sfondo dell'`UserControl` (fuori dalla regione scoped). Avvolta in un `Border` con `Background="Transparent"` → Row 2 hit-testable end-to-end. **Verified utente**: funziona.
- **B-reentrancy LoadAsync → `fixed`** (commit `5e7d0ff`): la deselezione ha esposto un **crash da rientranza** (`NullReferenceException` in `HabitScreenViewModel.LoadAsync` riga 136 `Calendar.CompletedColorHex = Node.Color`). `OnHomePropertyChanged` è `async void` senza cancellazione → selezione+deselezione rapide producono `LoadAsync` sovrapposti; il load superato, ripreso dopo `Task.WhenAll`, dereferenziava il `Node` azzerato dalla deselezione. Fix: **contatore di generazione** che aborta i load superati dopo ogni await + variabile locale `node` (il campo non può più azzerarsi a metà). Test deterministico che intervalla una deselezione gateando un partecipante di `WhenAll`. Latente anche col deselect del pulsante "Home" (R-navCloseColumns).
- **Flaky test noto**: `HomeViewModelTests.CompletionRecorded_event_reloads_the_list_for_visual_feedback` fallisce **a intermittenza solo nella suite completa** (415 test, contesa CPU sul reload async event-driven: `HomeCallCount` resta 1 invece di >1) ma **passa sempre isolato** (verificato con e senza le modifiche di sessione). Non causato da queste limature. Da stabilizzare (es. attesa deterministica dell'handler) in un giro dedicato.

---

## Stato al 2026-06-10

**Batch feedback utente (10 punti) → triage + cluster bug eseguito.** Triage: bug (2,4,6) vs feature (1,3,5,7,8,9,10). Utente ha scelto il cluster bug; #2 e #7 si sono evoluti in corso d'opera.

- **#4 colore live → `fixed` + verified** (commit `3015f19`): le modifiche header in Configura (nome/colore/icona) ora pubblicano `HabitUpdated` sul bus; Home ricarica la riga (accento/nome) e il workspace aperto ricolora i marcatori calendario live. `HabitUpdatedPayload` aggiunto (il nome evento esisteva ma non era mai alzato). L'editor guadagna un `IEventBus` opzionale. Test: il cambio colore pubblica l'evento.
- **#6 colore-nota in Configura → `fixed` + verified** (commit `3015f19`): la sezione Note di Configura usa il `ColorSwatchPicker` condiviso (palette + recenti + hex) invece della sola casella hex; recenti condivisi via `IRecentColorStore`.
- **#7 selettore colore visuale → `fixed`** (commit `d74bd62`): nuovo controllo `HsvColorPicker` (quadrato saturazione/luminosità + striscia tonalità trascinabili) dentro il popup di `ColorSwatchPicker` → tutti e tre i picker permettono di scegliere qualsiasi colore visivamente, non solo digitare l'hex (richiesta esplicita utente: "solo l'hex non serve a molto"). Matematica `HsvColor` pura (16 unit test, round-trip RGB↔HSV). **Verifica visiva: da fare.**
- **#2 Tempo non registrabile → NON era un bug dati** (commit `d74bd62`): l'utente ha scoperto che la cella Tempo **finisce fuori vista** in colonna stretta; allargando, compare. Le celle delle serie Resistance erano in `StackPanel` orizzontale che tagliava a destra → cambiato in `WrapPanel` (vanno a capo). **Verifica visiva: da fare.**

**Richiesta trasversale nuova**: *"tutta la UI dovrebbe essere più godibile e proporzionata quando scalata"* → **R-responsiveUI** (nuovo, feature/design): layout responsivo su ridimensionamento finestra/colonne. Il fix wrap della registrazione è il primo tassello; serve un pass più ampio (proporzioni, clipping, scaling) — brainstorm.

**Altri fix di sessione (2026-06-10)**:
- **Recenti color picker → `fixed`** (commit `f1e7667`): la riga "Recenti" non appariva in Configura perché `HasRecentColors` si ricalcolava solo al cambio del *riferimento* della collezione `RecentColors`, ma in Configura la collezione si popola async **dopo** il bind. Ora il controllo si sottoscrive a `CollectionChanged`.
- **#1+#3 icona → `fixed`** (commit `66d350e`): l'icona era persistita (`HabitNode.Icon`) ma mai mostrata. Cablata via `HomeTreeRow` → proiezione query → `HomeRowViewModel`; render `Path` Lucide (tinta col colore abitudine) prima del nome nella riga Home e nell'header del workspace. Collassa se vuota o non risolvibile (icone legacy free-text). **Verifica visiva: da fare.**
- **#10 slide colonna → `verified-fixed`** (commit `3897ddc`, rifatto `e840fb4`→`87c03d4`): la prima versione animava `ColumnDefinition.Width` → ricalcolo di layout del sotto-albero pesante a ogni frame = scatti (peggiori in software rendering/RDP). Rifatto **transform-based**: l'apertura fissa la larghezza una volta (1 layout) e fa scivolare il contenuto con una `TranslateTransform.X` animata (solo composizione, niente layout per frame); la chiusura tiene la `GridLengthAnimation` sulla colonna ormai vuota (economica). Nettamente più fluido; il residuo su questa macchina è software rendering (no GPU, probabile VM/server), non un limite di WPF. Spec `2026-06-10-column-slide-animation-design.md`. **Verifica visiva: OK.**

- **#5 evidenziazione abitudini → `fixed`** (commit `c88a220`→`fd518dd`): identità visiva habit. `HabitTintBrushConverter` (hex + alpha → brush semitrasparente) + stile condiviso `Recordum.HabitBlock`: le righe Home (habit e gruppo) sono blocchi tinti ~10% del colore abitudine, la riga selezionata passa a ~20% + bordo interno colorato via nuovo flag `HomeRowViewModel.IsSelected` (sincronizzato da `HomeViewModel.SelectedRow` e ri-applicato a ogni `Rebuild`). Workspace: striscia 3px in alto + stile tab `Recordum.HabitTab` con sottolineatura/label del tab attivo nel colore abitudine. Spec `docs/superpowers/specs/2026-06-10-habit-visual-identity-design.md`. **Verifica visiva: OK.**
  - **Estensione identità dentro il workspace → `verified-fixed`** (commit `2a3f536`→`25483b2`): override locale di `Brush.Accent` dentro `HabitScreen` = colore abitudine, via `BindingProxy` (porta `Node.Color` nello scope risorse) + `HexToColorConverter`. Tutti i consumer `DynamicResource Brush.Accent` (hover/focus bordi Button/ComboBox, selezione TextBox, today/giorno-selezionato calendario) adottano il colore, live da Configura; Home/Charts/EditorPane restano neutri. Contrasto: giorno selezionato calendario passa da `Brush.OnAccent` fisso a `ContrastForeground(TintColorHex)`. Spec `2026-06-10-habit-screen-color-identity-design.md`. **Verifica visiva: OK.** (Redesign visivo completo del calendario → filone #3.)

**Feature ancora aperte dal batch (richiedono brainstorm)**:
- **#8 finestra "Nuova abitudine"** a fisarmonica (Configura fisso → toggle Preconfezionati → toggle Personalizzati) + bottone "Crea nuovo gruppo".
- **#9 gruppi** (iniziare) — grande, multi-sessione.
- **R-responsiveUI** — pass layout responsivo (il wrap registrazione è il 1° tassello).

---

## Stato al 2026-06-11 — Calendar view modes + redesign Anno (filone #3)

**Contesto**: continuazione del filone #3 (redesign visivo completo del calendario). Piano `docs/superpowers/plans/2026-06-10-calendar-view-modes.md` + spec `2026-06-10-calendar-view-modes-design.md`.

**Fatto e COMMITTATO** (Task 1-4 del piano, versione "celle quadrate piene"):
- Marker provider passato da per-mese a **range di date** (`CalendarMarkers`/`CalendarMarkerProvider(from,to,ct)`); host `LoadMarkersAsync` cicla i mesi del range e **normalizza il tint su tutto il range** (no modifiche Data-layer).
- Enum `CalendarViewMode {Week,Month,Year}` + nav view-aware (`PrevCommand`/`NextCommand`/`StepAsync`), `HeaderLabel`, `SetViewModeCommand`, `_weekAnchor`.
- `CalendarDayCellViewModel.TooltipText`.
- Toggle Settimana/Mese/Anno in XAML.
- Commit: `refactor(wpf): calendar markers by date range...` → `feat(wpf): calendar Week/Month/Year view modes` → `feat(wpf): TooltipText` → `feat(wpf): view-mode toggle + square cells + year heatmap body`.

**Decisioni di design LOCKATE con l'utente** (via preview browser `docs/calendar-preview.html`):
- **Mese**: cella ~40px (più piccola/compatta della prima versione).
- **Settimana**: cella ~56px.
- **Anno**: **12 mini-mesi** (riquadri-mese con dentro i giorni) come default + **sub-toggle [Mesi | Heatmap]** (heatmap = GitHub 7×53). Default Mesi.
- **Conteggio abitudine** dentro la cella: in **cerchio bianco contrast-aware** (segue il foreground del bottone → bianco in dark, scuro in light), **capsula** quando il valore è largo, **abbreviazione ≥1000** (`1.2k`/`13k`/`125k`/`1.2M`/`12M`, valore esatto nel tooltip). Serve a **distinguerlo dal numero del giorno** (piccolo/muted in alto a sx).
- **Triangolo nota** presente anche in Anno (mini-mesi + heatmap).
- **Giorni-settimana dentro i mini-mesi**: RIMANDATO (d'accordo con l'utente).

**Implementato ma WIP / NON funzionante visivamente** (committato come WIP, build 0/0, suite WPF 448 verde):
- `CalendarMonthBlockViewModel` (new), `MonthBlocks` + `YearWeeks` (53×7, istanze condivise con `Cells`), `VisibleCells`, `BuildYearBlocks`/`BuildYearHeatmap`, `[ObservableProperty] YearAsHeatmap` + `SetYearHeatmapCommand`.
- Host `FormatMag` con abbreviazione (`AbbreviateLarge`).
- `InverseBooleanConverter` (per il radio "Mesi" = !YearAsHeatmap).
- XAML: `DayCellTemplate` con ring/capsula contrast-aware, `YearMonthTemplate`+`YearCellTemplate` (mini-mese), `HeatCellTemplate`, sub-toggle, due body Anno (mini-mesi / heatmap).
- Test VM: `CalendarNavigatorViewModelTests` 17 verdi (view modes, heatmap toggle, blocks).

### B-calYearRender. Le celle-giorno in ANNO non si renderizzano (BLOCCANTE, aperto)
- **Stato**: `verified-fixed` (2026-06-11, commit `3c97ead`, utente "ci siamo"). **Root cause**: i Button delle celle Anno (`YearCellTemplate`/`HeatCellTemplate`) non impostavano `HorizontalContentAlignment/VerticalContentAlignment="Stretch"`, quindi ereditavano Left/Top dal `Control` e il ContentPresenter del template chrome dimensionava il Grid (solo-Border-background) alla sua desired size 0×0 → celle invisibili (si vedeva solo il `Border Bd` del template = bordo "oggi" arrotondato + il Path nota con desired 6×6). Fix: aggiunto lo Stretch a entrambi i template (come già faceva `DayCellTemplate`). **Follow-up della stessa sessione** (vedi "Stato al 2026-06-11 (continuazione)"): Settimana/Mese a dimensione fissa via `CalendarNavigatorViewModel.CellSize` (no ballooning, header allineato), base grigia delle celle, splitter senza rimbalzo.
- **Stato (storico)**: `triaged` (root cause parziale, fix tentati insufficienti).
- **Sintomo**: in **Anno → Mesi** i mini-mesi mostrano solo il nome del mese; in **Anno → Heatmap** la griglia è vuota. In entrambi i casi si vedono solo il **bordo "oggi"** (come un ovale/anello sottile) e i **triangoli nota** fluttuanti — le celle-giorno (quadratini tint/base) **collassano o non appaiono**. **Mese e Settimana funzionano** (Mese verificato bello dall'utente).
- **Diagnosi**:
  - Mese/Settimana = **singolo** ItemsControl + `MaxWidth` (larghezza definita) e le celle prendono altezza dal **contenuto testuale** (numero giorno) → niente collasso.
  - Anno usa **ItemsControl annidati (2 livelli)** dentro `ScrollViewer`; le celle Anno **non hanno testo** → desired height 0; in pannelli a vincolo infinito (UniformGrid/StackPanel dentro ScrollViewer) la misura collassa.
- **Tentativi FALLITI** (tutti lasciano la griglia invisibile):
  1. Versione iniziale flat `UniformGrid Rows=7 Cols=53` in ScrollViewer orizzontale → vuoto (classico UniformGrid in larghezza infinita).
  2. `Height="{Binding ActualWidth, RelativeSource=Self}}"` (quadrato auto) → **deadlock a 0** (ActualWidth iniziale 0, nessun contenuto a dare altezza).
  3. **Dimensioni FISSE** (mini 14px, heat 15px) **+ base `Border Background=Brush.Border`** → **ancora vuoto** secondo l'utente. ⇒ il problema NON è solo l'altezza: le celle probabilmente non vengono misurate/arrangiate dai pannelli annidati, o un binding annidato fallisce.
- **PROSSIMI PASSI consigliati (ordine)**:
  1. **Attivare il tracing dei binding WPF** (`PresentationTraceSources.TraceLevel=High` sui binding sospetti, o leggere l'Output) per confermare se i binding annidati falliscono: `Command="{Binding DataContext.SelectDateCommand, RelativeSource={RelativeSource AncestorType=ItemsControl, AncestorLevel=2}}"`, `Fill=...NoteColorHex...AncestorLevel=2`, `Visibility={Binding InCurrentMonth,...}`. Un `AncestorLevel` sbagliato non collassa il layout ma è un segnale.
  2. **Dare dimensioni in pixel ESPLICITE ai contenitori annidati**, non lasciare che il ScrollViewer passi misura infinita a UniformGrid/StackPanel: inner ItemsControl del mini-mese `Width=112 Height=96` (o `ItemHeight/ItemWidth` su UniformGrid); inner ItemsControl-colonna della heatmap `Height` esplicita (7×~17).
  3. In alternativa **abbandonare l'annidamento**: pre-costruire nel VM una struttura già renderizzabile — heatmap come 53 colonne ognuna `StackPanel` verticale a `Height` fissa; mini-mese come **singolo** ItemsControl con `UniformGrid` a dimensione esplicita.
  4. Confermare (test lo fanno già lato VM) che `MonthBlocks[].Cells` e `YearWeeks` siano popolati → è **puramente XAML/layout**, non dati.
- **File WIP**: `CalendarNavigatorViewModel.cs`, `CalendarMonthBlockViewModel.cs` (new), `CalendarDayCellViewModel.cs`, `HabitScreenViewModel.cs`, `Views/HabitScreen/Calendar/CalendarNavigator.xaml`, `Converters/InverseBooleanConverter.cs` (new), `App.xaml`, `Resources/Strings(.it).resx`+`Designer.cs`. Preview design (riferimento visivo concordato): `docs/calendar-preview.html`.
- **Da rivedere anche**: dimensione **Settimana** (l'utente in un giro l'ha vista ancora piccola — verificare che `MaxWidth=420` produca ~56px nella colonna reale).

---

## Aperti — bloccanti

### B11. L'abitudine costruita non riflette la configurazione nell'abitudine funzionale
- **Stato**: `verified-fixed` (2026-06-09) — registrazione multi-metrica confermata sul build (Quantità+Scala registra entrambe con unità; Resistance con serie mostra "Aggiungi serie"; "Registrazioni del giorno" elenca valori leggibili con unità). Vedi "Stato al 2026-06-09".
- **Fix (spec `2026-06-03-multi-metric-recording-design.md`, plan `2026-06-03-multi-metric-recording.md`)**: registrazione generalizzata a **multi-metrica kind-aware**. `SeriesCellViewModel` ora è consapevole del tipo (misura+unità / scala bottoni-o-slider / testo); `MetricCellFactory` risolve unità/scala da `IReferenceDataQueryService`; nuovo `MeasuredQuickAddViewModel` (1 riga di celle su tutte le metriche) **sostituisce** `CounterQuickAddViewModel`+`StateQuickAddViewModel` (host instrada sia Counter sia State qui); `ResistanceQuickAddViewModel` riusa le stesse celle (mostra unità). XAML: un solo `MetricCellTemplate` kind-aware riusato. Encoding scala preservato (indice 0-based). Cella-scala range-aware: bottoni ≤10 passi, slider >10. Suite **WPF 378 / Data 309** verdi, build 0/0. Commit `4a42118`(plan)→`MeasuredQuickAdd`.
- **Resta Opzione 2 (follow-up)**: Dettaglio/grafici/heatmap/cell-marker restano su metrica primaria (visualizzazione multi-metrica non ancora). Creazione scale custom = R-builderCustomScale.
- **Sintomo**: si costruisce un'abitudine col builder (es. Quantità + Scala, oppure Conteggio + serie), si Lancia, l'abitudine appare in Home ma **la registrazione mostra solo una parte** della configurazione: con Quantità+Scala compare solo "quantità" (senza l'unità scelta), la scala sparisce; in una prova precedente compariva solo "Serie".
- **Root cause**: lo strato di **registrazione (QuickAdd, M3.5) è mono-metrica per tipo canonico**. `CounterQuickAddViewModel.LoadMetricsAsync` prende `metrics.OrderBy(SortOrder).FirstOrDefault()` (la prima metrica) e salva un singolo `ValueInteger` senza mostrare l'unità. State mostra una sola scala, ecc. Il **builder (Spec 1) è più espressivo**: produce Counter multi-metrica (Quantità + Scala risolve a Counter con 2 `MetricDefinition`), che la `CounterQuickAddViewModel` scarta oltre la prima. I dati SONO persistiti correttamente (`ComposedHabitService` aggiunge tutte le metriche) — è la UI di registrazione a non renderizzarle.
- **Tensione di design**: i guardrail della grammatica (`HabitCompositionRules`) impediscono combinazioni *illegali* ma permettono combinazioni *semanticamente fuse* (Quantità+Scala→Counter) che nessuna QuickAdd canonica sa mostrare. Inoltre nessuna QuickAdd mostra l'**unità** della metrica.
- **Opzioni (da decidere in brainstorming)**:
  - (i) Rendere le QuickAdd **multi-metrica** (Counter mostra N righe input, una per metrica, con la propria unità; ecc.) — modifica significativa allo strato M3.5.
  - (ii) **Restringere la grammatica** del builder così da produrre solo composizioni che una QuickAdd canonica sa rendere (es. un solo campo misurato + serie opzionale) — più piccolo, ma limita l'espressività del builder (contraddice la visione "componi liberamente").
  - (iii) Ibrido: la QuickAdd del Counter mostra l'unità della metrica primaria (fix minimo per il caso 1-metrica) + guardrail che impediscono i mix problematici.
- **Priorità**: alta — un builder che genera abitudini non pienamente registrabili è peggio di nessun builder. Bloccante per dichiarare Spec 1 "verified".

### B1. Save note non funzionante
- **Stato**: `fixed` (2026-06-05, commit `64dfa31`) — **verifica visiva utente: ✓** (editing nota esistente ora funziona, niente più lampeggio).
- **Root cause (confermata via instrumentation del bridge)**: feedback-loop infinito (~93k change events) modificando una nota **esistente**. Toast UI `setMarkdown()` emette **più** change events sincroni; il flag one-shot `suppressNext` ne sopprimeva solo uno, il secondo rientrava dal two-way binding, e poiché `getMarkdown()` normalizza il markdown i guard di idempotenza non convergevano mai → loop ("lampeggia", CPU al massimo, Save inutilizzabile). NB: NON era un problema di Save/IsDirty (`canSave=True` per tutto il loop), né l'ipotesi originale (a)/(c). Le note **nuove** (testo plain) non innescavano il loop perché round-trippano senza normalizzazione.
- **Fix**: sopprimere TUTTI i change emessi durante `setMarkdown()` con un flag a scope sincrono (`applyingRemote`) invece del one-shot. + guard su `TryInitAsync` (era doppio-sottoscritto `WebMessageReceived`: Loaded + OnAvailabilityChanged in race prima dell'init → ogni messaggio gestito due volte). Strumentazione temporanea rimossa post-conferma.
- **Storico ipotesi (per memoria)**: il sintomo originale 2026-05-29 era conflato con R1 theming + falso negativo WebView2; il vero bug del save era latente e si manifestava solo sull'editing di note con markdown reale.

<details><summary>Sintomo originale (storico)</summary>

- **Sintomo**: dopo modifica di una nota nella tab Notes (o nel Journal QuickAdd), il bottone Save non persiste o resta disabilitato.
- **Ipotesi root cause**:
  - (a) Toast UI WYSIWYG non emette `'change'` → `MarkdownProperty` (TwoWay) non si aggiorna → `EditorMarkdown` resta uguale a `BodyMd` → `IsDirty` = false → Save disabilitato.
  - (b) Save abilitato ma `INoteService.UpdateAsync` non commita (improbabile, coperto da test).
  - (c) WebView2 non disponibile → fallback `TextBox`; il binding `TextBox.Text → Markdown` DP via `SetCurrentValue` propaga a source TwoWay? Da verificare.
- **Info da raccogliere prima del fix**:
  - Il bottone Save è cliccabile (Enabled) quando digiti, o resta grigio?
  - Hai WebView2 Runtime installato? Banner "WebView2 not available" visibile sopra HabitScreen?
  - Riavviando l'app la nota persiste o sparisce?

</details>

### B2. Costumizzazione note "non funziona particolarmente bene"
- **Stato**: `in-progress` (sotto-bucket "tema editor", 2026-06-23, working tree non committato)
- **Editor theming (working tree)**: l'editor Toast UI ora segue il tema app. `editor.html` ha override CSS completi (chrome/toolbar/superficie/source-mode/mode-switch/popup) su variabili `--rec-*`; `editor.js` ha un handler `setTheme`; `MarkdownEditorControl.PushThemeToEditor` invia le brush sul `ready` **e** — da oggi — sul **cambio tema a caldo** (tripwire `SetResourceReference` su `Brush.Surface`). Anteprima browser fedele in `docs/editor-theme-preview.html` (Toast UI reale + toggle dark/light). Build WPF verde (`EnableWindowsTargeting`). **Da verificare a schermo** (Mac via preview, o app sulla VM).
- **Sintomo**: vago — formattazione markdown nell'editor non si comporta come atteso.
- **Info da raccogliere**: screenshot di un caso specifico (es. "scrivo `**bold**`, vedo `**bold**` invece di **bold**"). Questa è la differenza fra mode WYSIWYG e mode Markdown source.
- **Possibili sotto-bucket**:
  - Toolbar Toast UI ridotta non ha `table`/`task`/`image`/`source-view` toggle.
  - Tema dell'editor: Toast UI usa il suo CSS, non rispetta il dark/light di Recordum. → **affrontato** (vedi "Editor theming" sopra; verifica visiva pendente).
  - Preview/edit toggle nel WYSIWYG.

### B3. "Tante altre cose non funzionano"
- **Stato**: `reported` (2026-05-29 utente)
- **Sintomo**: generico. Da triagiare dettaglio per dettaglio (2026-05-30+).
- **Plan**: per ogni nuovo bug specifico, aprire una nuova voce B<N> qui sotto con sintomo + ipotesi + info da raccogliere.

---

## Aperti — rifinitura

### R1. Theming Pass 2 — chrome flat per Button/TabItem/ComboBox
- **Stato**: `fixed` (Theming Pass 1→3, vedi CLAUDE.md Fase 4; tracker allineato 2026-06-11) — Button/TabItem/ComboBox hanno ControlTemplate flat in `BaseStyles.xaml` (Border + ContentPresenter, hover/focus all'accent).
- **Scope (storico)**: BaseStyles Pass 1 (commit `6a15cea`) cabla solo Foreground; il `ControlTemplate` Aero di Button/TabItem/ComboBox usa un gradiente chiaro hardcoded. In dark mode il testo è leggibile ma lo sfondo bottone resta light-gradient = ugly.

### R2. WebView2 fallback UX
- **Stato**: `fixed` (tracker allineato 2026-06-11) — banner di guida con Hyperlink al download del Runtime già presente sopra `HabitScreen` (`HabitScreen.xaml`, `Editor_WebView2_NotAvailable_*`), visibile quando `Availability.IsAvailable=false`.
- **Scope (storico)**: se WebView2 Runtime non è installato, l'editor cade su `TextBox` senza preview markdown. Aggiungere banner di guida con link diretto al Runtime download.

### R3. Toast UI toolbar — opzioni mancanti
- **Stato**: `fixed` (2026-06-09, commit `ce155fe`) — aggiunti `task`/`table`/`codeblock`; source-view già nativo (mode-switch). `image` escluso (no `addImageBlobHook`). Vedi "Blocco limature 2026-06-09 (sera)".

### R4. BackupSchedulerHost — start delay configurabile
- **Stato**: `fixed` (tracker allineato 2026-06-11) — `BackupSchedulerHost` legge `BackupSchedulerOptions` iniettabile (`InitialDelay`/`Interval`); i default sono già reattivi (InitialDelay 2 min, Interval 30 min, "Defaults favour responsiveness"). Configurabile via DI in `ServiceRegistrar`.
- **Scope (storico)**: il fix di M3.10b era "wait one tick = 60min prima del primo run". Per polish: rendere il delay configurabile (es. 5min default per esperienza utente più reattiva).

### R5. Accessibility / keyboard navigation
- **Stato**: `fixed` parziale (2026-06-09, commit `c9442e4`) — color/icon picker resi keyboard-accessible (focus/Invio-Spazio/Escape + AutomationProperties.Name). **Resta aperto** l'audit app-wide: focus order, tab traversal su tutte le view, AutomationPeers, contrasto colori WCAG, nav-tastiera dei controlli custom. Effort separato (candidato milestone/brainstorm dedicato).

### R6. CLAUDE.md ridotto — strategia di consultazione dei file companion
- **Stato**: `fixed` (questo commit) — `CLAUDE.md` rimosso le sezioni storiche, ora pointer a `docs/MILESTONES.md`. Tracker bug in questo file.

---

## Risolti

### B10. Configura — le sezioni type-specific non compaiono al cambio tipo
- **Stato**: `fixed` (2026-06-01, su `fase-4-polish`)
- **Sintomo (report "Recordum problemi 2")**: creata un'abitudine vuota e aperta Configura, "nulla si inserisce affinché l'abitudine possa essere personalizzata" — niente unità/quantità/cronometro.
- **Root cause**: un'abitudine vuota nasce `Action`; le sezioni Metrics/Scales/Timer sono type-gated (Counter/Resistance/State). La visibilità (`IsVisible = IsApplicable(...)`) era calcolata **solo in `HabitEditorViewModel.LoadAsync`**. `OnSelectedNodeTypeChanged`/`OnSelectedHabitTypeChanged` persistevano il tipo ma **non ricalcolavano** la visibilità → cambiando il tipo a Counter le sezioni non comparivano mai senza riaprire l'editor.
- **Fix**: estratto `RefreshSectionVisibility()`, chiamato in `LoadAsync` + nei due handler di cambio tipo. Test rosso→verde `SectionVisibilityReactivityTests` (Action→Counter rivela Metrics/Timer; Habit→Group nasconde le sezioni habit-only). Suite WPF 337 verdi.
- **NB**: il persist delle sezioni era già corretto (`ApplyAsync`→`IHabitConfigService.UpsertAsync`); il difetto era solo la reattività della visibilità. Restano separati: creazione habit vuota senza prompt + apertura automatica della Cronologia (vedi B3/nuove voci).

### B0. Testo nero su nero in tema scuro — Pass 1
- **Stato**: `fixed` (2026-05-29, commit `6a15cea` su `fase-4-polish`)
- **Verifica utente**: in attesa (Pass 2 chrome flat è R1).
- **Root cause**: `App.xaml` non aveva implicit `Style` per i control type principali → Foreground default = nero anche in dark.
- **Fix**: `Resources/BaseStyles.xaml` con ~20 implicit `Style` cablati ai brush di tema via `DynamicResource`.

### B-prev. Crash tab Dettaglio al click (e poi al launch)
- **Stato**: `verified-fixed` (2026-05-29, commit `abb9e4c` su `fase-4-polish`)
- **Verifica utente**: ✓ confermato — "Dettagli ora va".
- **Root cause**: `CommandParameter="Days30"` string passato a `IAsyncRelayCommand<TrendRange>`. `RelayCommand<T>` throws `InvalidCastException` durante CanExecute check al template materialization.
- **Fix**: `CommandParameter="{x:Static vmd:TrendRange.Days30}"` (et al.) in `HabitScreen.xaml`.
