# Direzione visiva, onda 1 — Fondamenta e Home

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** il sistema visivo della spec (Inter, scala, neutri caldi, colore dell'abitudine con la sua variante "testo",
pulsanti e campi) in tutta la shell Avalonia, più la Home e la cornice ridisegnate, senza perdere una funzione.

**Architecture:** i valori stanno nei token (`Themes/Tokens.axaml`, varianti Light/Dark) e in un solo modulo di calcolo
dei colori (`Theming/HabitColors.cs`); le viste li leggono con `DynamicResource`, così un cambio di tema a runtime li
aggiorna. Le aggiunte in Presentation sono additive (i ViewModel sono condivisi con WPF, che non si tocca).

**Tech Stack:** .NET 10, Avalonia 12.1.2, CommunityToolkit.Mvvm, xunit v3 + Avalonia.Headless (UI), xunit (Presentation).

**Spec:** `docs/superpowers/specs/2026-10-28-ux-direction-design.md` (approvata il 2026-10-28) — §4 e §5, più la §9 (onda 1)
e la §10. Mockup: `docs/design/2026-10-28-ux-mockups/` (`correzioni.html`, `integrazioni.html` parte 1,
`anteprima-home-v2.html`, `cornice.html`). **Il testo della spec vince sui mockup.**

## Global Constraints

- Font: **Inter** per l'interfaccia (`Font.Default`), **Source Serif 4** per testo dell'utente e marchio (`Font.Serif`).
- Scala: 20 titoli e numeri grandi · 16 titoli di sezione · 14 testo · 13 etichette e comandi · 12 note e giorni.
- Neutri (chiaro / scuro): fondo `#FBFBFA`/`#1B1B1A` · pannello `#FFFFFF`/`#222221` · barra `#F4F3F1`/`#181817` · riga
  `#ECEBE8`/`#2E2E2C` · bordo dei campi `#D6D5D0`/`#3A3A37` · selezione `#ECEBE8`/`#2E2E2C` · testo `#1F1F1D`/`#E6E5E1` ·
  attenuato `#6B6A65`/`#A3A29C` · tenue `#A3A29C`/`#6B6A65` · inchiostro `#1F1F1D` su `#FFFFFF` / `#E6E5E1` su `#1B1B1A`.
- Tinta della riga: colore dell'abitudine sul pannello al **9 %** (chiaro) e **16 %** (scuro). Gradini della mappa:
  vuoto = cella neutra, poi **25 / 50 / 75 / 100 %**.
- Valori nel colore dell'abitudine = variante "testo" a **≥ 4,5:1** sulla tinta; testo e attenuato ≥ 4,5:1 sul loro fondo.
- Pulsanti: colore dell'abitudine solo per il comando che registra; inchiostro per l'azione principale (una per
  finestra); contorno per le secondarie; solo testo per "Annulla". Nessun secondario pieno di grigio.
- Icone Lucide; niente glifi Unicode come icone (`＋`, `▾`, `✎`, `☰`).
- Home: titolo **"Oggi"** con la data lunga; **oggi a destra** nella striscia; il menu ☰ esce.
- Ogni stringa nuova in `Strings.resx` + `Strings.it.resx` + accessor in `Strings.Designer.cs` (**solo via shell**: l'hook
  blocca Edit/Write sui `*.Designer.cs`). Test che leggono etichette: `TestCulture.Pin()`.
- WPF non si tocca e deve compilare: in Presentation solo proprietà e tipi **nuovi**.
- Build con `--no-incremental`; mai `-p:UseAppHost=false`; `TreatWarningsAsErrors`.
- **Un test di regressione si prova rosso** contro il codice di prima (CLAUDE.md §7). I test che oggi fissano i valori
  vecchi (`RowCardTintTests`, `EmbeddedFontTests`, test della `HeatmapStrip`) si **aggiornano** con la ragione nel commit,
  non si cancellano.
- Branch: `ux1-foundations`, creato da `ux-review` (porta con sé spec e mockup).

## Review Focus

1. **Tema cambiato con l'app aperta** (il sistema passa a scuro): tinte, variante "testo", striscia e heatmap si
   aggiornano senza riavvio → Task 3 (test che cambia `RequestedThemeVariant` a runtime).
2. **Colori estremi o rotti** (`#FFFFFF`, `#000000`, `#FFFF00`, un hex malformato, `null`): la variante "testo" arriva a
   4,5:1 o, se il colore è rotto, si usa il testo normale; niente eccezioni → Task 3.
3. **Colonna della Home stretta** (280 px, il minimo) con nomi lunghi e strisce da 14 giorni: niente sovrapposizioni
   (F2b-5), nome con l'ellissi, la colonna di oggi allineata → Task 6.
4. **Righe con strisce diverse** (nessuna, 7, 14 giorni) e figlie rientrate nei gruppi: la colonna di oggi è sotto
   "oggi" dell'intestazione → Task 6.
5. **Valori da scrivere nella striscia**: 0,5 · 7 · 1234,5 · 12000 · durate · giorno senza valore; ogni cella al massimo
   4 caratteri, "·" per il vuoto, separatore decimale della cultura → Task 4.

---

### Task 1: Inter, la scala, il font di ogni finestra

**Files:**
- Modify: `Directory.Packages.props` e `src/Recordum.UI/Recordum.UI.csproj` (oppure `src/Recordum.UI/Fonts/` con i TTF di
  Inter e la sua OFL), `src/Recordum.UI/Themes/Tokens.axaml`, `src/Recordum.UI/App.axaml`,
  `src/Recordum.UI/Views/Timer/TimerStatusBarView.axaml`, `src/Recordum.UI/Views/HabitScreen/TimerControlView.axaml`,
  `src/Recordum.UI/Views/HabitScreen/QuickAdd/MetricCellView.axaml`, `THIRD-PARTY-NOTICES.md`
- Test: `tests/Recordum.UI.Tests/EmbeddedFontTests.cs`, `tests/Recordum.UI.Tests/Styles/TypeScaleTests.cs` (nuovo)

**Interfaces:**
- Produces: `Font.Default` = Inter; `Font.Serif` invariato; chiavi `FontSize.Small`=12, `FontSize.Label`=13 (nuova),
  `FontSize.Body`=14, `FontSize.Heading`=16 (era 15), `FontSize.Title`=20 (nuova). Uno stile `:is(Window)` in `App.axaml`
  che dà `FontFamily="{DynamicResource Font.Default}"` a ogni finestra, anche quelle dei test (chiude **UX-P2**).

- [ ] **Step 1: test rossi.** `EmbeddedFontTests`: `Font_Default_resolves_to_Inter` (la `FontFamily` risolta di un
  `TextBlock` in una `Window` qualunque ha nome di famiglia "Inter"); `Font_Serif_still_resolves_to_Source_Serif_4`;
  `A_view_hosted_in_a_plain_test_window_uses_the_app_font` (una `UserControl` in `new Window()` eredita Inter — è il caso dei
  PNG del dettaglio di oggi). `TypeScaleTests`: le cinque chiavi valgono 12/13/14/16/20.
- [ ] **Step 2: eseguire, vedere il rosso** (`dotnet test tests/Recordum.UI.Tests --filter "FullyQualifiedName~EmbeddedFontTests|FullyQualifiedName~TypeScaleTests"`).
- [ ] **Step 3: implementare.** Inter dal pacchetto `Avalonia.Fonts.Inter` **12.1.2** (stessa versione di Avalonia,
  versione esatta) referenziato con `avares://Avalonia.Fonts.Inter/Assets#Inter` — già provato nel confronto dei font —
  oppure i TTF in `Fonts/`; voce Inter (SIL OFL 1.1, Copyright The Inter Project Authors) in `THIRD-PARTY-NOTICES.md`. I tre
  usi di `Consolas,DejaVu Sans Mono,monospace` diventano `Font.Default` con le cifre tabellari
  (`FontFeatures="+tnum"`).
- [ ] **Step 4: verdi**, poi la suite UI (`Category!=Performance`): i PNG cambiano, i conteggi no.
- [ ] **Step 5: commit** `feat(ui): Inter per l'interfaccia, la scala tipografica e il font di ogni finestra (UX-1, UX-P2)`.

### Task 2: I neutri, i tre livelli di testo, l'inchiostro

**Files:**
- Modify: `src/Recordum.UI/Themes/Tokens.axaml`, `src/Recordum.UI/App.axaml` (`ColorPaletteResources`),
  `src/Recordum.UI/Themes/ControlStyles.axaml`, `src/Recordum.UI/Themes/RecordumStyles.axaml`
- Create: `src/Recordum.UI/Theming/HabitColors.cs` (solo `Contrast` in questo task), `tests/Recordum.UI.Tests/Styles/TokenContrastTests.cs`

**Interfaces:**
- Produces (chiavi, Light/Dark con i valori dei Global Constraints): `Brush.Background` (fondo), `Brush.Pane` (nuova,
  pannello), `Brush.Surface` (barra e zone secondarie), `Brush.SurfaceElevated` (popup, campi: `#FFFFFF`/`#2A2A28`),
  `Brush.Border` (riga), `Brush.FieldBorder` (nuova), `Brush.Selection` (nuova), `Brush.Text`, `Brush.TextMuted`,
  `Brush.TextFaint` (nuova), `Brush.Ink` e `Brush.OnInk` (nuove), `Brush.Cell` (nuova, la cella vuota della mappa:
  `#F4F3F1`/`#2A2A28`), `Brush.Danger` (`#B42318`/`#F97066`), `Brush.Warning` (`#B45309`/`#F5B454`) e
  `Brush.WarningSurface` (`#FDF6EC`/`#2E2418`) nuove, `Brush.Success` (`#3F7A26`/`#7BC25A`). `Brush.Accent`/`Brush.OnAccent`
  restano come chiavi e valgono l'inchiostro (le usano molte viste; il significato diventa "azione").
- Produces: `public static double HabitColors.Contrast(Color a, Color b)` — rapporto WCAG 2 (luminanza relativa sRGB).
- Il `ColorPaletteResources` di Fluent ha `Accent` = inchiostro (`#FF1F1F1D` / `#FFE6E5E1`).

- [ ] **Step 1: test rossi.** `TokenContrastTests` (`[AvaloniaTheory]` sui due temi): `Text`, `TextMuted` ≥ 4,5 su
  `Background`, `Pane`, `Surface`; `OnInk` su `Ink` ≥ 4,5; `Danger` e `Warning` ≥ 4,5 su `Pane` (e `Warning` su
  `WarningSurface`); `TextFaint` ≥ 2,5 su `Pane` (solo segnaposto); `Fluent_accent_is_the_ink` (`SystemAccentColor` risolto
  == `Brush.Ink`). Un caso noto per la funzione: `Contrast(#000000, #FFFFFF)` = 21 con tolleranza 0,01; `Contrast(x, x)` = 1.
- [ ] **Step 2: rosso visto** (le chiavi nuove non esistono, l'accento è il blu).
- [ ] **Step 3: implementare** i token e la palette. Gli usi di `Brush.Surface` come "pannello" nelle viste passano a
  `Brush.Pane` dove il mockup ha il bianco: il criterio è il ruolo della superficie, non il nome di oggi.
- [ ] **Step 4: verdi + suite UI.**
- [ ] **Step 5: commit** `feat(ui): neutri caldi, tre livelli di testo, l'inchiostro come accento (UX-3)`.

### Task 3: Il colore dell'abitudine — tinta e variante "testo"

**Files:**
- Modify: `src/Recordum.UI/Theming/HabitColors.cs`, `src/Recordum.UI/Themes/Tokens.axaml`
- Create: `src/Recordum.UI/Converters/HabitTextBrushConverter.cs`, `tests/Recordum.UI.Tests/Styles/HabitColorsTests.cs`

**Interfaces:**
- Produces (token tematici `x:Double`): `Opacity.RowTint` 0.09/0.16, `Opacity.RowTintSelected` 0.16/0.26,
  `Opacity.TimerTint` 0.08/0.14, `Opacity.Heat1..Heat4` 0.25/0.5/0.75/1.0 (uguali nei due temi).
- Produces: `public static Color HabitColors.Blend(Color top, double opacity, Color under)`;
  `public static Color HabitColors.TextVariant(Color habit, Color pane, double tintOpacity, bool dark)`;
  `public static bool HabitColors.TryParse(string? hex, out Color color)`.
- Produces: `HabitTextBrushConverter : IMultiValueConverter` — valori `[string? hex, ThemeVariant variant]` → `IBrush`;
  legge `Brush.Pane` e `Opacity.RowTint` della variante da `Application.Current`; hex rotto → `Brush.Text`.
- **La tinta non passa più da un convertitore con l'alfa**: è un livello sotto il contenuto (`Border` col colore pieno e
  `Opacity="{DynamicResource Opacity.RowTint}"`), così segue il tema a runtime. Sopra il pannello l'opacità equivale alla
  miscela della spec.

- [ ] **Step 1: test rossi.** `HabitColorsTests`:
  `Text_variant_reaches_4_5_on_its_tint_for_every_palette_colour` (i 16 colori della spec §7.1, chiaro e scuro, con
  `pane` = i valori di `Brush.Pane`);
  `Text_variant_reaches_4_5_for_extreme_colours` (`#FFFFFF`, `#000000`, `#FFFF00`, `#00FFFF`, `#808080`);
  `The_raw_colour_is_not_enough` (prova che il test morde: `Contrast(#C98A1E, Blend(#C98A1E, 0.09, #FFFFFF))` < 3);
  `Dark_variant_is_lighter_than_the_habit_colour` (luminanza maggiore); `TryParse` rifiuta `null`, `""`, `"#12"`, `"blu"`.
  In `Styles`: `Row_tint_follows_a_theme_change_at_runtime` — una riga montata in chiaro ha l'opacità 0.09; dopo
  `Application.Current.RequestedThemeVariant = Dark` + `RunJobs()` vale 0.16 (Review Focus 1).
- [ ] **Step 2: rosso visto.**
- [ ] **Step 3: implementare `TextVariant`.** L'algoritmo (il test non lo fissa): `bg = Blend(habit, tintOpacity, pane)`;
  in chiaro si parte dal colore e lo si mescola verso il nero a passi del 4 %; in scuro si parte da `Blend(white, 0.30,
  habit)` (70 % colore, 30 % bianco) e lo si mescola verso il bianco; ci si ferma al primo ≥ 4,5:1, al più 25 passi (poi
  nero o bianco puri).
- [ ] **Step 4: verdi.**
- [ ] **Step 5: commit** `feat(ui): la tinta dell'abitudine per tema e la variante "testo" a 4,5:1`.

### Task 4: La striscia coi valori e la heatmap nel colore dell'abitudine

**Files:**
- Modify: `src/Recordum.Presentation/ViewModels/HomeRowViewModel.cs` (additivo), `src/Recordum.UI/Controls/HeatmapStrip.axaml(.cs)`,
  `src/Recordum.UI/Controls/YearHeatmapControl.axaml(.cs)`, `src/Recordum.UI/Themes/Tokens.axaml`
- Delete: `src/Recordum.UI/Converters/IntensityStepToBrushConverter.cs`, le chiavi `Brush.Heatmap.*` e `Brush.HeatmapEmpty`
- Test: `tests/Recordum.Presentation.Tests/ViewModels/HomeRowStripCellsTests.cs` (nuovo),
  `tests/Recordum.UI.Tests/HeatmapStripScreenshotTests.cs`, `tests/Recordum.UI.Tests/Styles/NoGitHubGreensTests.cs` (nuovo)

**Interfaces:**
- Produces (Presentation, nuovi): `public readonly record struct StripCell(string Glyph, bool On, bool IsToday)`;
  `HomeRowViewModel.StripCells : ImmutableArray<StripCell>` allineato a `StripCounts` (dal più vecchio, **oggi ultimo**).
  Glifi: Azione, Diario, Astinenza → "✓" se acceso; Contatore e Stato → il valore del giorno da `ValuesLast14Days`
  (o il conteggio per il contatore a tocco); vuoto → "·".
- Produces: `public static string StripValueFormat.Compact(decimal value, CultureInfo culture)` in Presentation:
  intero se intero; una cifra decimale sotto 10; `1,2k` da 1000; `12k` da 10000; mai più di 4 caratteri.
- Consumes: `HabitColors`, `HabitTextBrushConverter` (Task 3).
- `HeatmapStrip` disegna `StripCells` come testo (13 px, 600 per gli accesi) nella variante "testo"; i vuoti in
  `Brush.TextFaint`; si ridisegna su `ActualThemeVariantChanged`. Il disegno per forme di oggi (`StripStyle`) resta per
  chi lo usa fuori dalla Home. `YearHeatmapControl` riceve `HabitColor` (hex) e colora i gradini con `Brush.Cell` e
  `Opacity.Heat1..4`.
- **Trappola**: l'unità di `ValuesLast14Days` per i campi tempo va verificata in `HabitTreeQueryService` prima di
  formattare (secondi o minuti): una durata si scrive in minuti.

- [ ] **Step 1: test rossi.** `HomeRowStripCellsTests` (cultura fissata `it-IT`): `Action_on_days_are_check_marks`,
  `Counter_days_show_their_value` (5 → "5"), `Empty_day_is_a_middle_dot`, `Today_is_the_last_cell`,
  `Values_are_compact` con 0,5 → "0,5", 7 → "7", 1234,5 → "1,2k", 12000 → "12k" (Review Focus 5), `Length_follows_the_strip`
  (Days7 → 7 celle, None → 0). `NoGitHubGreensTests`: nessun file sotto `src/Recordum.UI` contiene `Brush.Heatmap` né
  gli hex `#9BE9A8`, `#40C463`, `#30A14E`, `#216E39` (guardia come `DayDerivationGuardTests`).
- [ ] **Step 2: rosso visto.**
- [ ] **Step 3: implementare** `StripCells`, `Compact`, il disegno della striscia e della heatmap; togliere convertitore e chiavi.
- [ ] **Step 4: verdi**; `HeatmapStripScreenshotTests` aggiornati con un'asserzione strutturale (numero di celle, il glifo
  dell'ultima).
- [ ] **Step 5: commit** `feat(ui): la striscia scrive i valori, la heatmap ha il colore dell'abitudine (UX-4, UX-6)`.

### Task 5: Pulsanti, campi, menu

**Files:**
- Modify: `src/Recordum.UI/Themes/ControlStyles.axaml`, `src/Recordum.UI/Themes/RecordumStyles.axaml`,
  `src/Recordum.UI/Themes/Icons.axaml` (le icone Lucide che mancano: plus, chevron-down, check, play, sliders-horizontal,
  pen-line, pin, eye-off, arrow-up-down, home, book, chart, settings, archive, trash, pencil, rotate-ccw)
- Test: `tests/Recordum.UI.Tests/Styles/ButtonStyleTests.cs` (nuovo)

**Interfaces:**
- Produces (classi): `Button` di base = contorno (`Brush.Pane`, bordo `Brush.FieldBorder`, raggio 6); `Button.primary` =
  `Brush.Ink`/`Brush.OnInk`; `Button.ghost` = solo testo attenuato; `TextBox`/`ComboBox` con cornice di 1 px
  `Brush.FieldBorder`, raggio 6, segnaposto `Brush.TextFaint` (chiude UX-15 per tutte le viste); `MenuItem` con l'icona a
  sinistra (16 px, `Brush.TextMuted`) e la voce pericolosa in `Brush.Danger` (classe `danger`).
- Il pulsante "che registra" nel colore dell'abitudine non è uno stile globale: in quest'onda esiste solo il log rapido
  della riga (Task 6).

- [ ] **Step 1: test rossi.** `ButtonStyleTests` (due temi): `A_plain_button_is_outlined_not_filled` (fondo == `Brush.Pane`,
  bordo 1 px — oggi è il grigio Fluent); `Primary_is_the_ink`; `Ghost_has_no_border`; `TextBox_has_a_full_frame` (spessore
  del bordo 1 su tutti i lati); `A_danger_menu_item_is_red`.
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + suite UI.**
- [ ] **Step 5: commit** `feat(ui): pulsanti a contorno, primario a inchiostro, campi con la cornice (UX-5, UX-15)`.

### Task 6: La Home

**Files:**
- Modify: `src/Recordum.UI/Views/HomeView.axaml(.cs)`, `src/Recordum.UI/Views/Rows/HabitRow.axaml`,
  `src/Recordum.UI/Views/Rows/GroupRow.axaml`, `src/Recordum.UI/Views/Rows/RowCardStyles.axaml`,
  `src/Recordum.UI/Views/Rows/RowMenu.cs`, `src/Recordum.UI/Views/EmptyState.axaml`, `src/Recordum.UI/Views/MainWindow.axaml`
  (larghezza della colonna Home), `src/Recordum.Presentation/ViewModels/HomeViewModel.cs` e `HomeRowViewModel.cs`
  (additivi), `Strings*.resx` + `Strings.Designer.cs`
- Test: `tests/Recordum.UI.Tests/HomeViewTests.cs`, `tests/Recordum.UI.Tests/HomeRowViewTests.cs`,
  `tests/Recordum.UI.Tests/RowCardTintTests.cs`, `tests/Recordum.Presentation.Tests/ViewModels/HomeHeaderTests.cs` (nuovo)

**Interfaces:**
- Produces (Presentation, nuovi): `HomeViewModel.TodayLabel : string` (data lunga del giorno dell'app, "mercoledì 28
  ottobre", dal `DayBoundary` di `daily_reset_time` e da `IClock`); `HomeViewModel.StripHeader :
  ImmutableArray<StripCell>` di 14 elementi (glifo = giorno breve "mer", l'ultimo = la stringa "oggi"/"today",
  `IsToday` sull'ultimo); `HomeViewModel.MaxVisibleStripLength : int` (la striscia più lunga fra le righe visibili);
  `HomeRowViewModel.ChildrenDoneToday : int` e `ChildrenTotal : int` (per i gruppi, con lo stesso predicato "completata oggi"
  che usa il filtro `HideCompletedToday`).
- Stringhe nuove: `Home.Title.Today` ("Today"/"Oggi"), `Home.StripHeader.Today` ("today"/"oggi"), `Home.Filter.Done`
  ("Done"/"fatte"), `Home.Group.Progress` ("{0} of {1}"/"{0} di {1}").
- Consumes: Task 2–5.

Layout (spec §5): testa "Oggi" 20/600 + data attenuata 13 + "+ Nuova" `Button.primary` con le icone plus e chevron-down
(menu: Nuova abitudine / Nuovo gruppo); segmenti Attive · Archiviate · Cestino; riga degli strumenti: Cerca, "fatte"
(interruttore con eye-off, legato a `HideCompletedToday`), ordinamento; intestazione dei giorni allineata a destra sopra le
strisce. Riga: livello di tinta (`Opacity.RowTint`, `Opacity.RowTintSelected` quando aperta, con il bordo di 1,5 px che c'è
già), barra di 4 px, nome 14/500 con l'ellissi, valore chiave 12 attenuato, striscia (Task 4), log rapido a destra
(`QuickActionCommand` esistente) come pulsante a contorno nel colore dell'abitudine con l'icona del tipo: azione check,
contatore plus, sessione play, stato sliders-horizontal, diario pen-line, astinenza rotate-ccw. Gruppo: riga non tinta con
la barra nel colore del gruppo, chevron, nome 12/600 attenuato, "{0} di {1}". Menu della riga: le voci di `RowMenu` di
oggi con le icone Lucide (Elimina definitivamente e Sposta nel cestino con `danger`). Colonna della Home: larghezza
iniziale **340**, minima **280**; sotto 320 la data della testa si nasconde. Vuoti, caricamento, errore: una frase e al
più un `Button.primary`.

- [ ] **Step 1: test rossi.** `HomeHeaderTests` (clock fisso al 28/10/2026 alle 10:00, reset 05:00, `it-IT`):
  `Today_label_is_the_long_date`; `Strip_header_ends_with_today` ("oggi" ultimo, "mar" penultimo);
  `Before_the_reset_today_is_yesterday` (alle 04:30 l'ultimo è il 27); `Group_counts_children_done_today` (5 figlie, 3
  fatte → 3 e 5). UI (`[AvaloniaFact]`, `TestCulture.Pin()`): `Header_reads_Oggi_with_the_date`;
  `Today_column_is_aligned_across_strip_lengths` — righe con Days7, Days14 e None più una figlia rientrata: il bordo destro
  dell'ultima cella di ogni striscia coincide (±1 px) con quello di "oggi" nell'intestazione (Review Focus 4);
  `Narrow_home_has_no_overlap` — a 280 px i rettangoli di "+ Nuova", dei segmenti e degli strumenti non si intersecano e i
  nomi lunghi hanno l'ellissi (Review Focus 3, F2b-5: rosso sul layout di oggi); `Every_row_has_its_quick_log`;
  `Group_row_shows_progress`; `Row_menu_items_have_icons`. `RowCardTintTests` aggiornato: la tinta è il livello con
  `Opacity.RowTint`, non più l'alfa 26.
- [ ] **Step 2: rosso visto.**
- [ ] **Step 3: implementare** VM additivi, stringhe (Designer via shell), viste.
- [ ] **Step 4: verdi + suite UI + Presentation.**
- [ ] **Step 5: commit** `feat(ui): la Home "Oggi" con l'intestazione dei giorni, il log rapido e i gruppi con l'avanzamento`.

### Task 7: La cornice — barra, schede, timer, sblocco

**Files:**
- Modify: `src/Recordum.UI/Views/WorkspaceRibbon.axaml(.cs)`, `src/Recordum.UI/Views/MainWindow.axaml(.cs)`,
  `src/Recordum.UI/Views/Workspace/WorkspaceView.axaml`, `src/Recordum.UI/Views/Timer/TimerStatusBarView.axaml`,
  `src/Recordum.UI/Views/Startup/UnlockWindow.axaml`, `src/Recordum.UI/Views/Startup/FatalErrorWindow.axaml`,
  `Strings*.resx` + `Strings.Designer.cs`
- Test: `tests/Recordum.UI.Tests/MainWindowTests.cs`, `tests/Recordum.UI.Tests/WorkspaceViewTests.cs`,
  `tests/Recordum.UI.Tests/Timer/*`, `tests/Recordum.UI.Tests/Startup/*`

**Interfaces:**
- Barra a sinistra (52 px, `Brush.Surface`): "R" in `Font.Serif` 20/600 in alto; **Oggi** (`ShowHomeCommand`, icona home,
  tooltip "Oggi"); **Diario** (disabilitato, tooltip `Rail.ComingSoon` "{0} — coming soon"/"{0} — in arrivo"); **Statistiche**
  (`ChartsButton` visibile e disabilitato con lo stesso tooltip); in basso **Impostazioni**. Escono dalla barra i pulsanti
  nascosti Backup, Esporta, Diagnostica (i comandi restano nel ViewModel; Backup è già in Impostazioni › Dati e backup;
  Esporta e Diagnostica arrivano con l'onda 3 nella categoria Informazioni).
- Barra del titolo: esce `RibbonToggle` (☰) e il testo "R Recordum" (il marchio vive nella barra a sinistra: una volta
  sola, UX-8); restano riduci, ingrandisci, chiudi.
- Schede: pallino di 8 px nel colore, nome, icona pin quando la scheda è fissata (il comando "Fissa / sblocca" esistente),
  ×; a vuoto il testo di oggi.
- Barra del timer: livello di tinta `Opacity.TimerTint` nel colore dell'abitudine che corre, tempo in `Font.Default` +tnum
  15/600 nella variante "testo", nome, Pausa/Riprendi/Ferma con le icone.
- Sblocco: marchio "Recordum" in `Font.Serif` 28/600, la frase, il campo, "Ricorda su questo dispositivo", **un**
  `Button.primary`; stringa nuova `Unlock.LostPassphrase` ("If you forget the passphrase, your data cannot be recovered: that
  is the price of encryption." / "Se dimentichi la passphrase, i dati non si possono recuperare: è il prezzo della
  cifratura."). Errore fatale con gli stessi token.

- [ ] **Step 1: test rossi.** `Rail_has_today_diary_stats_settings` (ordine, Diario e Statistiche disabilitati con il
  tooltip); `There_is_no_ribbon_toggle`; `The_brand_appears_once` (un solo `TextBlock` "R" in serif nell'albero di
  `MainWindow`, nessun "Recordum" nella barra del titolo); `A_pinned_tab_shows_the_pin`; `Timer_bar_is_tinted_with_the_habit`;
  `Unlock_has_one_primary_button_and_the_warning`.
- [ ] **Step 2: rosso visto.** **Step 3: implementare.** **Step 4: verdi + suite UI.**
- [ ] **Step 5: commit** `feat(ui): la cornice — barra a sinistra senza ☰, schede col colore, timer tinto, sblocco`.

### Task 8: Schermate, inventario, prestazioni, documenti

**Files:**
- Modify: i test di screenshot esistenti (`ShellIntegrationTests`, `ControlsScreenshotTests`, `EmptyStateTests`,
  `Startup/*`), `docs/phase-4-issues.md`, `docs/ROADMAP.md`, `docs/MILESTONES.md`

- [ ] **Step 1: PNG in chiaro e in scuro** di: Home normale, Home a 280 px, Home vuota, Archivio e Cestino vuoti, la riga col
  menu aperto, la barra del timer, lo sblocco al primo avvio e normale, l'errore fatale. Ognuno con un'asserzione
  strutturale. I PNG vecchi che non produce più nessun test (`editor-*`, `livepreview-*`, `theme-*`, `frame-*`) non vanno
  negli artefatti (la CI pulisce la cartella prima della suite, se oggi non lo fa).
- [ ] **Step 2: prestazioni.** `Category=Performance` di UI e Data in locale **prima** (sul primo commit del branch) e
  **dopo**, alternati due volte come in D9; la Home con 300 righe resta nel budget (< 500 ms). Numeri nel report.
- [ ] **Step 3: inventario.** Le voci "Home" e "Cornice" dell'Appendice A della spec: ognuna trovata nel nuovo albero
  visuale o nel suo comando (cerca, nascondi completate, ordinamento, segmenti, + Nuova con i due comandi, log rapido, le
  otto voci del menu della riga, trascina e rilascia, caricamento/errore/Riprova, i tre vuoti, i pulsanti della finestra,
  schede chiudi/fissa/vuoto, barra del timer). Esito nel report.
- [ ] **Step 4: documenti.** Tracker: UX-1, UX-3, UX-4, UX-5, UX-6, UX-15, UX-P2 `fixed`; UX-8 e UX-13 `in-progress` (la
  parte della Home e della cornice fatta); F2b-5 `fixed` se il test di Task 6 lo prova. ROADMAP "Dove siamo" e MILESTONES.
  Commit e push.
- [ ] **Step 5: review dell'intero branch** (modello più capace) con la spec in mano; un'onda di correzioni; **CI verde** su
  Linux e Windows; push.
