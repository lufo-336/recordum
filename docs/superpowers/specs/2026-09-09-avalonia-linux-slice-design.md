# Recordum su Linux — fetta verticale Avalonia: design

> **⚠️ SUPERATO NELLO SCOPE (2026-09-10).** Questo spec descrive una *fetta verticale per Linux*.
> Dopo le quattro sonde di misura l'autore ha deciso che **Avalonia è la piattaforma** — sostituisce
> WPF e assorbe la Fase 5 (MAUI) e la futura Web/PWA. Le misure che devono guidare la riscrittura
> stanno in `docs/design/2026-09-09-avalonia-theme-probe.md`,
> `2026-09-09-avalonia-controls-probe.md`, `2026-09-09-avalonia-editor-probe.md` e
> `2026-09-10-avalonia-live-preview-probe.md`, riassunte in CLAUDE.md §7.
> Resta valido quanto già eseguito (Task 1-2 del piano); **i Task 3-9 non vanno eseguiti come scritti**.

> **Stato**: approvata in brainstorming il 2026-09-09. Il piano d'implementazione si scrive da questa spec.
> **Non è** il porting dell'applicazione: è la fetta che decide se il porting vale la pena, e a che costo.

---

## 1. Perché, e perché adesso

Recordum si descrive come *"open-source, offline-first, cross-platform habit editor"*, ma spedisce solo su
Windows. L'autore vuole che un utente Linux possa installarlo — è la ragione principale, davanti al voler
usare l'app da questa macchina e al volerla mostrare senza accendere la VM.

**Costo dichiarato**: rallenta l'arrivo alla v1.0, e contraddice il freeze del 2026-06-16 ("nessuna nuova
fase finché il desktop non è una v1.0 funzionante e gradevole"). Decisione dell'autore, presa
consapevolmente: si sblocca come già successe per la workspace shell.

**Modo di lavorare concordato**: quando la sessione gira su Linux si porta avanti questa linea; il codice
della v1.0 mantiene una leggera priorità quando c'è qualcosa che si può chiudere. La VM resta la macchina
della v1.0, questa diventa la macchina della portabilità.

**Sul sync non si apre un fronte.** Il ponte dati esiste già ed è `.recbak` (Argon2id + AES-256-GCM,
Milestone 3.10b) — la roadmap lo usa negli stessi termini per la Fase 5 mobile: *"dati locali; ponte dati
via export/import `.recbak`"*. Finché non arriva la Fase 6, Linux e Windows sono due archivi separati che
si scambiano un file. Nessuna eccezione inventata per l'occasione.

## 2. La misura che rende il progetto plausibile

Tre fatti **misurati su questa macchina il 2026-09-09**, non dedotti:

1. **Gli agganci a WPF nei ViewModel sono sei righe in sei file**, su 126 file e 12.300 righe:
   - `new DispatcherTimer` in `HomeClockTicker`, `LiveElapsedClock`, `AbstinenceQuickAddViewModel`,
     `ChartBuilderViewModel` — tutti già protetti da `Application.Current is not null`, perché i test
     girano headless;
   - `Application.Current?.Shutdown()` in `MainWindowViewModel:144`;
   - `TryFindResource("Brush.Danger") as SolidColorBrush` in `HabitScreenViewModel:963`.

   Il primo gruppo non è nemmeno una differenza di modello: **Avalonia ha `DispatcherTimer` con lo stesso
   nome e la stessa forma**, in `Avalonia.Threading`.

2. **Lo strato portabile gira davvero**: `Recordum.Core` (143 test), `Recordum.Data` (478 + 18 DPAPI
   Windows-only per natura), `Recordum.Plugins` (66) e `Recordum.Plugins.ExportEngine` (37) compilano ed
   eseguono su Fedora. **SQLCipher funziona** (6/6, incluso il guard `PRAGMA cipher_version`): il database
   cifrato non è una cosa Windows.

3. **I percorsi sono già a norma**: `Environment.SpecialFolder.LocalApplicationData` su Linux risolve
   `/home/<utente>/.local/share`, quindi `RecordumOptions.Default()` scrive in
   `~/.local/share/Recordum` **senza una riga di modifica**.

Questa è l'assicurazione che la disciplina degli strati (CLAUDE.md §4) aveva pagato senza sapere per cosa.
Non è fortuna: è il motivo per cui scegliere WPF nel 2026 non è stato un errore.

## 3. Perché Avalonia e non le alternative

| Strada | Esito | Perché |
|---|---|---|
| **Avalonia 12.1.2** | **scelta** | XAML parente stretto di WPF, stesso `CommunityToolkit.Mvvm`, stesso MVVM. I ViewModel non si toccano: cambia la resa, non la logica. Linux + Windows + macOS dallo stesso progetto |
| Uno Platform 6.7 | scartata | Copre più piattaforme, ma il baricentro è WinUI/WASM e per una app XAML desktop esistente impone **due** traduzioni (WPF→WinUI→Skia) invece di una |
| Web/PWA (Blazor) | rimandata | È già in roadmap fra le fasi future e darebbe Linux+mobile+web insieme, ma cambia il prodotto: un'app in una scheda del browser non è il desktop del design brief, e butterebbe via **anche** i ViewModel |
| .NET MAUI | esclusa | Non fa desktop Linux (Android/iOS/Mac/Windows) |
| GTK# | esclusa | Riscrittura in un paradigma senza XAML né MVVM |
| Electron / Tauri | esclusa | Butta via tutto il C# di interfaccia |

**Dipendenze verificate su NuGet** (2026-09-09):

| Serve per | Pacchetto | Versione |
|---|---|---|
| Shell | `Avalonia`, `Avalonia.Desktop` | 12.1.2 |
| Test UI headless | `Avalonia.Headless.XUnit` | 12.1.2 |
| Grafici | `LiveChartsCore.SkiaSharpView.Avalonia` | 2.0.5 |
| Editor markdown | `Avalonia.AvaloniaEdit` 12.0.0 + `Markdown.Avalonia` 11.0.3 | — |

**Nota che vale oltre questa fetta**: sull'editor markdown la versione Linux **non porta WebView2, lo
elimina**. Un intero processo browser dentro la finestra — che ha già prodotto due difetti (C1 dell'Onda
2.4, W24-Q1 della 2.5) — si sostituisce con un editor nativo più un renderer. Esiste un WebView per
Avalonia (`WebViewControl-Avalonia`, Chromium), ma sarebbe portarsi dietro il problema invece del motivo.

## 4. Scope della fetta

**Dentro**: l'app Avalonia si avvia su Linux, apre un database cifrato, mostra l'albero delle abitudini
nella Home, registra un completamento di un'abitudine *action*, e il dato sopravvive al riavvio.

Attraversa apposta tutti gli strati che possono sorprendere:

- **Bootstrap + DI + migrazioni** — l'`ApplicationInitializer` su Linux (le migrazioni sono già provate qui, lui no)
- **SQLCipher senza DPAPI** — primo uso reale di `PassphraseKeyProvider`, che esiste dal 2026 e non è mai
  stato la strada principale di nessuno
- **Un ViewModel vero riusato senza modifiche** — `HomeViewModel` + `HomeRowViewModel`, fra i più intrecciati
- **Una scrittura**, non solo una lettura
- **Il tema**, in chiaro e in scuro

**Fuori**: grafici, editor markdown, export, backup, builder, Configura, drag-and-drop, `HeatmapStrip`
(controllo disegnato a mano: va portato, ma non serve a rispondere alla domanda).

## 5. Struttura dei progetti

**Esito, non premessa**: la forma finale è un progetto `Recordum.Presentation` (`net10.0`, zero UI) con i
126 file di ViewModel, referenziato da entrambe le shell. Una copia sola, per sempre.

**Non lo si fa adesso.** Quei file sono citati da 872 test WPF: spostarli significa toccare namespace e
using in tutta la suite, cioè infilare un refactor meccanico e rumoroso dentro il branch che deve diventare
v1.0 e restare verificabile a schermo. Mischiare le due cose rende impossibile capire quale delle due ha
rotto cosa.

**Per la fetta**: il progetto Avalonia compila **gli stessi file `.cs`** via `<Compile Include="../Recordum.WPF/ViewModels/…" />`,
elencati uno per uno (mai a tappeto). Zero spostamenti, zero namespace cambiati, zero righe toccate in WPF.
Due assembly, una sorgente: se un ViewModel non è portabile, **non compila**, e lo si scopre subito.

È dichiaratamente un'impalcatura e si smonta il giorno dell'estrazione.

| Progetto | TFM | Note |
|---|---|---|
| `src/Recordum.Avalonia` | `net10.0` | **senza** `-windows`. Referenzia Core, Data, Plugins.Contracts. Nessun riferimento a `Recordum.WPF` |
| `tests/Recordum.Avalonia.Tests` | `net10.0` | `Avalonia.Headless.XUnit`. Girano su Linux |

Entrambi entrano in `Recordum.slnx`. Il pin del RID è già per-OS (commit `e8bfe92`), quindi non serve altro.

## 6. Le cuciture

La fetta ne tocca **una sola**: `HomeClockTicker` crea un `DispatcherTimer` di WPF. Si introduce
`ITickSource` — *"chiamami ogni secondo, sul thread UI"* — con due implementazioni, una per shell.

Costo: **un solo file di ViewModel toccato** (`HomeClockTicker`), più la registrazione in DI di ciascuna
shell. Guadagno collaterale: quel timer diventa sostituibile nei test, che oggi lo saltano perché
`Application.Current` è null in headless.

Le altre cinque (`Shutdown`, il brush del tema, i tre timer di Dettaglio/Chart) **restano com'erano**: sono
fuori dalla fetta e si affrontano quando ci si arriva. Elencarle qui serve a non riscoprirle.

## 7. Dipendenze e decisioni operative

- **Chiave**: `PassphraseKeyProvider` (Argon2id) dietro `IDbKeyProvider`. Conseguenza visibile: l'avvio
  chiede una passphrase, quindi la fetta include una finestrella di sblocco. Vale la regola già
  documentata — *passphrase persa = dati persi, nessun recupero lato server*.
- **Database della fetta**: **non** quello reale dell'autore. Un DB nuovo in `~/.local/share/Recordum`,
  popolato da `DemoDataSeeder` — che è stato toccato in R9 e qui viene messo alla prova. Zero rischio per
  i dati veri.
- **Servizi della Home**: misurati, tutti portabili. L'unico legato a una shell
  (`IHabitTemplatePickerLauncher`) è **opzionale e nullable**: la fetta passa `null` e la Home funziona
  senza il picker.
- **Tema**: non si portano `Tokens.xaml` e `BaseStyles.xaml` interi, ma il set minimo di token — sfondo,
  testo, testo attenuato, accento, bordo, raggio, tipografia — abbastanza per giudicare se *sembra
  Recordum* in chiaro e in scuro. **Avvertenza**: il brief chiede "Inter o system stack" e l'app usa
  Palatino come font UI; su Linux quel font di norma non esiste, quindi si vedrà un ripiego finché non si
  decide cosa spedire.

## 8. Verifica

**Criterio di successo, fissato prima di cominciare**: il numero di righe modificate in
`src/Recordum.WPF/ViewModels/**` a fetta conclusa. **Il preventivo è un file solo** — `HomeClockTicker`,
per la cucitura `ITickSource`. (Da non confondere con i *sei* agganci WPF totali contati in §2: quelli
sono la mappa di tutto il porting, questo è il preventivo di questa fetta.)

- Se il conto finale è quello: il porting del resto è meccanico e si pianifica con fiducia.
- Se è molto di più: la fetta ha fatto il suo lavoro — i ViewModel sono più intrecciati di quanto sembri, e
  si ripianifica prima di aver speso mesi.

Il numero, qualunque sia, va scritto nel commit di chiusura.

**Test che girano su Linux**:

1. **Smoke headless** (`Avalonia.Headless.XUnit`): la finestra si costruisce, la Home elenca le abitudini
   di un DB seminato, un tap crea una `Completion` e la riga si aggiorna.
2. **Contenitore DI**: tutto ciò che la fetta chiede si risolve — il gemello di `BootstrapSmokeTests`.
3. **Nessuna regressione**: Core, Data, Plugins ed ExportEngine restano verdi dopo la cucitura del timer.

**Sonda del primo task — non una promessa**: Avalonia headless con backend Skia sa renderizzare un frame in
memoria. Se lo si riesce a salvare come PNG, diventa possibile **guardare** la schermata invece di asserire
soltanto che un ViewModel ha i dati giusti — cioè verificare visivamente senza l'autore, che finora è stato
l'unico occhio del progetto (i venti findings `W2V-*` nascono tutti da lui che naviga e manda screenshot).
Va provato per primo; se non regge, si dice e si torna al metodo di sempre.

**Cosa resta all'autore**: aprirla e dire se *sembra Recordum*. Quello non lo sostituisce un PNG.

**Definizione di "chiusa"**: l'app si apre su Linux, mostra l'albero, registra un completamento che
sopravvive al riavvio, i test headless sono verdi, e il conto delle righe di ViewModel modificate è nel
commit finale.

## 9. Regola di convivenza

Finché la fetta è esplorativa, **le feature nuove si fanno solo su WPF** e Avalonia insegue. Il verso si
inverte il giorno in cui l'autore decide che Avalonia è la versione ufficiale. Senza questa regola il
"due volte" diventa permanente per distrazione, e l'autore è una persona sola.

## 10. Cosa questa spec NON promette

- Non promette che il porting completo sia meccanico: lo **misura**, ed è tutto ciò che una fetta può fare.
- Non promette parità di funzioni: grafici, markdown, export e backup restano fuori.
- Non promette che Avalonia diventi ufficiale. Quella decisione arriva **dopo** aver visto la fetta girare.
- Non tocca il sync: due archivi, un `.recbak` per attraversarli.

## 11. Domande lasciate aperte, deliberatamente

1. **Il font di spedizione su Linux** — Palatino non c'è; si sceglie un ripiego o si spedisce un font?
   Decisione estetica, l'autore la prende guardando.
2. **Come si sblocca il DB a regime** — la passphrase a ogni avvio va bene per la fetta; per un utente vero
   si valuterà il portachiavi di sistema (libsecret) o una sessione ricordata.
3. **Packaging** (AppImage, Flatpak, `.deb`?) — non prima di sapere se la fetta regge.
4. **Che ne è di `HeatmapStrip`** e degli altri controlli disegnati a mano: portati, ripensati, o resi
   comuni? Si decide con il primo di essi davanti.
