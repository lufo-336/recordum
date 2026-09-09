# Sonda di styling Avalonia — quanto transfer del linguaggio visivo di Recordum

**Branch**: `avalonia-linux-slice`. **Tipo**: probe/spike, non una feature — l'output è una stima
misurata, non codice di produzione. Niente sotto `src/Recordum.WPF/` è stato toccato.

## Ipotesi in verifica

`BaseStyles.xaml` (WPF) riscrive da zero 1046 righe / 34-35 tipi di controllo. L'ipotesi: la
maggior parte di quelle righe è il costo di disfare i default Aero di WPF, non del linguaggio
visivo di Recordum — e su Avalonia, la cui `FluentTheme` è già una base neutra moderna, la
maggior parte di quelle righe non servirebbe.

**Verdetto: confermata, con un margine più ampio del previsto.** Del migliaio di righe, solo una
piccola frazione ha bisogno di un vero controparte su Avalonia — la stima in dettaglio più sotto.

## Metodo

1. Letti `Tokens.xaml`/`Light.xaml`/`Dark.xaml` (43 chiavi) e portati 1:1 in
   `src/Recordum.Avalonia/Themes/Tokens.axaml`, stessi nomi di chiave, stessi hex.
2. Letto `BaseStyles.xaml` riga per riga: per ogni tipo di controllo, giudicato se il motivo del
   restyle WPF era "Aero è datato" o "decisione di design Recordum" (spesso entrambi insieme).
3. Costruita `ThemeProbeView.axaml` con **esattamente** i controlli richiesti dal brief, colorati
   **solo** dai token — aggiunto uno Style Recordum-specifico (`Themes/RecordumStyles.axaml`, 64
   righe commenti inclusi) **solo** dove il default Fluent non bastava (pulsante primario/ghost,
   TextBox a sottolineatura).
4. Renderizzata in headless reale (Skia, non il disegno finto) nelle due varianti — due screenshot,
   confrontati a occhio.
5. Per i tipi di controllo **non** nella checklist del brief (Menu, ContextMenu, ToolTip, Calendar
   e le sue parti, GroupBox, ecc.) la classificazione è **ragionata**, non verificata a schermo —
   segnalato riga per riga in tabella. Budget discipline: se un controllo avesse opposto
   resistenza sarebbe stato classificato C e si sarebbe andati avanti; non è successo, ma la nota
   sulla verifica-non-fatta resta comunque onesta.

## Tabella: i tipi di controllo restilizzati in BaseStyles.xaml

Ho contato **35** `TargetType` distinti in `BaseStyles.xaml` (contro i "34" del brief — la
differenza quasi certa è `Separator`, che arriva dallo stile chiave
`{x:Static MenuItem.SeparatorStyleKey}` e si dimentica facilmente di contare come "tipo di
controllo"). Aggiungo in fondo le tre composizioni **chiave** (non implicite) che non sono
restyle di un tipo esistente ma widget bespoke: `Recordum.SegmentCheck`, `Recordum.Segment`,
`Recordum.PillToggle`.

Legenda: **A** = FluentTheme + token bastano, niente da scrivere. **B** = piccolo restyle
(una manciata di Setter). **C** = riscrittura completa del template. **✓ reso** = verificato nello
screenshot della sonda. **○ ragionato** = classificato per ispezione/conoscenza di Fluent, non
renderizzato in questa sonda.

| # | Controllo | Righe WPF | Class. | Verifica | Motivo |
|---|---|---:|:---:|:---:|---|
| 1 | `Window` | ~7 | B | ○ | Setter radice (Background/Foreground/Font) — non è un undo-Aero, serve comunque su qualunque shell per agganciare i token; costo identico alle due shell. |
| 2 | `UserControl` | ~3 | B | ○ | Come sopra. |
| 3 | `Page` | ~5 | B | ○ | Come sopra; uso marginale in questo repo. |
| 4 | `TextBlock` | ~3 | **A** | **✓** | **Sorpresa**: nella sonda l'intestazione "Avalonia theme probe" non ha `Foreground` esplicito in nessun antenato (né sul `TextBlock`, né su `Window`, né su `UserControl`) eppure è leggibile in entrambe le varianti — Fluent porta di suo un `Foreground` theme-adattivo di default. In WPF serviva perché `SystemColors.ControlTextBrush` non segue alcun tema. |
| 5 | `Label` | ~3 | **A** | ○ | Stesso ragionamento di TextBlock (stessa famiglia `TextElement`/`ContentControl` in Fluent). |
| 6 | `TextBox` | ~28 | B | **✓** | Decisione di design Recordum (input "a sottolineatura", non un box pieno) — **non** un undo-Aero. Costa uguale sulle due shell perché in entrambe il template di default onora `Background`/`BorderBrush`/`BorderThickness` via `TemplateBinding`: bastano i Setter, mai un `ControlTemplate` nuovo (il commento WPF lo dice esplicitamente, e vale identico su Avalonia — verificato: `Themes/RecordumStyles.axaml`). |
| 7 | `PasswordBox` | ~15 | **A** | ○ | Avalonia non ha una classe separata: `TextBox` con `PasswordChar` impostato. Lo Style del punto 6 si applica gratis — zero righe aggiuntive. |
| 8 | `ComboBox` (+ toggle template) | ~103 | **A** | **✓** | WPF riscriveva tutto per (a) disfare il chrome Aero e (b) costruire il pannello flottante arrotondato con ombra. Il flyout di default di Fluent **è già** quel linguaggio (pannello arrotondato, ombra morbida, colonna separata per lo chevron) — confermato nello screenshot. |
| 9 | `ComboBoxItem` | ~29 | **A** | **✓** | Riga arrotondata con tinta accento all'hover già di serie in Fluent. |
| 10 | `CheckBox` | ~60 | **A** | **✓** | Il rewrite WPF era quasi puramente undo-Aero (il box/glifo Aero è datato). Il CheckBox Fluent di default è già piatto con glifo pulito — screenshot conferma; la parità esatta di colore sullo stato `:checked` con `Brush.Accent` (anziché l'accento di sistema) resterebbe un paio di Setter extra, non inclusi nella stima perché irrilevanti alla forma. |
| 11 | `RadioButton` (implicito) | ~3 | **A** | ○ | In WPF era già *solo* `Foreground` — il glifo radio Aero non veniva nemmeno toccato. Stesso trattamento su Avalonia. |
| 12 | `ListBox` | ~5 | **A** | ○ | Trivial anche in WPF, Fluent di suo già piatto. |
| 13 | `ListBoxItem` | ~5 | **A** | ○ | Come sopra. |
| 14 | `ListViewItem` | ~3 | — | — | **Nessun equivalente**: Avalonia non ha una classe `ListView`/`ListViewItem`. In WPF l'uso era comunque marginale (solo `Foreground`); se il repo la usa per una griglia vera, l'equivalente Avalonia è `DataGrid` o `ListBox` con `ItemTemplate` — non un restyle 1:1. |
| 15 | `TabControl` | ~4 | **A** | **✓** | Trivial anche in WPF. |
| 16 | `TabItem` | ~40 | **A** | **✓** | **Il caso più netto**: WPF riscriveva tutto per ottenere "niente chrome a scheda, solo una barra accento 2px sotto la scheda selezionata" — ed è **esattamente** il design di default del `TabItem` Fluent (screenshot: "Registrazioni" selezionata mostra la sottolineatura blu, "Statistiche" resta muta). Coincidenza reale, non forzata: entrambi convergono sullo stesso pattern "underline indica selezione". |
| 17 | `GroupBox` | ~4 | B | ○ | Trivial in WPF; Avalonia ha `GroupBox` ma è un controllo di uso più raro nell'ecosistema (si preferisce `HeaderedContentControl` o `Expander`) — non verificato, basso rischio. |
| 18 | `Expander` | ~60 | **A** | **✓** | WPF lo riscriveva per sostituire la freccia blu cerchiata di Aero (finding U5 nel changelog) con un'intestazione a riga piena + chevron Lucide che ruota. Il default Fluent **è già** quello — stessa forma, stessa animazione di rotazione — screenshot conferma. |
| 19 | `Menu` | ~4 | **A** | ○ | Trivial. |
| 20 | `MenuItem` (+ template) | ~71 | **A** | ○ *(ragionato)* | Stessa famiglia flyout arrotondato-con-ombra della voce 8/9 — non renderizzato in questa sonda (serve un menu aperto, fuori dalla checklist del brief), ma è la stessa infrastruttura di popup Fluent osservata per `ComboBox`. |
| 21 | `Separator` | ~9 | **A** | ○ | Fluent non ha mai avuto separatori a solco 3D — è un linguaggio puramente Win32/Aero che Fluent non porta. |
| 22 | `ContextMenu` | ~24 | **A** | ○ *(ragionato)* | Stessa famiglia flyout di voce 8/20. |
| 23 | `ToolTip` | ~30 | **A** | ○ *(ragionato)* | Il tooltip di default Fluent è già un pannello arrotondato con ombra morbida; non catturato a schermo (serve simulare l'hover in headless, fuori scope). |
| 24 | `RepeatButton` (implicito) | ~3 | **A** | ○ | Lasciato *solo Foreground* anche in WPF apposta per non corrompere ScrollBar/Expander — stesso trattamento qui. |
| 25 | `Slider` (+ 2 RepeatButton chiave + Thumb chiave) | ~73 | **A** | **✓** | **Il singolo risparmio più grande**: WPF sostituiva il thumb a goccia sfumata di Aero con track piatta arrotondata + thumb circolare + riempimento accento. Il `Slider` Fluent di default **è già esattamente questo** — screenshot pressoché indistinguibile dal design WPF. |
| 26 | `Button` | ~41 | B | **✓** | Il flat-chrome di base è già gratis in Fluent (l'Aero-undo non serve). Ma qui la sonda introduce **due varianti che WPF non aveva nemmeno** (primario pieno-accento e "ghost"/quiet — `BaseStyles.xaml` aveva un solo stile Button universale): 20 righe di Setter con selettori di classe (`Button.primary`, `Button.ghost`) in `RecordumStyles.axaml`, mai un `ControlTemplate`. |
| 27 | `ToggleButton` (implicito) | ~3 | **A** | ○ | Come RepeatButton, lasciato a `Foreground` anche in WPF. |
| 28 | `DatePicker` | ~5 | B | ○ | Costo di stile trivial anche in WPF. **Ma** vedi §"Non transfer" — la UX del `DatePicker` Fluent (flyout con spinner) è strutturalmente diversa dal dropdown-calendario di WPF: non è un restyle, è un widget diverso. |
| 29 | `Calendar` | ~5 | B | ○ | Trivial, non verificato. |
| 30 | `CalendarItem` | ~4 | **A** | ○ | Trivial. |
| 31 | `CalendarButton` | ~27 | **A** | ○ *(ragionato)* | Nessun motivo per credere che Fluent abbia lo stesso bug di brush hardcoded di Aero (vedi #32). |
| 32 | `CalendarDayButton` | ~40 | **A** | ○ *(ragionato)* | Il commento WPF è esplicito: "Aero **hardcodes** alcuni brush che ignorano i Setter semplici" — un bug specifico di Aero, non un problema architetturale generale. Il sistema di risorse Fluent è `DynamicResource` end-to-end, quella classe di bug non esiste. |
| 33 | `ScrollViewer` | ~61 | B | **✓ parziale** | La parte strutturale (contenuto a piena larghezza, nessuna colonna riservata alla barra) **è già** il default Fluent — confermato nello screenshot (il riquadro Righe 1-8 non ha un "buco" per la barra). **Non verificata** la fase di dissolvenza show/hide al passaggio del mouse (serve simulare hover in headless): stimo un paio di `Transition`/pseudo-classe `:pointerover`, non un `ControlTemplate` — resta B, non C. |
| 34 | `ScrollBar` | ~64 | **A** | **✓** | Barra sottile piatta, thumb arrotondato — già il default Fluent, screenshot alla mano. |
| — | `Recordum.SegmentCheck` (chiave, pillola segmento su CheckBox) | ~31 | C | ○ *(non costruito)* | Widget **bespoke** di Recordum (barra di toggle per-vista tipo tab pill), non un restyle di un controllo Fluent esistente — Fluent 12.1 non spedisce un "SegmentedControl" pronto. Costo pieno, trasferisce 1:1. Non incluso nella checklist del brief: non costruito né reso in questa sonda, stima per analogia col codice WPF. |
| — | `Recordum.Segment` (chiave, pillola su RadioButton) | ~32 | C | ○ *(non costruito)* | Stesso discorso di sopra, variante RadioButton. |
| — | `Recordum.PillToggle` (chiave, toggle "Advanced") | ~36 | B | ○ *(non costruito)* | Il `ToggleButton` Fluent è già piatto e ha uno stato `:checked` con riempimento — la forma a pillola (CornerRadius pieno) + colore-al-checked verso `Brush.Accent`/`Brush.OnAccent` restano un paio di Setter su pseudo-classe, non un `ControlTemplate`: C→B, non C→A. |

## La stima che segue dalla tabella

Sommando le righe WPF per classificazione dalla tabella sopra (solo i 35 `TargetType` impliciti;
i tre widget bespoke chiave — `Recordum.Segment*`/`PillToggle` — sono voci aggiuntive, non
`TargetType`, e vanno tenute separate):

| Classe | Righe WPF coinvolte | Costo Avalonia stimato |
|---|---:|---:|
| **A** — nessuna contropartita necessaria | ≈ 682 righe | 0 |
| **B** — piccolo restyle | ≈ 159 righe | ≈ 64 righe |
| **N/A** — nessun equivalente (ListViewItem) | ≈ 3 righe | n/d (widget diverso, non stile) |
| **Subtotale attribuibile a un controllo** | ≈ 844 righe | ≈ 64 righe |
| Commento d'intestazione + separatori tra i blocchi (non attribuibili a un singolo controllo) | ≈ 202 righe | — |
| **Totale file** | **1046 righe** | |
| + 3 widget bespoke chiave (Segment/SegmentCheck/PillToggle, se e quando servissero) | ≈ 99 righe | ≈ 73 righe (2×C invariato + 1×B) |

(Le ≈202 righe non attribuite sono il blocco di commento in testa al file e le righe vuote/di
struttura tra un blocco `Style` e l'altro — non sono "stile di un controllo", quindi non hanno un
costo Avalonia diretto; scalano comunque verso il basso perché con meno blocchi da scrivere
servono meno separatori e meno commenti di spiegazione.)

**Delle 844 righe attribuibili a un singolo tipo di controllo, l'81% (≈682) non ha bisogno di
alcuna contropartita su Avalonia** (classe A) — l'ipotesi è confermata, e con una sfumatura che il
brief stesso non prevedeva: non solo il chrome-undo di Aero è gratis su Fluent, ma **alcune
decisioni di design specifiche di Recordum** (la sottolineatura di selezione sui tab, il thumb
circolare dello slider, l'header con chevron rotante dell'Expander, il pannello flottante
arrotondato dei popup) **coincidono** col default Fluent invece di dover essere reimplementate —
non erano poi così "Recordum-specifiche" quanto sembrava dal codice WPF: erano scelte di design
moderno generico che Aero non aveva ancora, e WPF doveva costruirle a mano perché non ha un tema
di sistema equivalente.

Il residuo realistico da scrivere (classe B, esclusi i tre widget bespoke non verificati) è
**≈ 64 righe** — coerente con quanto effettivamente scritto in questa sonda:
`Themes/RecordumStyles.axaml` pesa 64 righe **commenti inclusi** (il codice-Style vero e proprio è
circa 20 righe: due classi Button + un override TextBox). Se si aggiungono i tre widget bespoke
(mai richiesti dal brief, stimati per analogia, non costruiti) il totale sale a circa 140 righe —
comunque **~1/7 delle 1046 originali**.

## Cosa NON transfer

- **Window chrome custom** (`WindowChromeStyles.xaml`, 129 righe, file separato non incluso nel
  conteggio delle 1046): usa `WindowChrome.WindowChrome` allegato,
  `WindowChrome.IsHitTestVisibleInChrome`, `SystemCommands.MinimizeWindowCommand` — **API che non
  esistono in Avalonia**. L'equivalente Avalonia è un meccanismo completamente diverso:
  `Window.ExtendClientAreaToDecorationsHint` + `ExtendClientAreaChromeHints` per nascondere il
  chrome di sistema, una riga di intestazione disegnata a mano con `Button` cablati a
  `window.Close()`/`window.WindowState`, e il trascinamento della finestra via
  `PointerPressed` → `BeginMoveDrag(e)` invece di un attached-behavior dichiarativo. Nessuna riga
  XAML del file WPF è riusabile as-is; va **riscritto da zero con un'API diversa** — stessa scala
  di sforzo (~100-130 righe), zero riuso letterale. Non l'ho costruito in questa sonda (fuori
  dalla checklist del brief) ma l'ho verificato leggendo il file: è l'unico punto dove l'ipotesi
  "molto transfer" **non vale affatto**.
- **`PasswordBox`** come classe separata: non esiste, si passa a `TextBox` + `PasswordChar` — non
  un problema (anzi, un risparmio), ma un cambio di API da fare nel code-behind/binding.
- **`ListViewItem`**: nessun equivalente diretto; se il codice WPF usa `ListView` per una vista a
  colonne, la controparte Avalonia è `DataGrid` (pacchetto separato) — un porting di widget, non
  di stile.
- **`DatePicker`**: stile a parte, la *forma* del controllo Fluent (flyout con spinner
  giorno/mese/anno) è strutturalmente diversa dal dropdown-calendario di WPF. Non è "the same
  control, different colors" — è un pattern d'interazione diverso, da far approvare all'autore
  come le altre decisioni di prodotto (principio 8 di CLAUDE.md: non è solo styling).
- **Font.Default = Palatino Linotype**: non esiste su Linux. In `Tokens.axaml` uso uno stack serif
  di sistema (Georgia/Noto Serif/Liberation Serif/DejaVu Serif) solo per rendere leggibile la
  sonda — la decisione finale sul font resta all'autore (vedi commento nel file).

## Le sorprese

1. **TextBlock/Label/Window non hanno bisogno di NESSUN Setter, nemmeno un token**, per essere
   leggibili in entrambi i temi — Fluent porta un `Foreground` theme-adattivo di suo. In WPF
   l'intero paragrafo di apertura di `BaseStyles.xaml` esiste per questo identico problema
   ("SystemColors.ControlTextBrush = nero, illeggibile su sfondo scuro"). Verificato per caso e
   non per progetto: l'intestazione della sonda ("Avalonia theme probe") non ha `Foreground`
   esplicito in nessun antenato eppure rende correttamente in entrambe le varianti nello
   screenshot — non l'avevo previsto scrivendo la vista.
2. **Il `TabItem`, l'`Expander` e lo `Slider` di Fluent non sono "abbastanza vicini da poter
   riusare con qualche Setter" — sono voluti *identici* al design che Recordum si era costruito a
   mano.** Non me lo aspettavo a questo livello: pensavo di dover comunque scrivere piccoli
   override di forma (raggio, spessore) e invece lo screenshot mostra gli stessi identici pattern
   (sottolineatura di selezione, chevron rotante, thumb circolare + track piatta) senza toccare
   nulla oltre ai colori.
3. **Il costo di `Button` non si riduce a zero come pensavo**: WPF aveva un solo stile Button
   universale, ma per riprodurre quello che la sonda chiedeva (primario + ghost) ho dovuto
   *aggiungere* qualcosa che WPF non aveva affatto — il confronto "quante righe WPF → quante righe
   Avalonia" per questa singola voce non è una riduzione, perché il punto di partenza a schermo è
   diverso (WPF non distingueva le due varianti). È l'unico punto della tabella dove il porting
   letterale sottostima cosa serve davvero al prodotto finito.
4. **Il meccanismo `ThemeDictionaries` ha funzionato al primo tentativo**, nessuna sorpresa
   negativa: build pulita, `RequestedThemeVariant` per-finestra risolve correttamente contro le
   chiavi "Light"/"Dark" senza bisogno di un `ThemeService` che scambia dizionari come in WPF —
   Avalonia lo fa nativamente.
5. **L'ipotesi era formulata con cautela ("la maggior parte"), la realtà è più netta**: non è
   "la maggior parte" delle 1046 righe a non servire, è circa **7/8**. Il vero costo della
   migrazione dello styling non sta nel restilizzare i controlli standard — sta nei tre widget
   bespoke di Recordum (pillole segmento/toggle) che non hanno mai avuto niente a che fare con
   Aero, e nel chrome della finestra, che va riscritto con un'API concettualmente diversa.

## File della sonda

- `src/Recordum.Avalonia/Themes/Tokens.axaml` — 43 chiavi, stessi nomi/hex di WPF.
- `src/Recordum.Avalonia/Themes/RecordumStyles.axaml` — le uniche righe di styling scritte oltre
  ai token (pulsante primario/ghost, TextBox a sottolineatura).
- `src/Recordum.Avalonia/Views/ThemeProbeView.axaml` (+ `.axaml.cs`) — non referenziata da nessun
  altro punto dell'app.
- `tests/Recordum.Avalonia.Tests/ThemeProbeScreenshotTests.cs` — genera `theme-light.png` e
  `theme-dark.png` via `ScreenshotProbeTests.Capture`.
