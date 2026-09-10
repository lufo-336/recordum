# Sonda controlli Avalonia — quanto costa portare i controlli hand-built di Recordum

**Branch**: `avalonia-linux-slice`. **Tipo**: probe/spike, non una feature — l'output è una stima
misurata, non codice di produzione. Niente sotto `src/Recordum.WPF/` è stato toccato.

## Premessa (già misurata, non ri-derivata)

Nessun controllo di Recordum usa il rendering a modalità immediata — zero `OnRender`,
`DrawingContext`, `DrawingVisual` in `src/Recordum.WPF`. Sono tutti "calcola la geometria in C#,
poi metti forme in un panel". `HeatmapStrip` (Views/HeatmapStrip.xaml+.cs, 21+169 righe) è il
caso peggiore — più code-behind, più logica di calcolo degli altri quattro — quindi è quello che
si è portato per intero; per gli altri quattro si è fatto un *desk check* mirato: letto il
sorgente WPF fino in fondo, verificate le API Avalonia equivalenti nel pacchetto referenziato
(non a memoria), classificato senza costruire.

## 1. Cosa è costata davvero la traduzione di HeatmapStrip

**File**: `src/Recordum.Avalonia/Controls/HeatmapStrip.axaml` (19 righe) +
`HeatmapStrip.axaml.cs` (205 righe). WPF: 21 + 169 = 190 righe totali → Avalonia: 19 + 205 = 224
(+34, +18%). Ma la crescita è quasi tutta commento: contando solo righe non vuote e non
puramente-commento nel `.cs`, WPF 127 → Avalonia 135 (+8 righe, +6%) — la logica di calcolo è
**identica riga per riga** (stesso switch su `StripStyle`, stesse costanti `TotalWidth`/
`TrackHeight`, stesso `BandRadius`). Il resto della crescita è commenti che spiegano le
differenze qui sotto, non richiesti dal compilatore.

### `DependencyProperty` → `StyledProperty`

Le quattro proprietà (`Counts`, `Fractions`, `Kind`, `AccentColor`) sono diventate
`StyledProperty<T>`, non `DirectProperty<T>`. La scelta è quella giusta per il caso d'uso, non
solo la più comoda: tutte e quattro sono pensate per essere impostate **da fuori** (binding da un
`HomeRow` ViewModel, styling, template, animazione) — è esattamente lo scenario per cui Avalonia
prevede `StyledProperty`. `DirectProperty` è pensata per proprietà sostenute da un campo CLR e
lette/scritte perlopiù dal controllo stesso (serve a evitare l'overhead della property-value-store
quando il valore cambia molto spesso *da dentro*, es. proprietà calcolate ad ogni frame) — non è
questo il caso qui, dove il "produttore" del valore è sempre il chiamante.

Il callback di cambiamento — `PropertyMetadata(..., OnChanged)` in WPF — diventa la sottoscrizione
`XxxProperty.Changed.AddClassHandler<HeatmapStrip>((strip, _) => strip.Rebuild())` nel costruttore
statico: stessa idea (un solo punto che richiama `Rebuild()` per tutti e quattro i property-changed
in blocco), meccanismo diverso (evento reattivo invece di un delegato passato a `PropertyMetadata`).

### `System.Windows.Media` → `Avalonia.Media`

Mappatura pressoché 1:1 per quello che serve qui: `SolidColorBrush`, `Color.FromArgb`,
`Thickness`, `CornerRadius` (stesso ordine di argomenti — TopLeft/TopRight/BottomRight/BottomLeft
— nessuna sorpresa), `VerticalAlignment` (stessi valori enum, namespace `Avalonia.Layout` invece
di `System.Windows`). Un'unica differenza reale: **`ColorConverter.ConvertFromString` →
`Color.Parse`** — stessa sintassi hex accettata (`"#RRGGBB"`, alpha implicito FF), quindi zero
logica da riscrivere, solo il nome della chiamata. **Nessun equivalente di `Brush.Freeze()`**:
Avalonia non ha il pattern `Freezable` di WPF (l'ottimizzazione che rende un oggetto altrimenti
mutabile immutabile e condivisibile tra thread) — qui non serve nulla, il brush non viene più
mutato dopo la creazione, quindi la riga sparisce senza perdita.

### Differenza di panel/composizione: niente `ItemsControl`+`DataTemplate`

Questa è la scelta di traduzione più visibile, e **deliberata, non forzata da un blocco tecnico**.
L'originale WPF usa un `ItemsControl` con `ItemsPanel=StackPanel` orizzontale e un `DataTemplate`
a 6 binding sopra un `record Cell` creato apposta per essere "bindabile" (`Width`/`Height`/
`Margin`/`Radius`/`VAlign`/`Brush`). Il progetto Avalonia ha
`AvaloniaUseCompiledBindingsByDefault=true`: un `ItemTemplate` del genere richiederebbe un
`x:DataType` che punti al record `Cell` (renderlo pubblico solo per questo) oppure
`x:CompileBindings="False"` sul template, perdendo la verifica di binding a compile-time che il
resto del progetto ha. Dato che i valori sono già calcolati in C# — non c'è nessun dato "vivo" da
tenere sincronizzato con l'elemento visuale dopo la creazione, la cella non cambia più una volta
disegnata — un pannello riempito direttamente in code-behind (`StackPanel.Children`) è la via più
diretta: stessa forma finale a schermo, zero indirezione di binding per celle statiche. `Rebuild()`
quindi non produce più una `List<Cell>` intermedia: costruisce `Border` già completi
(`MakeCell(...)`) e li aggiunge/toglie direttamente da `Children`.

### La sorpresa vera: `AvaloniaXamlLoader.Load(this)` non è sufficiente

Non è nella lista dei "differenza di meccanismo attesa" del brief, ma è stato il vero costo in
tempo di questa sonda (vedi §4 per il dettaglio investigativo). Riassunto: Avalonia ha **due**
generatori indipendenti coinvolti nella compilazione XAML — il compilatore XamlIl (che produce
`InitializeComponent(bool loadXaml = true)`, un metodo che fa `AvaloniaXamlLoader.Load(this)` **e
poi** cabla ogni campo `x:Name` dal `NameScope`) e un secondo Roslyn incremental generator
(`Avalonia.Generators.NameGenerator`, nel package `Avalonia` stesso) che dichiara i campi. Il
boilerplate copiato da `MainWindow.axaml.cs`/`ThemeProbeView.axaml.cs` (già nel repo prima di
questa sonda) chiama `AvaloniaXamlLoader.Load(this)` **direttamente**, saltando il cablaggio dei
campi. Per `HeatmapStrip` questo produceva una `NullReferenceException` a runtime (non un errore
di compilazione) alla prima cella da disegnare. Fix: chiamare `InitializeComponent()` invece di
`AvaloniaXamlLoader.Load(this)` nel costruttore — un dettaglio da manuale Avalonia, ma il
boilerplate esistente nel repo lo ignorava (e lo ignora tuttora: `MainWindow`/`ThemeProbeView` non
leggono mai il loro campo nominato, quindi il bug è latente lì, non visibile).

### Cosa NON è cambiato

`Math.Max`/`Math.Min`/operatori aritmetici, lo `switch` espressione su `StripStyle`, la logica di
`BandRadius` (arrotonda solo i bordi esterni di un run coperto), i valori numerici (`TotalWidth =
96`, `TrackHeight = 16`, soglie di alpha) — tutto identico, copiato senza modifiche concettuali.

## 2. Estrapolazione agli altri quattro controlli

| Controllo | Righe WPF (xaml+cs) | Verdetto | Motivo |
|---|---:|:---:|---|
| `YearHeatmapControl` | 23+8 | **A — quasi gratis** | È **puro XAML dichiarativo**: `ItemsControl` con `ItemsSource="{Binding Cells}"`, `ItemsPanel=UniformGrid Rows="7" Columns="53"`, `DataTemplate DataType="{x:Type vm:HeatmapCellViewModel}"` con un `IValueConverter` (`IntensityStepToBrush`) e un `ToolTip`. Il code-behind è 8 righe (solo `InitializeComponent()`). Verificato nel package referenziato: `Avalonia.Controls.UniformGrid` **esiste** (non è scontato — non tutti i panel WPF hanno un equivalente Avalonia di serie), quindi il panel si porta senza sostituzioni. Il costo reale è tutto nel binding: serve un `x:DataType` esplicito sul `DataTemplate` (compiled bindings) — una riga, non zero come in WPF, ma comunque un'aggiunta meccanica — e il converter passa da `System.Windows.Data.IValueConverter` a `Avalonia.Data.Converters.IValueConverter` (stessa forma `Convert`/`ConvertBack`, namespace diverso, porting a costo quasi nullo). `ToolTipService.InitialShowDelay` → `ToolTip.ShowDelay` (proprietà allegata diversa, stesso effetto). Nessun code-behind da riscrivere: è il controllo **più semplice** dei cinque, nonostante sia il secondo per complessità visiva. |
| `StreakTimelineControl` | 6+45 | **A — diretto** | `Canvas` popolato su `SizeChanged`, con `Rectangle` (`RadiusX`/`RadiusY` per gli angoli arrotondati), `Canvas.SetLeft`/`SetTop`, `FindResource("Brush.Accent")`. Verificato nel package: **tutte** queste API hanno un equivalente diretto in Avalonia — `Control.SizeChanged` esiste (evento, stessa firma concettuale), `Avalonia.Controls.Shapes.Rectangle.RadiusX`/`RadiusY` esistono (non scontato: Avalonia tiene i controlli-forma più snelli di WPF in generale, ma qui il caso specifico è coperto), `Canvas.SetLeft`/`SetTop` sono le stesse attached property, `ResourceNodeExtensions.FindResource(IResourceHost, object)` è l'analogo diretto. L'unica differenza reale: il `Rectangle.ToolTip = "testo"` di WPF (proprietà diretta su qualunque `FrameworkElement`) diventa l'attached property `ToolTip.SetTip(rect, testo)` in Avalonia — un cambio meccanico, stesso pattern già visto in `YearHeatmapControl`. **Sorpresa positiva**: leggendo il codice mi aspettavo che un `Canvas` con `Rectangle` a raggio fosse il candidato più a rischio (forma "quasi-drawing"); si è rivelato invece il porting più meccanico dei quattro, perché ogni singola API usata ha un omologo diretto nel pacchetto già referenziato. |
| `TrendChartControl` / `HourlyHistogramControl` (wrapper) | 12+70 / 12+58 | **B — non un semplice swap di pacchetto, ma vicino** | I due wrapper sono quasi identici: un `Grid` con `ContentControl x:Name="ChartHost"` + un `TextBlock` per lo stato vuoto, e un code-behind che si aggancia a `DataContextChanged`/`Loaded`/`Unloaded` per ri-hookare il ViewModel e chiamare `Rebuild()`, che poi fa `ChartHost.Content = TrendChartBuilder.Build(...)`. Il porting del *wrapper* stesso è diretto: `Loaded`/`Unloaded`/`DataContextChanged` esistono su `Avalonia.Controls.Control` con la stessa semantica, `ContentControl.Content = object` funziona identico — ma **il ContentControl statico va accoppiato a un selector/content-null-guard**, la regola già scritta in CLAUDE.md dopo C1/W24-Q1 in WPF→WPF stessa app; qui vale il principio gemello lato Avalonia (`ContentTemplate` implicito di `ContentControl` per un `object` generico non scatta comunque, quindi il rischio è più basso che nel caso Diario, ma va verificato). **Il vero costo non è nel wrapper, è nel motore dietro**: ho letto `ChartEngineRenderer.cs` (`Recordum.Plugins.ChartEngine`) riga per riga. La mappatura serie→modello (`MapSeries`, ~35 righe) usa **solo** tipi di `LiveChartsCore`/`LiveChartsCore.SkiaSharpView` (`ISeries`, `ColumnSeries<T>`, `LineSeries<T>`, `SolidColorPaint`, `SKColor`) — **zero** riferimenti a `LiveChartsCore.SkiaSharpView.WPF` — quindi è già portabile parola per parola. Il punto di rottura è `CreateView(...)`: **due righe** istanziano `LiveChartsCore.SkiaSharpView.WPF.CartesianChart`/`PieChart` esplicitamente. Il pacchetto `LiveChartsCore.SkiaSharpView.Avalonia` 2.0.5 esiste su NuGet (verificato dal brief, non installato qui per vincolo del probe) e offre gli stessi tipi `CartesianChart`/`PieChart`/`Axis` con la stessa superficie di proprietà (`Series`, `XAxes`, `YAxes`, `LegendPosition`) — quindi il cambio in `CreateView` è **letteralmente la sostituzione del namespace `.WPF` con `.Avalonia` su due righe**. Quello che rende questo "B" e non "A": il progetto `Recordum.Plugins.ChartEngine.csproj` è `net10.0-windows` + `UseWPF=true` — non compila per Avalonia così com'è. Serve o (a) un secondo progetto sorella `Recordum.Plugins.ChartEngine.Avalonia` che duplica `MapSeries`/`Paint` (violando riuso, ma zero rischio) o (b) — meglio — estrarre la mappatura pura (già priva di dipendenze WPF) in un progetto condiviso `net10.0` referenziato da due renderer sottili (`CreateView` soltanto), uno per shell. Non è un rewrite, ma è **un progetto e una decisione di architettura in più rispetto a un puro swap di pacchetto** — da qui il verdetto B invece di A. |

**Totale extrapolato**: dei quattro controlli restanti, **tre su quattro** (`YearHeatmapControl`,
`StreakTimelineControl`, e il wrapper nudo di `TrendChartControl`/`HourlyHistogramControl`) sono
porting diretti — stessa classe di costo di `HeatmapStrip` o meno, perché più dichiarativi e con
meno stato interno da ricostruire. L'unico pezzo che costa più di un porting 1:1 è il **motore**
dietro i due grafici (il pacchetto NuGet Avalonia esiste già, il costo è architetturale — separare
la mappatura pura dal renderer platform-specific — non di riscrittura).

## 3. Cosa ha sorpreso

**La sorpresa più grande non era nel brief e non riguarda la logica dei cinque stili — riguarda il
boilerplate già nel repo.** `AvaloniaXamlLoader.Load(this)`, il pattern usato sia in
`MainWindow.axaml.cs` sia in `ThemeProbeView.axaml.cs` (probe precedente, styling), **non cabla i
campi `x:Name`** — serve chiamare il metodo generato `InitializeComponent()`, che fa
`Load(this)` e poi il cablaggio via `NameScope`. Ho perso tempo reale a diagnosticarlo: il campo
esiste (visibile via reflection, tipo corretto), il `NameScope` lo trova subito
(`NameScope.GetNameScope(this).Find("Cells")` funziona), il `Content` stesso è popolato
correttamente — ma il campo nudo resta `null` finché non si chiama `InitializeComponent()`. Ho
dovuto scrivere ed eseguire tre test-sonda usa-e-getta (reflection sui campi, ispezione del
`NameScope`, cast diretto di `Content`, e infine forzare `EmitCompilerGeneratedFiles=true` per
leggere il `.g.cs` del generatore e trovare la riga esatta) prima di capire la causa — un
`NullReferenceException` a runtime, senza nessun errore né warning a compile-time, in un pattern
che il repo stesso usa già altrove senza accorgersene (perché lì il campo nominato non viene mai
letto). **Se il porting reale copierà il boilerplate di `MainWindow.axaml.cs` per i nuovi
controlli — cosa probabile — questo bug si ripresenterà identico su ogni controllo con almeno un
elemento nominato**, quindi vale la pena fissarlo ora nel template/scaffold piuttosto che
riscoprirlo per ognuno dei quattro controlli restanti.

**Sorpresa minore, in positivo**: `StreakTimelineControl` — quello che sulla carta sembrava il più
"vicino al drawing manuale" per via del `Canvas`+`Rectangle` — si è rivelato il porting più pulito
dei quattro non ancora costruiti, perché ogni singola API che usa (`SizeChanged`, `RadiusX`/
`RadiusY`, `Canvas.SetLeft/SetTop`, `FindResource`) ha un omologo diretto in Avalonia, verificato
nel pacchetto referenziato e non per assunzione.

**Nessuna delle cinque forme ha opposto vera resistenza concettuale.** La parte "difficile" di
`HeatmapStrip` (il calcolo geometrico per tipo — barre, banda con strappi, ramp) è rimasta
byte-per-byte la stessa; l'unico attrito è stato di API/toolchain (property system, il
loader-gate di cui sopra), non di logica. Se il piano di migrazione stava preventivando rischio
sui cinque stili visivi, quel rischio non c'è — il rischio reale (piccolo, ma reale) è nel
boilerplate di scaffolding dei controlli, non nel loro contenuto.

## 4. Metodo (per chi rilegge la sonda)

1. Letto `Views/HeatmapStrip.xaml(.cs)` per intero prima di scrivere una riga.
2. Portato in `src/Recordum.Avalonia/Controls/HeatmapStrip.axaml(.cs)`, stessa logica, note nei
   commenti dove il meccanismo differiva (§1).
3. Costruita `HeatmapStripProbeView.axaml(.cs)` — cinque righe (Action/Counter/Resistance/
   Journal/State), valori non piatti apposta (v. brief), montate su `ContentControl` nominati per
   evitare la complessità di binding di un `ImmutableArray<double>` in XAML letterale.
4. **Trovato il bug del `Load(this)`** costruendo il test screenshot: `NullReferenceException` al
   primo `Rebuild()`. Diagnosticato con tre test-sonda scartati (reflection sui campi, ispezione
   `NameScope.GetNameScope`, cast diretto di `Content`) più un build con
   `EmitCompilerGeneratedFiles=true`/`CompilerGeneratedFilesOutputPath` per leggere il `.g.cs` di
   `Avalonia.Generators.NameGenerator.AvaloniaNameIncrementalGenerator` e trovare la riga esatta
   che manca (`Cells = __thisNameScope__?.Find&lt;StackPanel&gt;("Cells")`, dentro
   `InitializeComponent()`, mai in `Load(this)`).
5. Corretto sia `HeatmapStrip` sia `HeatmapStripProbeView` per chiamare `InitializeComponent()`.
6. `tests/Recordum.Avalonia.Tests/HeatmapStripScreenshotTests.cs` — 2 `[AvaloniaFact]`, stesso
   pattern di `ThemeProbeScreenshotTests.cs` (finestra 480×420, `RequestedThemeVariant` Light/
   Dark, cattura reale via Skia con `ScreenshotProbeTests.Capture`). Suite completa: **6/6 verdi**
   (4 preesistenti + 2 nuovi).
7. Letti per intero `YearHeatmapControl.xaml(.cs)`, `StreakTimelineControl.xaml(.cs)`,
   `TrendChartControl.xaml.cs`, `HourlyHistogramControl.xaml.cs`, `ChartEngineRenderer.cs` e il
   `.csproj` di `Recordum.Plugins.ChartEngine` per il desk check di §2 — nessuno di questi è stato
   costruito, come da vincolo del probe.

## Verdetto complessivo

I cinque controlli hand-built di Recordum **non sono il costo grosso della migrazione**: quattro
su cinque (`HeatmapStrip` costruito, `YearHeatmapControl`/`StreakTimelineControl`/i due wrapper
grafico desk-checked) sono porting diretti, stessa taglia o più piccoli dell'originale WPF, con
zero riscritture concettuali. Il quinto pezzo — il motore LiveCharts2 dietro i grafici — richiede
un pacchetto NuGet già disponibile più una separazione architetturale (mappatura pura vs. view
platform-specific) che oggi non esiste, ma non un rewrite. Il rischio reale scoperto da questa
sonda non è nei controlli: è nel boilerplate di scaffolding (`AvaloniaXamlLoader.Load` vs
`InitializeComponent()`) già presente due volte nel repo e silenzioso finché qualcosa non legge un
campo nominato.
