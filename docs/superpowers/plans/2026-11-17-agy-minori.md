# Minori aperti: GIO-1, GIO-5, GIO-8, GIO-11 (selezione) — brief per Antigravity

> Leggi SOLO dentro il repo. Non leggere la memoria di Claude. Non usare la shell. Non fare commit.
> Non toccare `.resx` né `Strings.Designer.cs`: le stringhe che servono esistono già (sotto).
> Se qualcosa non è chiaro, lascialo ed elencalo alla fine in "NON FATTO".
> Regole del progetto: `CLAUDE.md` (radice). Ogni correzione ha un test che fallisce senza la correzione.

Branch `agy-minori`, da `main`. Descrizione dei difetti: `docs/phase-4-issues.md`, voci GIO-1, GIO-5, GIO-8, GIO-11.

## 1. GIO-1 — test instabile per la cultura

`tests/Recordum.Presentation.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs:253`
(`SaveAsTemplate_creates_custom_template_with_roundtrippable_payload`) a volte legge "Quantity" dove si aspetta
"Quantità". La classe è già in `[Collection(ProcessGlobalCultureCollection.Name)]`
(`tests/Recordum.Presentation.Tests/ProcessGlobalCultureCollection.cs`, che spiega la regola): quindi c'è un'altra
classe di test che cambia `Strings.Culture` (o `CultureInfo.DefaultThreadCurrentCulture`/`DefaultThreadCurrentUICulture`)
**senza** stare in quella collection, o senza ripristinare in un `finally`.

- Cerca in `tests/Recordum.Presentation.Tests` ogni scrittura di `Strings.Culture =`, `CultureInfo.DefaultThreadCurrent`,
  `CultureInfo.CurrentCulture =`, `CultureInfo.CurrentUICulture =`, e gli helper che le fanno (es. `TestCulture`).
- Ogni classe che scrive `Strings.Culture` o un `DefaultThreadCurrent*` (stato del processo) va in quella collection e
  ripristina in `finally`/`Dispose`. `CurrentCulture`/`CurrentUICulture` del thread non sono del processo: lasciali.
- In più, nel test di GIO-1 confronta col nome letto **nella stessa cultura** in cui il ViewModel l'ha scritto: fissa
  `Strings.Culture` a it-IT all'inizio del test e ripristinalo in `finally`.
- Nel resoconto: l'elenco delle classi che hai spostato o corretto, con il motivo.

## 2. GIO-5 — un filtro che svuota la lista non si può più togliere

Oggi `HomeViewModel.Rebuild` (`src/Recordum.Presentation/ViewModels/HomeViewModel.cs`, circa riga 758) mette
`State = HomeState.Empty` quando non resta nessuna riga, anche se le righe ci sono e le nasconde un filtro. In `Empty` la
Home mostra il germoglio "crea un'abitudine" e nasconde gli strumenti (lente, "fatte", ordinamento: `HomeView.axaml`
circa riga 101, visibili solo con `State == Loaded`), quindi il filtro non si spegne più.

**ViewModel**
- `src/Recordum.Presentation/ViewModels/HomeState.cs`: nuovo valore `NoResults` **in fondo** all'enum.
- `HomeViewModel.Rebuild`: se `Rows.Count == 0` e la scheda attiva ha righe prima dei filtri (`_lastFetched` filtrato
  solo per `ActiveTab`, come all'inizio di `Rebuild`) e almeno un filtro è acceso → `NoResults`; se no come oggi.
  "Filtro acceso" = `SearchText` non vuoto, `TypeFilter` o `TagFilter` non vuoti, `HideCompletedToday` vero.
- Proprietà `bool ShowsTools` = `State is HomeState.Loaded or HomeState.NoResults`, notificata quando cambia `State`.
- `[RelayCommand] ClearFilters()`: spegne i quattro filtri sopra (non tocca ordinamento, scheda né `DaysBack`) e
  ricostruisce **una** volta (usa `_suppressRebuild` come fa `ResetFilters`, circa riga 595).

**Vista** — `src/Recordum.UI/Views/HomeView.axaml` e `src/Recordum.UI/Views/HomeStateTemplate.cs`
- Lo `StackPanel` degli strumenti (circa riga 101) si lega a `ShowsTools` invece che a `State == Loaded`.
- `HomeStateTemplate`: nuova proprietà `NoResults` (come `Empty`) e il ramo `HomeState.NoResults => NoResults?.Build(param)`.
- In `HomeView.axaml`, dentro `v:HomeStateTemplate`, un `<v:HomeStateTemplate.NoResults>` con un `StackPanel`
  centrato (stesso schema del ramo `Error`, `DataContext="{Binding $parent[v:HomeView].DataContext}"`,
  `x:DataType="vm:HomeViewModel"`): `TextBlock` con `res:Strings.Home_NoResults` in `Brush.TextMuted` e un `Button`
  `Classes="ghost"` con `res:Strings.Home_NoResults_Clear` legato a `ClearFiltersCommand`.
- Stringhe già presenti: `Strings.Home_NoResults` ("Nessuna abitudine con questi filtri"), `Strings.Home_NoResults_Clear`
  ("Togli i filtri").

**Test**
- `tests/Recordum.Presentation.Tests/ViewModels/HomeNoResultsTests.cs`: con due righe attive entrambe fatte oggi e
  `HideCompletedToday = true` → `State == NoResults`, `ShowsTools` vero; `ClearFilters` → `State == Loaded`, due righe;
  ricerca che non trova niente → `NoResults`; nessuna abitudine e nessun filtro → `Empty` (come oggi); scheda
  Archiviate vuota senza filtri → `Empty`. Prendi come modello i test esistenti della Home (es.
  `tests/Recordum.Presentation.Tests/ViewModels/HomeTagFilterTests.cs`) per costruire il ViewModel e le righe finte.
- `tests/Recordum.UI.Tests/HomeNoResultsViewTests.cs` (`[AvaloniaFact]`, xunit v3, `using Xunit;`): con
  `NoResults` la vista mostra il pulsante "Togli i filtri" e la lente (`SearchButton`) è visibile; un clic sul pulsante
  riporta le righe. Modello: `tests/Recordum.UI.Tests/HomeTagFilterViewTests.cs`.

## 3. GIO-8 — il secondo punto di un elenco resta "- "

Nell'editor del diario (colonna Note, `src/Recordum.UI/Controls/DiaryEditor/`: `DiaryEditor.axaml.cs`,
`DiaryLineScanner.cs`, `DiaryElementGenerator.cs`) il testo "- 14 km\n- ritmo 5:40" disegna il pallino sul primo punto e
lascia il trattino sul secondo. Il blocco di sola lettura del giorno li disegna giusti tutti e due.

- Trova perché la seconda riga non viene riconosciuta come voce d'elenco (scansione per riga, offset, riga corrente
  esclusa, invalidazione dopo l'Enter: leggi i tre file) e correggilo alla radice, nello scanner o nel generatore.
- Test in `tests/Recordum.UI.Tests` accanto ai test esistenti del `DiaryEditor` (cerca `DiaryLineScanner` o
  `DiaryEditor` nei test): lo scanner riconosce **due** voci d'elenco in "- 14 km\n- ritmo 5:40" e in
  "- a\n- b\n- c"; e, se il difetto è nel generatore, l'editor con quel testo produce due elementi pallino.
- Se il difetto non si riproduce con un test, **non inventare una correzione**: scrivi in "NON FATTO" cosa hai provato.

## 4. GIO-11, solo la parte sulla selezione

`HomeViewModel.ReloadPreservingSelectionAsync` (cerca il metodo) legge la selezione **prima** di aspettare il caricamento
e la rimette **dopo**: se nel frattempo l'utente ha scelto altre righe, la sua scelta viene sovrascritta da quella vecchia.

- Correzione: dopo l'attesa, rimetti la selezione salvata solo se la selezione attuale è ancora quella letta prima
  (stessi id); altrimenti tieni quella attuale (filtrata sulle righe che esistono ancora).
- Test in `tests/Recordum.Presentation.Tests/ViewModels/` (classe nuova `HomeReloadSelectionTests`): una query finta
  che si blocca su un `TaskCompletionSource`; selezione A → parte il ricaricamento → l'utente seleziona B → il
  caricamento finisce → la selezione è B.
- Le altre due parti di GIO-11 (sottoscrizioni della Home, nota del giorno in Markdown grezzo) **non** si toccano.

## Alla fine

Elenca: file toccati; test scritti; "NON FATTO" con i motivi. Non toccare file fuori da questi elenchi; se pensi che
serva, scrivilo in "NON FATTO".
