# Direzione visiva, onda 3a — Il creatore nei cinque blocchi, il selettore dei modelli con la traduzione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** il creatore della spec §6.4 nei cinque blocchi di Configura (con l'anteprima a destra, Annulla · Salva come
modello · Crea abitudine) e il selettore dei modelli della §7.2 (tessere, schede con ricerca, dettaglio, "Usa questo
modello" e "Personalizza…"), con i modelli ufficiali tradotti nella lingua dell'interfaccia alla creazione (UX-11). Chiude
UX-12. Senza perdere una funzione.

**Architecture:** una sola strada per creare da un modello: il **creatore** (`HabitBuilderViewModel`) impara a caricarsi da
un modello (`LoadTemplateAsync`) e crea con `CreateComposedHabitAsync`, come oggi da zero. "Usa questo modello" lo fa senza
mostrarlo, "Personalizza…" lo apre già riempito: così la parità modello ↔ creatore (principio 8, `BuilderTemplateParityTests`)
è anche la garanzia che le due strade creino la stessa abitudine, e la traduzione sta in un punto solo. La traduzione vive in
Presentation (`OfficialTexts`: chiavi `.resx` per i testi seminati dei modelli, delle scale e delle unità ufficiali), non
tocca i dati seminati e si applica **alla creazione**: l'abitudine nasce coi testi tradotti e da lì è dell'utente.
`IHabitTemplateService.InstantiateAsync` resta com'è per WPF.

**Tech Stack:** .NET 10, Avalonia 12.1.2, CommunityToolkit.Mvvm, xunit v3 + Avalonia.Headless (UI), xunit (Presentation,
Data).

**Spec:** `docs/superpowers/specs/2026-10-28-ux-direction-design.md` — §6.4 (il creatore), §7.2, §9 (onda 3), §10,
§12 punti 3, 7, 14, Appendice A ("Creare"). Mockup: `docs/design/2026-10-28-ux-mockups/builder.html`, `modelli.html`,
`integrazioni.html` parte 3, `correzioni.html` parte 3 (**la spec vince**: "come si registra" sta dentro "Cosa registri").
Tracker: UX-11, UX-12. Modello di forma: il piano 2b (`docs/superpowers/plans/2026-10-30-ux-wave2b-group-configure.md`).

**Fuori da 3a (piano 3b):** Impostazioni a categorie (§7.3), finestre del backup (§7.4), diario a tutta finestra e a schermo
intero (§7.5).

## Global Constraints

- Branch: `ux3a-creator-templates`, da `main` a `8e9af6f9` (2a e 2b in `main`).
- Valgono i Global Constraints di 2a e 2b: scala 12/13/14/16/20, `Brush.*`, `Opacity.*`, `HabitTextBrushConverter` per i
  valori nel colore; colore dell'abitudine **solo** per il comando che registra; inchiostro (`Button.primary`) per **una**
  azione principale per finestra; contorno per le secondarie; `Button.ghost` per "annulla"; icone Lucide, nessun glifo.
- **Ordine unico dei blocchi** (spec §6.4), nel creatore come in Configura, con gli stessi titoli (`Editor.Block.Identity`,
  `.Record`, `.When`, `.Home`, `.Advanced`, già in `Strings.resx`): 1 Chi è (nome 20/600, icona, colore; **niente**
  descrizione né colore del segno: quelli sono solo di Configura) · 2 Cosa registri (campi, scale, unità nuove, nomi delle
  serie; poi come si registra: gesto principale, più volte al giorno, "un tocco = +1", "è un'astinenza", sessione a tempo) ·
  3 Quando conta (programma, serie con modalità e recupero, obiettivo con nome e periodo; **niente** inizio della giornata:
  solo Configura) · 4 Come appare in Home = la colonna d'anteprima a destra · 5 Avanzate, chiuso (tolleranza della serie,
  obiettivo su giorni mobili, segmenti del timer, riposo, richiesta automatica, condivisione con l'AI "in arrivo").
- **Traduzione dei modelli ufficiali** (spec §7.2, §12.14): per Id stabile (`00000003-0000-0000-0000-0000000000NN`, NN
  01…08), **mai** per nome; copre nome, descrizione e tutto il testo che l'utente vede nel contenuto (nomi delle metriche,
  nomi delle serie, segmenti, etichette delle scale seminate, nomi delle unità); si fissa alla creazione nella lingua del
  momento; i modelli dell'utente restano come li ha scritti; nessuna migrazione, i dati seminati non si toccano.
- WPF non si tocca e deve compilare. Comportamenti condivisi cambiati **solo** qui: (a) Task 1 — un campo che usa una scala
  ufficiale nasce con le etichette nella lingua dell'interfaccia (anche dal builder WPF); (b) Task 3 — `IHabitBuilderLauncher`
  prende un metodo nuovo, che `WpfHabitBuilderLauncher` implementa nel modo più piccolo; (c) le stringhe esistenti cambiate
  dal Task 5 ("Lancia" → "Crea abitudine", "Colore (hex)" → "Colore") cambiano anche in WPF. Ogni altro cambio in
  Presentation è additivo.
- Ogni stringa nuova in `Strings.resx` + `Strings.it.resx` + accessor in `Strings.Designer.cs` (**solo via shell**). Test con
  etichette: `TestCulture.Pin()`; test che controllano una traduzione fissano **entrambe** le culture (`en-US` e `it-IT`).
- Avalonia: `InitializeComponent()`, `x:DataType` ovunque, niente `ContentControl` con `ContentTemplate` statico;
  `[AvaloniaFact]` per i test con tipi Avalonia.
- Un parametro opzionale nuovo con default null va fornito dalla DI di **entrambi** gli host e asserito in
  `CoreCompositionTests` (null compila e spegne la funzione in silenzio).
- Build con `--no-incremental`; mai `-p:UseAppHost=false`; `TreatWarningsAsErrors`. **Un test di regressione si prova rosso.**
- Mai `git stash` (c'è uno stash dell'autore).

## Review Focus

1. **Doppio clic su "Usa questo modello"** (o Invio ripetuto): crea **una** abitudine. `AsyncRelayCommand.ExecuteAsync` non
   consulta `CanExecute`: la guardia sta dentro il metodo, come `PickAsync`. Test nel Task 6.
2. **Un modello dell'utente che si chiama come un ufficiale**, o salvato da un ufficiale tradotto ("Diario quotidiano"):
   resta come l'ha scritto, in qualunque lingua. La traduzione passa solo dall'Id. Test nel Task 1.
3. **Un testo seminato senza traduzione** (un modello ufficiale aggiunto domani): esce il testo seminato, mai una stringa vuota
   né un'eccezione; e un test elenca **tutti** i testi dei payload seminati e fallisce se uno non ha la sua chiave. Task 1.
4. **"Personalizza…" e poi Annulla** (o la finestra chiusa): nessuna abitudine creata, nessun modello salvato. Task 3.
5. **Cambiare lingua dopo aver creato**: l'abitudine creata in italiano resta in italiano in inglese (i testi sono suoi); un
   campo con scala ufficiale creato prima di quest'onda tiene le etichette inglesi (nessun riempimento a ritroso). Task 1.

---

### Task 1: I testi dei modelli, delle scale e delle unità ufficiali

**Files:**
- Create: `src/Recordum.Presentation/Templates/OfficialTexts.cs`
- Modify: `src/Recordum.Presentation/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs` (shell)
- Modify: `src/Recordum.Presentation/ViewModels/HabitBuilder/HabitBuilderViewModel.cs` (`BuildRequest`: etichette delle scale
  ufficiali)
- Create: `src/Recordum.UI/Converters/OfficialNameConverter.cs`; Modify: i `ComboBox` di unità e scala del creatore
  (`HabitBuilderView.axaml`) e di Configura (`MetricsSectionView.axaml`, `ScalesSectionView.axaml`)
- Test: `tests/Recordum.Presentation.Tests/Templates/OfficialTextsTests.cs`

**Interfaces:**
- Produces (`Recordum.Presentation.Templates`):
  - `static class OfficialTexts`
  - `static bool IsOfficialTemplate(Guid templateId)` — vero per i soli 8 Id di `HabitTemplateSeed.All`
  - `static string TemplateName(HabitTemplate t)` · `static string? TemplateDescription(HabitTemplate t)` — tradotti se
    ufficiale, altrimenti `t.Name` / `t.Description`
  - `static string TemplateText(Guid templateId, string seeded)` — il testo del contenuto (nome del nodo, di una metrica, di
    una serie, di un segmento) tradotto se il modello è ufficiale e il testo è noto; altrimenti `seeded`
  - `static string ScaleName(ScaleTemplate s)` · `static IReadOnlyList<string>? ScaleLabels(ScaleTemplate s)` — per le scale
    `IsOfficial`, da `.resx`; per le altre `Name` e null
  - `static string UnitName(UnitOfMeasure u)` — per le unità `IsOfficial`, da `.resx`; per le altre `Name`
- Chiavi `.resx` (inglese = il testo seminato, italiano nuovo): `Template.Official.NN.Name`, `Template.Official.NN.Description`
  (NN 01…08); `Template.Text.<Slug>` per ogni testo del contenuto (slug PascalCase del testo inglese: `HoursSlept`,
  `SleepQuality`, `Night`, `Nap`, `Work`, `Rest`, `Duration`, `Reps`, `Weight`, `Amount`, `Mood`, più i nomi dei nodi);
  `Scale.<code>.Name` e `Scale.<code>.Labels` (etichette separate da `|`) per le scale di `ScaleTemplateSeed`; `Unit.<code>.Name`
  per le unità di `UnitOfMeasureSeed`. La mappa testo seminato → accessor è un dizionario statico in `OfficialTexts`
  (`StringComparer.Ordinal`).

- [ ] **Step 1: test rossi** (`OfficialTextsTests`, culture fissate per test):
  - `Every_seeded_template_text_has_a_translation` — per ogni `HabitTemplateSeed.All`: nome, descrizione e ogni testo del
    payload (nome del nodo, `metrics[].name`, `config.series_labels[]`, `segments[].name`) hanno la chiave; in `it-IT` il
    risultato è diverso dal seminato per almeno nome e descrizione.
  - `Every_official_scale_and_unit_has_a_name` e `Official_scale_labels_match_the_seeded_count` (il numero di etichette
    tradotte = quello di `LabelsJson` seminato, per ogni scala).
  - `Templates_translate_by_id_not_by_name` — un `HabitTemplate` `IsOfficial=false` chiamato "Daily journal" esce "Daily
    journal" in `it-IT`; l'ufficiale `…0003` esce col nome italiano.
  - `An_unknown_text_falls_back_to_the_seeded_one` — `TemplateText(…0003, "Qualcosa")` = "Qualcosa"; `TemplateText` su un Id
    non ufficiale torna sempre il testo dato.
  - `A_field_on_an_official_scale_is_born_with_translated_labels` (nel file dei test del builder): in `it-IT` un campo Scala
    su `mood-5` senza override → la `MetricDefinition` della richiesta ha `LabelsJson` con le etichette italiane; una scala
    dell'utente → `LabelsJson` null come oggi; in `en-US` le inglesi.
- [ ] **Step 2: implementa** `OfficialTexts` e le chiavi; in `BuildRequest` il campo Scala senza override su una scala
  `IsOfficial` riceve `LabelsJson = JsonSerializer.Serialize(OfficialTexts.ScaleLabels(scala))`.
- [ ] **Step 3: i nomi nei menu.** `OfficialNameConverter` (UI) chiama `UnitName`/`ScaleName`; i `ComboBox` di unità e scala
  di creatore e Configura lo usano nell'`ItemTemplate`. Test UI: `Official_units_and_scales_show_in_the_interface_language`
  (`it-IT`: il menu delle unità mostra "Litro", non "Litre").
- [ ] **Step 4: verdi, commit** `feat(presentation): i testi dei modelli, delle scale e delle unita' ufficiali nella lingua dell'interfaccia (UX-11)`.

### Task 2: I mattoni che mancano al creatore

Il creatore oggi non ha obiettivo con nome e periodo, tolleranza, giorni mobili, riposo, richiesta automatica: la spec li
mette nei blocchi 3 e 5. Per il principio 8 ognuno va **anche** nel modello (payload) e torna indietro.

**Files:**
- Modify: `GoalBrickViewModel.cs`, `ScheduleBrickViewModel.cs`, `SessionBrickViewModel.cs`, `HabitBuilderViewModel.cs`
  (`BuildRequest`, `BuildTemplatePayloadJson`, classi del payload)
- Modify: `src/Recordum.Data/Services/HabitTemplateService.cs` (lettura dei campi nuovi del payload, additiva)
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitBuilder/BuilderTemplateParityTests.cs`, i test dei mattoni;
  `tests/Recordum.Data.Tests/…/HabitTemplateServiceTests` (il file che già prova `InstantiateAsync`)

**Interfaces:**
- Produces: `GoalBrickViewModel.Name` (`string?`), `.PeriodType` (`GoalPeriodType`, default `Open` come oggi), `.RollingDays`
  (`int?`); `ScheduleBrickViewModel.StreakToleranceMinutes` (`int?`); `SessionBrickViewModel.RestTimerSeconds` (`int?`),
  `.TimerAutoPrompt` (`bool`). Payload (snake_case come il resto): `goal.name`, `goal.period_type` (esiste), `goal.rolling_days`,
  `config.streak_tolerance_minutes`, `config.rest_timer_seconds`, `config.timer_auto_prompt`. Campi assenti = il default di oggi.
- Regole: `RollingDays` richiesto e > 0 quando `PeriodType = Rolling` (altrimenti `CanLaunch` è falso, come il
  `GoalSectionViewModel` di Configura con `Editor.Error.RollingDaysPositive`); un periodo `Fixed` chiede inizio e fine come in
  Configura — **se** il creatore non li ha, `Fixed` non si offre nel creatore (decisione: il creatore offre `Open` e `Rolling`;
  `Fixed` resta in Configura).

- [ ] **Step 1: test rossi:** `A_composition_using_every_brick_survives_the_payload_round_trip` esteso ai campi nuovi;
  `Rolling_goal_needs_its_days` (CanLaunch falso con `Rolling` e giorni null o 0); in Data
  `Instantiate_reads_goal_name_rolling_days_tolerance_rest_and_auto_prompt`; il "parametro muto" (CLAUDE.md §7): ogni campo
  nuovo provato con un valore diverso dal default.
- [ ] **Step 2: implementa**; `HabitTemplateService` legge i campi nuovi (assenti = come oggi). Nessuna migrazione.
- [ ] **Step 3: verdi (Presentation + Data), commit** `feat(presentation): il creatore ha obiettivo con nome e periodo, tolleranza, riposo e richiesta automatica, anche nei modelli`.

### Task 3: Il creatore si carica da un modello

**Files:**
- Modify: `HabitBuilderViewModel.cs`; `IHabitBuilderLauncher.cs`; `src/Recordum.UI/Services/AvaloniaHabitBuilderLauncher.cs`;
  `src/Recordum.WPF/Views/WpfHabitBuilderLauncher.cs` (il minimo per compilare e funzionare)
- Test: `BuilderTemplateParityTests.cs`; `tests/Recordum.UI.Tests/HabitBuilder/…` (il launcher)

**Interfaces:**
- Consumes: Task 1 (`OfficialTexts.TemplateText`, `TemplateDescription`, `ScaleLabels`), Task 2 (i campi nuovi).
- Produces:
  - `Task HabitBuilderViewModel.LoadTemplateAsync(Guid templateId, CancellationToken ct = default)` — dopo `InitializeAsync`;
    legge il modello da `IHabitTemplateService.GetAsync`, riempie **tutti** i mattoni dal payload (l'inverso di
    `BuildTemplatePayloadJson`: nome, colore, icona, tipo, ripetibile e serie, gesto, campi con unità e scale risolte per
    codice, programma, serie, obiettivo, sessione e segmenti), coi testi passati da `OfficialTexts.TemplateText` se il modello
    è ufficiale; un'unità o una scala che il codice non trova lascia il campo incompleto (il `CanLaunch` di oggi lo blocca),
    mai un'eccezione.
  - `string? HabitBuilderViewModel.Description` — riempita dal modello (tradotta se ufficiale) e scritta su `HabitNode.Description`
    in `BuildRequest`; il creatore non la mostra (è di Configura).
  - `Task<Guid?> IHabitBuilderLauncher.LaunchFromTemplateAsync(Guid templateId, Guid? parentId = null, CancellationToken ct = default)`
    — apre il creatore già riempito; torna l'id creato o null.

- [ ] **Step 1: test rossi:**
  - `Every_official_template_loads_and_composes_like_its_fixture` (theory sugli 8 Id): `LoadTemplateAsync` → `BuildRequest`
    equivale alla richiesta della fixture già in `BuilderTemplateParityTests` (stessi tipi, campi, unità, scale, programma,
    serie, obiettivo, segmenti, gesto), con i testi in `en-US`.
  - `An_official_template_loads_in_the_interface_language` (`it-IT`): nome, descrizione, metriche, serie, segmenti italiani;
    le etichette della scala nella `LabelsJson` della metrica.
  - `A_user_template_loads_as_written` (`it-IT`, un modello dell'utente con testi inglesi: restano inglesi).
  - `Loading_then_saving_as_template_round_trips` (payload ricaricato = payload salvato, per un modello dell'utente).
  - UI: `Customize_then_cancel_creates_nothing` — `LaunchFromTemplateAsync`, Annulla: nessun nodo nuovo, nessun modello nuovo.
- [ ] **Step 2: implementa**; il launcher Avalonia apre la stessa finestra del creatore di oggi e chiama `LoadTemplateAsync`
  prima di mostrarla.
- [ ] **Step 3: verdi, commit** `feat(presentation): il creatore si carica da un modello, ufficiale tradotto o dell'utente com'e' scritto`.

### Task 4: Le etichette di una scala come elenco di campi

Spec §6.4 e §12.7: le etichette non si scrivono più come array JSON (Configura, `ScalesSectionView`) né separate da virgole
(creatore, "Nuova scala", `NewScaleFormViewModel.LabelsText`).

**Files:**
- Create: `src/Recordum.Presentation/ViewModels/HabitEditor/ScaleLabelsEditorViewModel.cs`;
  `src/Recordum.UI/Controls/ScaleLabelsEditor.axaml(.cs)`
- Modify: `ScalesSectionViewModel.cs` + `ScalesSectionView.axaml`; `StagedReferenceData.cs` (`NewScaleFormViewModel`) +
  il form "Nuova scala" in `HabitBuilderView.axaml`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitEditor/ScaleLabelsEditorViewModelTests.cs`; i test di
  `ScalesSectionViewModel`; un test UI della vista

**Interfaces:**
- Produces: `ScaleLabelsEditorViewModel` con `ObservableCollection<ScaleLabelRowViewModel> Rows` (`int Value`, `string Text`),
  `IReadOnlyList<string> Labels`, `event EventHandler? LabelsChanged`, e `bool FixedCount` — **vero** per una scala che esiste
  (le righe sono `MaxValue − MinValue + 1`, una per valore, col valore a sinistra; niente aggiungi/togli), **falso** per una
  scala nuova (righe da 2 in su, "+ aggiungi etichetta", × per togliere; il valore va da 1 a n).
- In Configura il salvataggio resta campo per campo: `LabelsChanged` scrive la `LabelsJson` della metrica con
  `ApplyAsync` (come oggi); etichette tutte vuote = `LabelsJson` null (vince quella del modello di scala, CLAUDE.md §5).
  **Trappola** (CLAUDE.md §8): ricostruire `Rows` al caricamento **sospende** il salvataggio e non scrive niente.

- [ ] **Step 1: test rossi:** `Existing_scale_has_one_row_per_value`; `New_scale_adds_and_removes_rows_down_to_two`;
  `Editing_a_label_saves_the_metric_labels` (Configura); `Loading_does_not_write` (nessuna `UpdateAsync` al caricamento:
  provarlo rosso togliendo la sospensione); `All_empty_labels_clear_the_override`.
- [ ] **Step 2: implementa** VM e controllo (TextBox con cornice piena, valore in `Brush.TextMuted` a sinistra); il campo JSON
  di Configura e il campo a virgole del creatore escono.
- [ ] **Step 3: verdi, commit** `feat(ui): le etichette di una scala come elenco di campi, in Configura e nel creatore`.

### Task 5: Il creatore nei cinque blocchi

**Files:**
- Modify: `src/Recordum.UI/Views/HabitBuilder/HabitBuilderView.axaml(.cs)`, `BuilderPreviewPane.axaml`;
  `HabitBuilderViewModel.cs` (solo membri d'aiuto per la vista: `DerivedTypeLine`, `FieldTileHint(FieldNature)`)
- Modify: stringhe (`HabitBuilder.Launch` → "Create habit" / "Crea abitudine"; `HabitBuilder.Color` → "Color" / "Colore";
  le righe nuove sotto)
- Test: `tests/Recordum.UI.Tests/HabitBuilder/BuilderBlocksTests.cs` (nuovo), `BuilderScreenshotTests.cs`

**Interfaces:**
- Consumes: Task 2 (i mattoni nuovi), Task 4 (`ScaleLabelsEditor` nel form "Nuova scala"), `ColorSwatchPicker` e `IconPicker`
  come oggi, i titoli `Editor.Block.*`, `ConfigRow` di 2b **non** si usa (il creatore mostra i campi aperti, non riassunti).
- Stringhe nuove (inglese / italiano):
  - `HabitBuilder.DerivedType.Line` "Behaves like a **{0}** · changes by itself with what you record" / "Si comporta come
    **{0}** · cambia da solo con quello che registri" ({0} = la parola del tipo già usata in Home, minuscola).
  - Una riga per tessera: `HabitBuilder.Tile.Quantity.Hint` "A number with a unit: litres, kilometres, pages" / "Un numero con
    l'unità: litri, chilometri, pagine"; `.Time.Hint` "A duration: how long it took" / "Una durata: quanto è durato";
    `.Scale.Hint` "A value on a labelled scale: mood, pain" / "Un valore su una scala con le etichette: umore, dolore";
    `.Text.Hint` "Free text: a note, a journal entry" / "Testo libero: una nota, una voce di diario".
  - Quando `CanAddField(nature)` è falso la tessera è spenta e la riga dice perché: `HabitBuilder.Tile.Disabled.Counter`
    "Not with a tap counter: a tap is already the record" / "Non con il contatore a tocco: il tocco è già la registrazione"
    (e le altre regole di `HabitCompositionRules.CanAddField`, una stringa per regola).
  - `HabitBuilder.AddGoal` "+ Goal" / "+ Obiettivo", `HabitBuilder.AddSession` "+ Timed session" / "+ Sessione a tempo".
  - `HabitBuilder.TemplateSaved` diventa "Saved ✓" / "Salvato ✓" (il ✓ è testo nella stringa, non un'icona: è la spec).
  - `Editor.Advanced.AiSharing.Soon` riusata da Configura per "in arrivo".
- Fuori: "Colore (hex)", la sezione vuota "Dati d'esempio" (`HabitBuilder.SampleData*`: la chiave resta se WPF la usa,
  altrimenti esce), la tendina senza etichetta della Quantità (UX-12: le si dà il suo `caption`).

- [ ] **Step 1: test rossi** (`BuilderBlocksTests`): `Blocks_follow_the_spec_order` (i cinque titoli dall'alto in basso: Chi è,
  Cosa registri, Quando conta, Avanzate; "Come appare in Home" è la colonna destra); `Advanced_is_closed_and_holds_tolerance_rolling_segments_rest_prompt_ai`;
  `Field_tile_disabled_says_why` (con il contatore a tocco: Quantità spenta, la riga è quella della regola);
  `Footer_is_cancel_save_create` (Annulla `Button.ghost`, Salva come modello contorno, Crea abitudine `Button.primary`, uno
  solo); `No_hex_field_no_sample_data` (nessun `TextBox` con un esadecimale, nessun testo "Dati d'esempio");
  `Derived_type_line_follows_the_composition` (aggiungere un campo Testo cambia la parola).
- [ ] **Step 2: implementa** la vista: scorrimento verticale a sinistra coi cinque blocchi, anteprima fissa a destra (come oggi);
  a < 720 px l'anteprima va sotto i blocchi. Ogni casella con la riga che spiega (`caption`, 12, `Brush.TextMuted`).
- [ ] **Step 3: PNG** `builder-blocks-{light,dark}.png`, `builder-advanced-{light,dark}.png`, `builder-narrow-{light,dark}.png`
  (600 px) con un'asserzione strutturale ciascuno (ordine dei titoli; Avanzate aperta mostra i suoi campi; a 600 px nessuna
  sovrapposizione: bordi destri ≤ 600). **Guardarli**; ciò che si vede storto diventa un test.
- [ ] **Step 4: verdi (UI + Presentation), commit** `feat(ui): il creatore nei cinque blocchi; "Crea abitudine"; fuori l'esadecimale e i dati d'esempio (UX-12)`.

### Task 6: Il selettore dei modelli

**Files:**
- Modify: `src/Recordum.Presentation/ViewModels/HabitEditor/HabitTemplatePickerViewModel.cs` (membri **aggiunti**: WPF usa
  `Actions`, `OfficialTemplates`, `CustomOptions`, `PickAsync` com'è); `PickerOption` (campi aggiunti con default)
- Modify: `src/Recordum.UI/Views/HabitTemplatePickerWindow.axaml(.cs)`; `AvaloniaHabitTemplatePickerLauncher.cs`
- Modify: `RecordumServiceCollectionExtensions.cs` (se manca, `Func<HabitBuilderViewModel>`); `CoreCompositionTests`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HabitEditor/HabitTemplatePickerViewModelTests.cs`;
  `tests/Recordum.UI.Tests/HabitTemplatePickerWindowTests.cs` (o il file che già prova la finestra)

**Interfaces:**
- Consumes: Task 1 (`TemplateName`, `TemplateDescription`), Task 3 (`LoadTemplateAsync`, `LaunchFromTemplateAsync`),
  `SamplePreviewFactory`/`BuilderPreviewModel.HomeRow` per "come apparirà in Home".
- Produces (su `HabitTemplatePickerViewModel`, con un parametro di costruttore opzionale `Func<HabitBuilderViewModel>? builders = null`
  fornito dalla DI):
  - `string SearchText` (two-way) e `ObservableCollection<PickerOption> VisibleOfficial` / `VisibleCustom` — filtrano per nome e
    descrizione **tradotti**, senza maiuscole e senza accenti (`CompareInfo.IndexOf` con `IgnoreCase | IgnoreNonSpace`); a
    risultato vuoto `bool NoResults`.
  - `PickerOption? Selected` e `TemplateDetailViewModel? Detail`: `HomeRow` (l'anteprima della riga), `TypeLine` ("diario · più
    volte al giorno": parola del tipo + `Registrazione` come nel riassunto di Configura), `Contents` (righe: cosa registri —
    i campi con unità o scala; quante volte; gesto principale).
  - `IAsyncRelayCommand UseSelectedCommand` — crea con un `HabitBuilderViewModel` nuovo: `InitializeAsync`, `LoadTemplateAsync`,
    `LaunchCommand`; scrive `CreatedHabitNodeId`. Guardia contro il doppio clic **dentro** il metodo.
  - `IRelayCommand CustomizeSelectedCommand` — scrive `Guid? CustomizeTemplateRequested = Selected.TemplateId` e chiude; il
    launcher Avalonia, chiusa la finestra, chiama `LaunchFromTemplateAsync(id, ParentForBuilder)` (come fa oggi con
    `OpenBuilderRequested`).
  - `PickerOption` aggiunge `string Color`, `string TypeLine` (default `"#808080"`, `""`); nomi e descrizioni delle opzioni
    ufficiali vengono da `OfficialTexts` (anche per WPF: è testo mostrato, nessun comportamento cambia).
- La finestra (spec §7.2): in alto due tessere **Da zero** (apre il creatore vuoto) e **Gruppo vuoto** (crea il gruppo come
  oggi); sotto **Modelli** come schede (barra del colore di 4 px, nome, descrizione su una riga con ellissi, `TypeLine`) con
  **Cerca un modello…** sopra; poi **I tuoi modelli** con Rinomina ed Elimina nel menu della scheda (i comandi di oggi); a
  destra il dettaglio della scheda scelta; in fondo **Personalizza…** (contorno) e **Usa questo modello** (inchiostro), spenti
  senza una scheda scelta. Rinomina in linea come oggi. Tastiera: frecce fra le schede, Invio = Usa questo modello.

- [ ] **Step 1: test rossi** (VM): `Search_ignores_case_and_accents_on_translated_text` (`it-IT`: "diario" trova "Diario del
  cibo"; "umóre" trova "Umore"); `Empty_search_result_says_so`; `Use_creates_once_even_on_double_click` (due `ExecuteAsync` in
  parallelo → un solo `CreateComposedHabitAsync`); `Use_creates_the_translated_habit` (`it-IT`: il nodo creato si chiama come
  il modello tradotto); `Customize_requests_the_builder_with_the_template`; `Detail_lists_what_the_template_records`
  (Sonno: "Ore dormite (h)", "Qualità del sonno", "più volte al giorno: Notte, Pisolino").
- [ ] **Step 2: test rossi** (UI): `Tiles_cards_and_detail_are_laid_out` (due tessere, 8 schede, dettaglio a destra; un solo
  `Button.primary`); `Use_is_disabled_until_a_card_is_selected`; `Enter_on_a_card_uses_it`; composizione:
  `CoreCompositionTests` asserisce che il selettore risolto dalla DI ha `builders` non null.
- [ ] **Step 3: implementa.**
- [ ] **Step 4: PNG** `templates-{light,dark}.png` (una scheda scelta), `templates-search-empty-{light,dark}.png`,
  `templates-narrow-{light,dark}.png` (720 px: il dettaglio va sotto), con l'asserzione strutturale. **Guardarli.**
- [ ] **Step 5: verdi, commit** `feat(ui): il selettore dei modelli — tessere, schede con ricerca, dettaglio, Usa e Personalizza (UX-11)`.

### Task 7: Schermate, inventario, documenti

**Files:** test di screenshot, `docs/phase-4-issues.md`, `docs/ROADMAP.md`, `docs/MILESTONES.md`

- [ ] **Step 1: PNG.** Oltre a quelli dei Task 5 e 6: il creatore caricato da "Sonno" in italiano (`builder-from-template-light.png`);
  Configura con l'editor delle etichette (`configura-scale-labels-{light,dark}.png`). Ognuno con un'asserzione strutturale;
  guardarli tutti.
- [ ] **Step 2: inventario.** Le voci "Creare" dell'Appendice A, una per una, nel nuovo creatore e nel selettore (da zero, gruppo
  vuoto, gli 8 modelli, i tuoi modelli con Rinomina ed Elimina, Salva come modello, ogni mattone del creatore di oggi: campi
  delle quattro nature, unità e scale nuove, serie, gesto, contatore a tocco, astinenza, programma, serie, obiettivo, sessione
  e segmenti, colore con recenti e preferiti, icona, anteprima per tipo). Esito nel report; una voce persa è un difetto del
  task che l'ha tolta.
- [ ] **Step 3: documenti.** Tracker: sezione "Onda 3a" coi finding nuovi; UX-11 e UX-12 `fixed` col test che lo prova.
  ROADMAP "Dove siamo" (3a fatta; prossimo: il piano 3b) e MILESTONES. Commit e push.
- [ ] **Step 4: review dell'intero branch** (modello più capace) con la spec in mano; un'onda di correzioni; **CI verde** su
  Linux e Windows; push. Il merge in `main` dopo che l'autore ha guardato i PNG.

## Decisioni mie (da controllare leggendo)

1. **Una strada sola per creare da un modello**: "Usa questo modello" passa dal creatore senza mostrarlo, non da
   `InstantiateAsync` (che resta per WPF). Costa un creatore costruito per ogni "Usa"; in cambio la parità del principio 8 è
   la garanzia che le due strade facciano la stessa cosa, e la traduzione sta in un punto.
2. **La traduzione sta in Presentation** (`.resx`), non nei dati: nessuna migrazione, nessun dato seminato toccato.
3. **Le etichette di una scala ufficiale si fissano alla creazione** nella `LabelsJson` della metrica, anche creando da zero
   (cambia anche il builder WPF). Le abitudini di prima tengono le loro (inglesi): nessun riempimento a ritroso.
4. **Nomi delle unità e delle scale ufficiali tradotti in visualizzazione** (menu), perché sono dati condivisi fra abitudini:
   il simbolo ("l", "h") resta quello seminato.
5. **Obiettivo nel creatore: periodo `Open` o `Rolling`**; `Fixed` (con inizio e fine) resta in Configura.
6. **La descrizione** arriva dal modello all'abitudine creata, ma il creatore non la mostra (Chi è senza descrizione, spec
   §6.4); si cambia in Configura.
7. **"Salvato ✓"**: il segno è testo nella stringa perché la spec lo scrive così; se l'autore lo vuole come icona Lucide
   (`Icon.Check`), è una riga.
8. **Il creatore non usa le righe-riassunto di Configura**: i campi stanno aperti nei loro blocchi (si crea da capo; il
   riassunto serve a chi rilegge).

## Fuori da 3a

- Impostazioni a categorie, finestre del backup, diario a tutta finestra e a schermo intero: **piano 3b**.
- Un modello di **gruppo** ("Salva come modello" del gruppo, in arrivo da 2b): formato nuovo, chiede anche il gruppo nel
  creatore; a parte.
- I minori di 2b nel tracker (UX2B-M*), UX2B-2/3/4 (domande di design) e UX2B-9 (astinenza sola vecchia se cambiata altrove).
