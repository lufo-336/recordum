# Onda 4 — testa Android: piano

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** l'APK `io.github.lufo336.recordum`, installato sul Galaxy S21 dell'autore, apre la Home dei dati veri (arrivati
con un ripristino del `.recbak` del computer) nella cornice da telefono, con impronta e regole d'accesso, e il tempo
dall'icona alla Home è misurato e scritto.

**Architecture:** la WPF si spegne; `Recordum.UI` diventa una libreria `net10.0` con tutte le viste, la cornice del
telefono e la sequenza d'avvio condivisa; due teste sottili (`Recordum.Desktop`, `Recordum.Android`) danno opzioni,
portachiavi e servizi di piattaforma attraverso `IAppHead`. La cornice usa `TabbedPage`/`NavigationPage`/`ContentPage` di
Avalonia 12 più un nostro `SheetHost`.

**Tech Stack:** .NET 10, Avalonia 12.1.2 (+ `Avalonia.Android` 12.1.2), EF Core 10, SQLite3MC, xunit v3 headless,
.NET for Android (workload `android` 36.1.x, `minSdk` 30, `targetSdk` 36), API di sistema Android (Keystore,
`android.hardware.biometrics.BiometricPrompt`, Storage Access Framework).

**Spec:** `docs/superpowers/specs/2026-11-24-telefono-onde-4-5-design.md` (decisioni T1–T9 al §5). Mockup:
`docs/design/2026-11-24-telefono-onde-4-5/accesso-e-finestre.html`, `docs/design/2026-11-10-telefono-mockups/cornice.html`.

**Branch:** `telefono-onda4` (da `unisci`, T1 della spec). Si ribasa su `main` quando l'autore unisce `unisci`.

## Global Constraints

- `Recordum.Presentation`, `.Core`, `.Data`, `.Plugins*` e la libreria `Recordum.UI` restano `net10.0`: nessun
  `-windows`, nessun `-android` (guardia in Task 2).
- Ogni astrazione di host nuova: implementazione Desktop + Android, e un test della registrazione DI per ciascuna testa.
- Stringhe solo in `Strings.resx` + `Strings.it.resx` + `Strings.Designer.cs` (a mano, via shell; l'hook blocca Edit/Write
  sui `*.Designer.cs`); la copia è nella tabella "Parole" in fondo, inglese e italiano. Ad agy niente `.resx`.
- Versioni esatte in `Directory.Packages.props`. **Nessun pacchetto nuovo** salvo `Avalonia.Android` 12.1.2 (e, se il
  Task 13 lo sceglie, `Microsoft.EntityFrameworkCore.Tasks` 10.0.12). Niente AndroidX aggiunto a mano.
- `TreatWarningsAsErrors` resta globale anche sulla testa Android; ogni `NoWarn` ha un commento col perché.
- Manifest Android: `package` `io.github.lufo336.recordum`, `minSdk` 30, **nessun** `INTERNET`, **nessun** `CAMERA`,
  `android:allowBackup="false"` con regole d'estrazione che escludono tutto.
- Tempo solo da `IClock`; giorni solo da `DayBoundary`; chiavi solo da `CipherPragmas`; file in chiaro solo in
  `TempDirectory` (CLAUDE.md §5–6).
- Avalonia: `InitializeComponent()`, `x:DataType` su ogni view e template, `[AvaloniaFact]` per i tipi Avalonia,
  `RunJobs()` + `ForceRenderTimerTick()` dopo un'azione, ogni PNG con un'asserzione strutturale. Larghezza telefono nei
  test: **360 × 780**, chiaro e scuro.
- Un test di regressione si prova contro il codice rotto (si rompe, rosso, si ripristina). Filtri `dotnet test` per
  nome di classe, controllando il totale eseguito.
- Build Linux: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"`; mai
  `-p:UseAppHost=false` da riga di comando; mai un `-r` da riga di comando sulla soluzione (arriva ai progetti condivisi).
- Ogni build installata sul telefono dell'autore, dal Task 9 in poi (dati veri), è firmata con la chiave del dogfooding
  e preceduta da "Backup ora". Mai `adb uninstall`.

## Review Focus

1. **L'activity ricreata** (rotazione, tema chiaro/scuro di sistema, ritorno dopo che Android ha distrutto l'activity
   ma non il processo): nessuna seconda inizializzazione, nessun "Recordum è già aperto". Test in Task 3
   (`AppStartup.RunAsync` chiamato due volte restituisce lo stesso `Task`) e voce di checklist in Task 14.
2. **Un'impronta aggiunta al telefono dopo l'attivazione**: pagina della passphrase con la ragione, poi l'impronta si
   riattiva. Test in Task 10 (`DbUnlockService` mappa la ragione `KeyInvalidated`; `UnlockViewModel` mostra il testo e
   spunta la casella), prova a mano nella checklist.
3. **Indietro con un foglio aperto sopra una pagina spinta**: chiude solo il foglio. Test in Task 6.
4. **Ripristino di un `.recbak` del computer**: le chiavi locali del telefono restano (`home.rows` Compatta, sicurezza),
   quelle del computer non arrivano. Test in Task 9.
5. **Avvio che fallisce sul telefono** (database rovinato, cartella non scrivibile, eccezione in `CoreInitializer`):
   pagina d'errore con il percorso del log e "Copia i dettagli", mai un ritorno muto al launcher. Test in Task 3 con una
   testa finta.

---

### Task 1: La WPF si spegne

**Livello:** Sonnet (le parti di documento: Haiku). **Fermarsi e salire:** un test portabile che diventa rosso dopo la
rimozione (vuol dire che qualcosa di portabile dipendeva dalla WPF).

**Files:**
- Delete: `src/Recordum.WPF/`, `tests/Recordum.WPF.Tests/`, `.claude/skills/run/` (lancia la WPF)
- Modify: `Recordum.slnx`, `.github/workflows/ci.yml` (step "Test WPF" e commento in testa), `src/Recordum.Plugins.ChartEngine/Recordum.Plugins.ChartEngine.csproj`
  (`net10.0`, via `UseWPF` e `LiveChartsCore.SkiaSharpView.WPF`), `tests/Recordum.Plugins.ChartEngine.Tests/*.csproj` (`net10.0`), `Directory.Packages.props`
  (via le versioni non più usate), `CLAUDE.md`, `docs/ARCHITECTURE.md`, `docs/phase-4-issues.md` (indice)
- Delete or move: in `src/Recordum.Plugins.ChartEngine/` il file che usa WPF (`ChartEngineRenderer.cs` o quello che
  `grep -l "System.Windows\|SkiaSharpView.WPF"` indica) e i suoi test

**Interfaces:** nessuna nuova. I builder di ChartEngine (`Builders/*.cs`, solo `Recordum.Plugins.Contracts.Charts`)
restano. `Recordum.Plugins.ExportEngine` non si tocca.

- [ ] **Step 1:** togliere progetti e riferimenti; ChartEngine a `net10.0` senza il renderer WPF.
- [ ] **Step 2:** `dotnet build Recordum.slnx -p:EnableWindowsTargeting=true` → 0 warning, 0 errori.
- [ ] **Step 3:** suite portabili come la CI Linux (`Core`, `Data` con `Category!=WindowsOnly&Category!=Performance&Category!=FixtureGenerator`,
  `Plugins`, `Plugins.ChartEngine`, `Plugins.ExportEngine`, `Presentation`, `UI`) → verdi, totali annotati nel registro.
- [ ] **Step 4:** CI: il job Windows esegue le suite portabili e `WindowsOnly`; via lo step serializzato della WPF.
- [ ] **Step 5:** CLAUDE.md: §3 ("WPF è congelata…" → spenta il 2026-11-24), §4 (righe WPF e ChartEngine), la regola
  "due implementazioni (WPF, UI)" → "una per testa (Desktop, Android)", §8 (la voce `CultureBootstrap`/`NumericInputRule`
  di WPF si toglie, resta quella di Avalonia), §10 (comando dei test WPF). ARCHITECTURE.md allineato. Nel tracker,
  una riga nell'indice: le voci aperte che riguardano solo viste WPF sono chiuse dallo spegnimento (elencarne gli id).
- [ ] **Step 6:** commit `chore: la WPF si spegne (F8 anticipato, spec telefono T3)`; push; CI verde Linux+Windows.

---

### Task 2: `Recordum.UI` libreria, `Recordum.Desktop` testa

**Livello:** Sonnet. **Fermarsi e salire:** gli screenshot headless smettono di uscire, o un `avares://` non si risolve.

**Files:**
- Modify: `src/Recordum.UI/Recordum.UI.csproj` (`OutputType` Library, via `Avalonia.Desktop`; `AssemblyName` invariato)
- Create: `src/Recordum.Desktop/Recordum.Desktop.csproj` (`WinExe`, `net10.0`, `Avalonia.Desktop`, riferimento a UI)
- Move: `src/Recordum.UI/Program.cs` → `src/Recordum.Desktop/Program.cs`
- Create: `src/Recordum.Desktop/DesktopHead.cs`
- Create: `src/Recordum.UI/Bootstrap/IAppHead.cs`
- Modify: `src/Recordum.UI/App.axaml.cs` (proprietà `Head`), `src/Recordum.UI/Bootstrap/AppComposition.cs`
  (`BuildServices` prende la testa), `src/Recordum.UI/Bootstrap/AvaloniaShellServices.cs` (si divide: comune + desktop),
  `src/Recordum.UI/Bootstrap/StoredKeyUnlock.cs` (il "Missing" lo dice la testa)
- Modify: `scripts/install-linux.sh` (publish di `Recordum.Desktop`, `exec … Recordum.Desktop.dll`), `Recordum.slnx`,
  `.github/workflows/ci.yml` se nomina il progetto
- Modify: `tests/Recordum.UI.Tests/Recordum.UI.Tests.csproj` (i nativi di Skia arrivavano da `Avalonia.Desktop`
  attraverso UI: ora un riferimento diretto ad `Avalonia.Desktop`, vedi il commento in `TestAppBuilder.cs`)
- Test: `tests/Recordum.UI.Tests/Bootstrap/AvaloniaShellServicesTests.cs`, nuovo `tests/Recordum.UI.Tests/Bootstrap/PlatformNeutralityTests.cs`

**Interfaces — Produces:**

```csharp
namespace Recordum.UI.Bootstrap;

/// La testa: cosa cambia fra computer e telefono. Due implementazioni: DesktopHead, AndroidHead (Task 4).
public interface IAppHead
{
    IReadOnlyList<string> Args { get; }                      // Desktop: argv; Android: vuoto
    RecordumOptions ResolveOptions();                         // Desktop: RecordumOptions.Resolve(Args, env); Android: ForDirectory(FilesDir/Recordum)
    ISecretStore CreateSecretStore(RecordumOptions options);  // Desktop: libsecret su Linux, Null altrove (oggi); Android: Keystore (Task 10)
    RememberUnavailableReason MissingKeyringReason { get; }   // Desktop: NoKeyring su Linux, NotSupported altrove (oggi in StoredKeyUnlock)
    TimeSpan StoredKeyCap { get; }                            // Desktop: StoredKeyUnlock.DefaultCap (10 s); Android: Timeout.InfiniteTimeSpan
    bool UsesWindowsDpapi { get; }                            // Desktop: OperatingSystem.IsWindows(); Android: false
    HomeRows DefaultHomeRows { get; }                         // Desktop: Comfortable; Android: Compact
    void AddShellServices(IServiceCollection services);       // Desktop: AddRecordumDesktopShell(); Android: AddRecordumPhoneShell() + servizi Android
    bool RunsSync { get; }                                    // Desktop: true; Android: false finché non c'è l'ISyncFileStore del selettore (spec §10.6)
    bool OffersBiometric { get; }                             // Desktop: false; Android: true (etichette dello sblocco, Task 10)
    TimeSpan ElapsedSinceProcessStart();                      // per StartupTimeline (Task 3)
    void Report(string line);                                 // Desktop: log in logs/; Android: log + logcat
}
```

- `public static IServiceCollection AddRecordumAvaloniaCommon(this IServiceCollection s)` — ciò che oggi è in
  `AddRecordumAvaloniaShell` e non apre finestre (tick, tema, `SettingsViewModel`, le Null).
- `public static IServiceCollection AddRecordumDesktopShell(this IServiceCollection s)` — launcher a finestra, dialog di file, `IApplicationLifetime` classico.
- `AppComposition.BuildServices(RecordumOptions, IDbKeyProvider, ISecretStore, IAppHead, Action<IServiceCollection>? configure = null)`;
  `HomeRowsPreference` registrato con `head.DefaultHomeRows`; `DataAndBackupPageViewModel(showDeviceKey: !head.UsesWindowsDpapi)`.
- `App.Head { get; init; }` (`IAppHead?`): con `Head` null `App` non avvia niente (è il caso dei test headless, come oggi).

- [ ] **Step 1: test rossi.** `PlatformNeutralityTests.Shared_assemblies_have_no_target_platform` — per `Recordum.Core`,
  `.Data`, `.Presentation`, `.Plugins`, `.Plugins.Contracts`, `.Plugins.ChartEngine`, `.Plugins.ExportEngine` e
  `typeof(App).Assembly`: `GetCustomAttribute<TargetPlatformAttribute>()` è null. `AvaloniaShellServicesTests`:
  un `BuildProvider` per la testa desktop (`AddRecordumAvaloniaCommon().AddRecordumDesktopShell()`), stessi assert di oggi.
- [ ] **Step 2:** libreria, testa, `IAppHead`, divisione delle registrazioni; `Program` in Desktop costruisce
  `AppBuilder.Configure(() => new App { Head = new DesktopHead(args) })` con `X11PlatformOptions { WmClass = "recordum" }`.
- [ ] **Step 3:** suite UI verde, **e gli screenshot escono** (controllare che la cartella `Screenshots` si riempia: senza
  i nativi di Skia smettono in silenzio). Build della soluzione con 0 warning.
- [ ] **Step 4:** `scripts/install-linux.sh` in una cartella di prova (`--prefix` o variabile che lo script già offre;
  se non c'è, leggere lo script e provarlo senza toccare l'installazione vera) → l'app parte con `--data-dir` di prova.
- [ ] **Step 5:** commit `refactor(ui): Recordum.UI libreria, Recordum.Desktop testa, IAppHead`.

---

### Task 3: La sequenza d'avvio condivisa, fuori dal thread dell'interfaccia, misurata

**Livello:** Sonnet; review Opus (è il percorso di ogni avvio). **Fermarsi e salire:** un test di
`KeyringStartupIntegrationTests` cambia comportamento.

**Files:**
- Create: `src/Recordum.UI/Bootstrap/AppStartup.cs`, `src/Recordum.UI/Bootstrap/IStartupHost.cs`,
  `src/Recordum.UI/Bootstrap/StartupTimeline.cs`, `src/Recordum.UI/Bootstrap/DesktopStartupHost.cs`,
  `src/Recordum.UI/Views/Startup/WaitingView.axaml(.cs)`
- Modify: `src/Recordum.UI/App.axaml.cs` (resta: lifetime → host → `AppStartup`), `src/Recordum.UI/Bootstrap/AppComposition.cs`
  (`InitializeAsync` non avvia più gli scheduler), `src/Recordum.UI/Diagnostics/CrashLogger.cs` (una riga informativa: `Info(string)`)
- Test: `tests/Recordum.UI.Tests/Startup/AppStartupTests.cs`

**Interfaces — Consumes:** `IAppHead` (Task 2). **Produces:**

```csharp
public interface IStartupHost   // Desktop: finestre (DesktopStartupHost); telefono: PhoneStartupHost (Task 5)
{
    void ShowWaiting(string status);                                  // sul thread UI
    Task<IDbKeyProvider?> UnlockAsync(UnlockViewModel viewModel);     // null = l'utente ha chiuso
    void ShowFatal(string message, string? logPath);
    void ShowMain(IServiceProvider services);
}

public sealed class AppStartup(IAppHead head, IStartupHost host)
{
    public StartupTimeline Timeline { get; }
    public Task<ServiceProvider?> RunAsync();   // idempotente: la seconda chiamata restituisce lo stesso Task
    public ValueTask DisposeAsync();            // flush dell'unione (tetto 10 s, come oggi), contenitore, poi il lock
}

public sealed class StartupTimeline
{
    public void Mark(string step);              // tempo da head.ElapsedSinceProcessStart()
    public IReadOnlyList<(string Step, TimeSpan At)> Marks { get; }
    public string Format();                     // "startup options=12ms lock=15ms … home=2840ms"
}
```

Ordine dei passi (spec §7.1, CLAUDE.md §6): `options` → `lock` → `seed-refusal` → `reconcile` → `detect` → `unlock`
(chiave ricordata col tetto `head.StoredKeyCap`, poi l'host) → `container` → `core-init` → `culture-theme-rows` →
`home` (`ShowMain`) → **dopo** la Home: scheduler del backup, giro dell'unione (solo se `head.RunsSync`). `head.Report(Timeline.Format())` a fine
avvio. Ogni passo lento con `Task.Run` (oggi `ConfigureAwait(true)` lo lascia sul thread UI); i passaggi all'host con
`Dispatcher.UIThread.InvokeAsync`. Lo stato a schermo: `Startup_Waiting_Opening` prima dello sblocco,
`Startup_Waiting_Preparing` da `container` a `home`.

- [ ] **Step 1: test rossi** in `AppStartupTests` (testa e host finti, `PrivateDbHost` per la cartella):
  - `Steps_run_in_the_documented_order` — i nomi dei `Marks` in quell'ordine, `home` prima di `backup-scheduler` e `sync-scheduler`.
  - `Sync_scheduler_does_not_start_when_the_head_does_not_run_sync` — con `RunsSync` falso nessun mark `sync-scheduler`
    (e con vero sì: il parametro muto).
  - `Core_initialization_does_not_run_on_the_UI_thread` — un passo di `CoreInitializer` finto registra
    `Dispatcher.UIThread.CheckAccess()`: falso.
  - `RunAsync_twice_returns_the_same_task_and_takes_the_lock_once` (Review Focus 1).
  - `A_failing_core_initialization_shows_the_fatal_page_with_the_log_path` (Review Focus 5): `ShowFatal` chiamato,
    `logPath` non vuoto, `ShowMain` mai.
  - `A_second_instance_shows_already_running` — lock preso da un altro `DataDirectoryLock` → `ShowFatal(Strings.Startup_AlreadyRunning, …)`.
- [ ] **Step 2:** implementare; `App.OnFrameworkInitializationCompleted` crea l'host giusto per il lifetime
  (classico → `DesktopStartupHost`; vista unica → `PhoneStartupHost`, che arriva al Task 5: fino ad allora un
  `NotSupportedException` esplicito).
- [ ] **Step 3:** verdi; `KeyringStartupIntegrationTests` e tutta la suite UI verdi; avvio a mano su Fedora con
  `--data-dir` di prova: il log contiene la riga `startup … home=…`.
- [ ] **Step 4:** commit `feat(ui): sequenza d'avvio condivisa, fuori dal thread UI, con i tempi`.

---

### Task 4: La testa Android, la firma, la CI

**Livello:** Sonnet + io sul telefono. **Fermarsi e salire:** l'APK non parte sul telefono dopo due tentativi, o il
manifest unito contiene `INTERNET`.

**Files:**
- Create: `src/Recordum.Android/Recordum.Android.csproj`, `MainActivity.cs`, `RecordumAndroidApplication.cs`,
  `AndroidHead.cs`, `AndroidApplicationLifetime.cs`, `Resources/values/styles.xml`, `Resources/xml/data_extraction_rules.xml`,
  `Properties/AndroidManifest.xml`, icona (dal marchio `scripts/linux/recordum.svg`, rasterizzato una volta)
- Create: `scripts/android/create-signing-key.sh`, `scripts/android/install-phone.sh`
- Modify: `Recordum.slnx` (il progetto Android **fuori** dalla soluzione predefinita, o con una condizione: la build
  Linux/Windows di oggi non deve richiedere il workload), `.github/workflows/ci.yml` (job `android`), `Directory.Packages.props`
  (`Avalonia.Android` 12.1.2)

**Decisioni:**
- csproj (dalla sonda, `sonda-android:spikes/android/Recordum.Sonda.csproj`): `net10.0-android`, `SupportedOSPlatformVersion` 30,
  `ApplicationId` `io.github.lufo336.recordum`, `ApplicationTitle` Recordum, `RuntimeIdentifier` `android-arm64` come
  proprietà (sovrascrivibile con `-p:RecordumRid=android-x64` per un emulatore), `AndroidPackageFormat` apk,
  `<Using Remove="Android.App;Android.Widget;Android.OS;Android.Runtime;Android.Content;Android.Views"/>`,
  `GlobalPropertiesToRemove="RuntimeIdentifier;SelfContained"` su ogni `ProjectReference`. Analizzatori accesi.
- `RecordumAndroidApplication : AvaloniaAndroidApplication<App>` con `CreateAppBuilder()` →
  `AppBuilder.Configure(() => new App { Head = new AndroidHead(this) }).UseAndroid()`. Il processo possiede lock,
  chiave e contenitore; l'activity riattacca la vista (spec §7.1b).
- `MainActivity : AvaloniaMainActivity`, `WindowSoftInputMode = AdjustResize`, `ConfigurationChanges` orientamento,
  dimensioni, `UiMode`. Indietro non gestito dall'app alla radice → `MoveTaskToBack(true)` (non chiude).
- `AndroidHead.ResolveOptions()` → `RecordumOptions.ForDirectory(Path.Combine(context.FilesDir.AbsolutePath, "Recordum"))`
  (`IsDefaultLocation` vero: è la cartella dei dati veri del telefono).
  `CreateSecretStore` → `NullSecretStore` fino al Task 10. `AddShellServices` → per ora solo le registrazioni comuni +
  `AndroidApplicationLifetime` (il resto al Task 5).
- Firma: `create-signing-key.sh` genera una volta `~/.local/share/recordum-signing/recordum-dogfood.jks` (alias
  `recordum`, RSA 4096, validità 30 anni) e mette la password nel portachiavi di GNOME (`secret-tool store --label
  "Recordum signing" service recordum-signing`); rifiuta di sovrascrivere un keystore esistente. `install-phone.sh`:
  `dotnet publish -c Release` con `AndroidKeyStore=true` e le proprietà di firma (password da `secret-tool lookup`),
  poi `adb install -r`; **non disinstalla mai**; se `adb install` risponde `INSTALL_FAILED_UPDATE_INCOMPATIBLE` si ferma
  e spiega (trappola 24 di Quaestio); prima di installare chiede conferma che "Backup ora" sia stato fatto.
- CI: job `android` su ubuntu, `dotnet workload install android`, JDK 21, `dotnet build src/Recordum.Android -c Release`
  senza firma di rilascio; poi una guardia sul manifest unito (`obj/Release/**/AndroidManifest.xml`): niente
  `android.permission.INTERNET`, niente `android.permission.CAMERA`, `android:allowBackup="false"`. Il push vuole lo
  scope `workflow` nel token di `gh` (CLAUDE.md §10).

- [ ] **Step 1:** progetto e testa; build Release locale → APK.
- [ ] **Step 2:** guardia del manifest eseguita in locale sul manifest unito → passa; togliendo `allowBackup` → fallisce
  (rosso provato), poi ripristinato.
- [ ] **Step 3:** sul telefono (Release, firmato con la chiave del dogfooding, `install-phone.sh`): l'avvio arriva alla
  Home (vuota, cartella nuova) dentro un contenitore provvisorio; logcat ha la riga `startup … home=…`. **Misura di base**
  (spec §7.4): 5 avvii a freddo, mediana, scritta nella tabella del §16 della spec.
- [ ] **Step 4:** CI col job `android` verde.
- [ ] **Step 5:** commit `feat(android): testa Android, firma del dogfooding, job di CI`.

---

### Task 5: La cornice — cancello, barra in basso, pagine

**Livello:** Sonnet; io sul telefono per il cancello. **Fermarsi e salire:** il cancello fallisce → si scrive il Task
5-bis "cornice nostra" (spec §4.2) prima di andare avanti.

**Files:**
- Create: `src/Recordum.UI/Phone/PhoneShell.axaml(.cs)` (un `TabbedPage` con `TabPlacement` in basso),
  `src/Recordum.UI/Phone/PhoneNavigator.cs`, `src/Recordum.UI/Phone/PhoneStartupHost.cs`,
  `src/Recordum.UI/Phone/ComingSoonPage.axaml(.cs)`, `src/Recordum.UI/Phone/PhoneShellServices.cs`
- Modify: `src/Recordum.Android/AndroidHead.cs` (`AddShellServices` → `AddRecordumPhoneShell()`)
- Test: `tests/Recordum.UI.Tests/Phone/PhoneShellTests.cs`, `tests/Recordum.UI.Tests/Phone/PhoneShellServicesTests.cs`

**Cancello (spec §13, sonda 1, ripetuta qui se non è già stata fatta fuori dal repo):** sul telefono, `PhoneShell` con
quattro voci in basso, una `PushAsync` e il gesto indietro di sistema (anche alla radice), bordi sicuri (niente sotto la
barra di stato o la barra dei gesti), rotazione e cambio tema senza perdere la pila. Risultato scritto nel registro.

**Interfaces — Produces:**
- `PhoneNavigator : IHabitNavigator, IHabitTemplatePickerLauncher, IHabitBuilderLauncher, ISettingsWindowLauncher`
  (singleton, registrato per le quattro interfacce). Tiene un riferimento a `PhoneShell` assegnato da `ShowMain`
  (come oggi `MainWindow()` è letto alla chiamata). `OpenHabitAtDayAsync` → crea con `ISubjectScreenFactory.Create()`
  e spinge la pagina dell'abitudine (Task 7) sulla voce Oggi; alla `Pop` della pagina, `SubjectScreen.Scope.Dispose()`.
  `ShowAsync(category)` → seleziona la voce Impostazioni (e la categoria, Task 8). `PickAsync`/`LaunchAsync` → pagine del
  Task 8; fino ad allora restituiscono `null` (annullato), mai un'eccezione.
- `PhoneShell` si abbona a `HomeViewModel.HabitActivationRequested` (lo stesso evento che usa `MainWindowViewModel`) e
  spinge la pagina dell'abitudine; la barra del timer (`TimerStatusBarView`) sta sopra la barra delle voci.
- `AddRecordumPhoneShell(this IServiceCollection)` = `AddRecordumAvaloniaCommon()` + `PhoneNavigator` + `PhoneStartupHost`.
- Indietro (spec §9.2): foglio aperto → lo chiude (Task 6); pagina spinta → `PopAsync`; radice di una voce diversa da
  Oggi → seleziona Oggi; radice di Oggi → non gestito (la testa fa `MoveTaskToBack`). Implementato su
  `TopLevel.BackRequested` in un metodo `bool HandleBack()` di `PhoneShell`, provabile in headless.
- Cerca e Statistiche: `ComingSoonPage` con le parole della tabella.

- [ ] **Step 1: test rossi** (`[AvaloniaFact]`, 360 × 780): `PhoneShellTests.Has_four_tabs_in_order`
  (Oggi, Cerca, Statistiche, Impostazioni; `TabPlacement.Bottom`); `Activating_a_habit_pushes_its_page_on_Today`;
  `Back_pops_a_pushed_page_and_disposes_its_scope`; `Back_on_the_root_of_Settings_selects_Today`;
  `Back_on_the_root_of_Today_is_not_handled`; `PhoneShellServicesTests.All_launchers_resolve_to_PhoneNavigator`
  e `Every_host_abstraction_registered_by_the_desktop_head_is_also_registered_by_the_phone_head` (confronta i
  `ServiceType` delle due collezioni: una guardia che nessuna astrazione resta senza implementazione Android).
  PNG `phone-shell-{light,dark}.png` con l'asserzione sulle quattro voci.
- [ ] **Step 2:** implementare; `PhoneStartupHost` mostra `WaitingView`, poi la pagina di sblocco (la vista di oggi
  dentro una `ContentPage`), l'errore fatale (messaggio, percorso del log e `Startup_CopyDetails`, che copia messaggio e
  percorso con gli appunti di `TopLevel`), poi `PhoneShell` come `MainView`.
- [ ] **Step 3:** verdi; sul telefono il cancello e la Home vera nella barra.
- [ ] **Step 4:** commit `feat(phone): cornice con le pagine di Avalonia 12`.

---

### Task 6: Il foglio dal basso e la conferma

**Livello:** Sonnet (adatto ad agy con brief e test rossi). **Fermarsi e salire:** il foglio non sale sopra la tastiera
sul telefono.

**Files:**
- Create: `src/Recordum.UI/Phone/SheetHost.cs` (+ stile in `src/Recordum.UI/Themes/`), `src/Recordum.UI/Phone/PhoneConfirmationDialog.cs`
- Modify: `src/Recordum.UI/Phone/PhoneShell.axaml` (lo strato del foglio sopra tutto), `PhoneShellServices.cs`
- Test: `tests/Recordum.UI.Tests/Phone/SheetHostTests.cs`

**Interfaces — Produces:**
- `SheetHost : Control` con `Task<object?> ShowAsync(object content)` (il contenuto è un ViewModel o un controllo; si
  chiude con `Close(object? result)`, col gesto indietro o toccando lo sfondo → risultato `null`), `bool IsOpen`.
  Animazione contenuta (scorrimento ≤ 200 ms), sfondo `--scrim` del mockup, angoli 18, maniglia.
- `PhoneConfirmationDialog(Func<SheetHost?> host) : IConfirmationDialog` — titolo, messaggio, pulsante distruttivo sopra
  (non rosso, CLAUDE.md §11) e "Annulla" sotto; schermo 3c del mockup.

- [ ] **Step 1: test rossi:** `SheetHostTests.Back_with_a_sheet_open_over_a_pushed_page_closes_only_the_sheet`
  (Review Focus 3: dopo `HandleBack()` il foglio è chiuso e la pagina spinta è ancora lì);
  `Tapping_the_scrim_resolves_null`; `Confirm_returns_true_only_on_the_destructive_button`; PNG
  `phone-confirm-{light,dark}.png` con l'asserzione che i due pulsanti siano larghi quanto il foglio.
- [ ] **Step 2:** implementare. **Step 3:** verdi; sul telefono un foglio con un `TextBox` numerico sale sopra la tastiera.
- [ ] **Step 4:** commit `feat(phone): foglio dal basso e conferma`.

---

### Task 7: La pagina dell'abitudine a sezioni

**Livello:** Sonnet (adatto ad agy). **Fermarsi e salire:** serve cambiare `HabitScreenViewModel` oltre a una proprietà
di sola lettura.

**Files:**
- Create: `src/Recordum.UI/Phone/PhoneHabitPage.axaml(.cs)`
- Test: `tests/Recordum.UI.Tests/Phone/PhoneHabitPageTests.cs`

**Decisioni (cornice, schermo 3):** una pagina con ← in alto (nome nel colore dell'abitudine, sottotitolo), i quattro
numeri della testa in due righe, un selettore a segmenti delle sezioni (Cronologia, Note, Configura: **una alla volta**,
riusando le viste delle colonne di oggi), il comando di registrazione **in basso** a tutta larghezza ("Registra …" o il
comando del tipo, come oggi in alto a destra) che apre nel `SheetHost` la vista di registrazione che il computer mostra
oggi (quick-add misurato, a serie, oggi di stato e astinenza); "per un altro giorno" sotto. Rinomina, archivia e il resto
nel ⋯. Il foglio vero per tipo, rifinito, è dell'onda 5.

- [ ] **Step 1: test rossi:** per ciascun tipo della fixture "manifesto" (azione, contatore, contatore a serie, astinenza,
  diario, stato): la pagina a 360 px mostra una sola sezione, il comando in basso, nessun controllo tagliato (larghezza
  effettiva ≤ 360); toccando il comando si apre il foglio con la vista di registrazione del tipo. PNG
  `phone-habit-<tipo>-{light,dark}.png`.
- [ ] **Step 2:** implementare. **Step 3:** verdi; PNG guardati da me (nomi, misure); sul telefono una registrazione vera
  da una pagina.
- [ ] **Step 4:** commit `feat(phone): pagina dell'abitudine a sezioni col comando in basso`.

---

### Task 8: Selettore dei modelli, creatore, Impostazioni come elenco

**Livello:** Sonnet (adatto ad agy, uno per pagina). **Fermarsi e salire:** una vista di oggi non sta in 360 px senza
cambiare il suo ViewModel.

**Files:**
- Create: `src/Recordum.UI/Phone/PhoneTemplatePickerPage.axaml(.cs)`, `PhoneCreatorPage.axaml(.cs)`,
  `PhoneSettingsPage.axaml(.cs)`, `PhoneSettingsCategoryPage.axaml(.cs)`
- Modify: `src/Recordum.UI/Phone/PhoneNavigator.cs` (`PickAsync`, `LaunchAsync`, `ShowAsync(category)`),
  `src/Recordum.UI/Views/Settings/SettingsWindow.axaml` (i `DataTemplate` delle categorie in un dizionario condiviso,
  `src/Recordum.UI/Views/Settings/SettingsTemplates.axaml`, usato da finestra e telefono)
- Modify: `src/Recordum.Presentation/ViewModels/Settings/SettingsCategoryId.cs` (+ `Security`),
  `SettingsWindowViewModel.cs` (parametro `SecuritySettingsViewModel? security = null`: la categoria c'è solo se non null)
- Test: `tests/Recordum.UI.Tests/Phone/PhonePagesTests.cs`, `tests/Recordum.Presentation.Tests/.../SettingsWindowViewModelTests.cs`

**Decisioni:** schermi 3a, 3b del mockup. Selettore: ricerca, "Da zero", "Gruppo vuoto", schede in due colonne, "I tuoi
modelli"; un tocco apre il dettaglio con "Usa questo modello" e "Personalizza…" in basso. Creatore: l'anteprima della
riga in alto, sempre visibili nome, tipo, colore, icona; sezioni chiuse; "Crea abitudine" in basso. Impostazioni:
l'elenco delle categorie di `SettingsWindowViewModel.Categories`, ognuna con la riga di stato (schermo 5 della cornice);
le categorie "in arrivo" attenuate; Sincronizzazione "In arrivo" sul telefono (spec §10.6); un tocco spinge la pagina
della categoria.

- [ ] **Step 1: test rossi:** `SettingsWindowViewModelTests.Security_category_is_absent_by_default` e
  `Security_category_is_present_when_the_head_provides_it` (il parametro muto); `PhonePagesTests`: dal + della Home si
  arriva al selettore, "Usa questo modello" crea **una** abitudine e torna alla Home; il creatore crea e torna; dalle
  Impostazioni ogni categoria abilitata apre la sua pagina. PNG per ciascuna pagina, chiaro e scuro.
- [ ] **Step 2–3:** implementare; verdi; PNG guardati.
- [ ] **Step 4:** commit `feat(phone): selettore, creatore e Impostazioni come pagine`.

---

### Task 9: File, ripristino e backup dal telefono; il ripristino non porta le chiavi locali

**Livello:** Sonnet (Data: mai Haiku). **Fermarsi e salire:** il contratto di salvataggio tocca più di
`BackupDialogViewModel` e la sua finestra.

**Files:**
- Create: `src/Recordum.Android/Services/AndroidDocumentPicker.cs` (apri, salva: SAF), `AndroidOpenFileDialog.cs`,
  `AndroidBackupSaveFileDialog.cs`
- Modify: `src/Recordum.Presentation/Services/IBackupSaveFileDialog.cs` (forma nuova, sotto), le sue implementazioni
  desktop e i chiamanti (il compilatore li elenca)
- Modify: `src/Recordum.Presentation/Services/IFolderOpener.cs`, `IFolderPicker.cs` (+ `bool IsAvailable`), le pagine che
  mostrano "Apri cartella" e "Sfoglia…" (spariscono se non disponibili)
- Modify: `src/Recordum.Data/Backup/RestoreService.cs` (chiavi locali)
- Test: `tests/Recordum.Data.Tests/Backup/RestoreKeepsDeviceSettingsTests.cs`, i test del backup che usano il dialog

**Interfaces — Produces:**

```csharp
public interface IBackupSaveFileDialog
{
    /// null = annullato. Il chiamante scrive in Target.Path, poi CommitAsync consegna il file dove l'utente ha scelto
    /// (Desktop: niente da fare; Android: copia nel documento SAF e cancella la copia in TempDirectory).
    Task<BackupSaveTarget?> PromptAsync(string defaultFileName, string filter);
}
public sealed record BackupSaveTarget(string Path, Func<CancellationToken, Task> CommitAsync, string DisplayLocation);
```

- Apri (`IOpenFileDialog` su Android): `ACTION_OPEN_DOCUMENT`, copia del flusso in `TempDirectory`, restituisce quel
  percorso; la copia si cancella dopo l'uso (il chiamante la usa subito: ripristino). Il ripristino passa dalla pagina
  Dati e backup del telefono (schermo 3d), con la passphrase del backup.
- Android: `IFolderPicker.IsAvailable` falso e `IFolderOpener.IsAvailable` falso in quest'onda (la cartella del backup
  automatico col selettore è dell'onda 5, spec §10.5); il backup automatico sul telefono resta spento.
- **Ripristino e chiavi locali** (spec §8.4): dopo il ripristino, ogni riga di `app_settings` la cui chiave **non** è in
  `SyncCatalog.TravellingSettingKeys` è quella del database di prima del ripristino (le chiavi locali che il dispositivo
  non aveva spariscono). Nello stesso punto in cui `RestoreService` già azzera `sync_meta`/`sync_base`.

- [ ] **Step 1: test rossi** (`RestoreKeepsDeviceSettingsTests`, Review Focus 4): un backup con `home.rows=Comfortable`
  e `theme=dark`, ripristinato in un database con `home.rows=Compact` e senza `theme` → dopo: `home.rows=Compact`,
  `theme` assente; `sync.folder` del computer non arriva (il telefono non si ritrova collegato alla cartella del
  computer); una chiave di `TravellingSettingKeys` arriva dal backup. Rosso provato contro il `RestoreService` di oggi.
- [ ] **Step 2:** implementare; contratto di salvataggio; implementazioni Android.
- [ ] **Step 3:** verdi (Data, Presentation, UI). Sul telefono: "Backup ora" su Drive col selettore, poi
  **il ripristino del `.recbak` del computer** → la Home dei dati veri, Compatta. Da qui il telefono ha dati veri: ogni
  installazione passa da `install-phone.sh` e da un "Backup ora".
- [ ] **Step 4:** commit `feat(phone): ripristino e backup col selettore di Android; il ripristino tiene le chiavi locali`.

---

### Task 10: Il portachiavi di Android con l'impronta

**Livello:** Sonnet scrive, Opus rivede (sicurezza). **Fermarsi e salire:** la sonda 2 (§13 della spec) non è stata
fatta: si fa prima, fuori dal repo, e il suo esito sceglie fra le due cifrature sotto.

**Files:**
- Modify: `src/Recordum.Core/Abstractions/Security/SecretStoreUnavailableException.cs` (+ `SecretDeclineReason? Declined`),
  `src/Recordum.Core/Abstractions/Security/IDbUnlockService.cs` (`StoredKeyOutcome.Declined`,
  `StoredKeyResult(..., SecretDeclineReason? Declined = null)`)
- Create: `src/Recordum.Core/Abstractions/Security/SecretDeclineReason.cs`,
  `src/Recordum.Presentation/Services/IDeviceKeyAccess.cs`
- Modify: `src/Recordum.Data/Security/DbUnlockService.cs` (`TryUnlockWithStoredKeyAsync`: un'eccezione con `Declined`
  → `StoredKeyOutcome.Declined`), `src/Recordum.UI/Bootstrap/StoredKeyUnlock.cs` (`Result` porta `Declined`),
  `src/Recordum.Presentation/ViewModels/Startup/UnlockViewModel.cs` (testo della ragione, etichetta della casella)
- Create: `src/Recordum.Android/Security/AndroidKeystoreSecretStore.cs`, `BiometricGate.cs`,
  `src/Recordum.Android/Security/AndroidDeviceKeyAccess.cs`, `src/Recordum.UI/Bootstrap/DesktopDeviceKeyAccess.cs`
- Test: `tests/Recordum.Data.Tests/Security/DbUnlockServiceDeclinedTests.cs`, `tests/Recordum.Presentation.Tests/.../UnlockViewModelDeclinedTests.cs`,
  `tests/Recordum.UI.Tests/Bootstrap/DeviceKeyAccessRegistrationTests.cs`

**Interfaces — Produces:**

```csharp
public enum SecretDeclineReason { Cancelled, KeyInvalidated, TooManyAttempts, Unavailable }

public enum DeviceUnlockMode { Biometric, Remembered, PassphraseEachTime }

public interface IDeviceKeyAccess   // Presentation. Desktop: Remembered/PassphraseEachTime (dove ISecretStore.IsAvailable); Android: tutti e tre
{
    IReadOnlyList<DeviceUnlockMode> SupportedModes { get; }
    Task<DeviceUnlockMode> GetModeAsync(CancellationToken ct = default);                 // fuori dal thread UI
    Task SetModeAsync(DeviceUnlockMode mode, CancellationToken ct = default);           // riscrive DbKey con la chiave aperta (IDbKeyProvider)
    Task<bool> ConfirmUserAsync(CancellationToken ct = default);                        // per "Blocca di nuovo" (Task 11): impronta o passphrase secondo il modo
}
```

**Decisioni (spec §8):**
- `AndroidKeystoreSecretStore : ISecretStore`: due chiavi nel Keystore (`recordum.bio`: solo biometria forte,
  `setUserAuthenticationParameters(0, AUTH_BIOMETRIC_STRONG)`, `setInvalidatedByBiometricEnrollment(true)`;
  `recordum.plain`: senza autenticazione). `DbKey` usa la chiave del modo (il modo è il nome del file cifrato:
  `keystore/dbkey.bio` o `keystore/dbkey.plain`, nella cartella dati; nessun file = passphrase ogni volta).
  `AutoBackup` usa sempre `recordum.plain`. Predefinito di una scrittura di `DbKey` senza file esistente: `bio`.
- **Cifratura**, decisa dalla sonda 2: (a) RSA-OAEP, la chiave pubblica cifra senza chiedere il dito e la privata
  decifra dietro `BiometricPrompt` → "Sblocca con l'impronta" al primo avvio costa un solo riquadro; oppure (b) AES-GCM,
  che chiede il dito anche alla scrittura. Preferita (a) se la sonda la conferma sul S21.
- `TryRead` di `dbkey.bio` gira su un thread del pool (contratto di `ISecretStore`): `BiometricGate` porta il riquadro
  sul thread UI e aspetta. Annullato → `SecretStoreUnavailableException(Declined = Cancelled)`;
  `KeyPermanentlyInvalidatedException` → cancella il file e lancia con `KeyInvalidated`; troppi tentativi →
  `TooManyAttempts`; nessun sensore → `Unavailable`.
- Il riquadro: titolo `Biometric_Title`, sottotitolo `Biometric_Subtitle`, pulsante negativo `Biometric_UsePassphrase`.
- `UnlockViewModel`: col `Declined` mostra la riga gialla (`Unlock_Declined_*`); su Android la casella è
  "Sblocca con l'impronta" (primo avvio) o "Torna a sbloccare con l'impronta" (dopo `KeyInvalidated`, spuntata), con la
  nota "La passphrase resta il ripiego."; su desktop resta "Ricorda su questo dispositivo". `AppStartup` sceglie le
  etichette con `head.OffersBiometric` (parametro del costruttore di `UnlockViewModel`, predefinito il valore di oggi).
- Dopo `Cancelled`: un pulsante "Riprova con l'impronta" nella pagina della passphrase.

- [ ] **Step 1: test rossi:** `DbUnlockServiceDeclinedTests`: un `ISecretStore` finto che lancia con ciascuna ragione →
  `StoredKeyOutcome.Declined` con la stessa ragione, nessun tentativo d'apertura; `KeyInvalidated` non cancella il salt.
  `UnlockViewModelDeclinedTests`: per ogni ragione il testo atteso (cultura fissata), `RememberOnDevice` vero dopo
  `KeyInvalidated`; senza `Declined` nessuna riga (parametro muto). `DeviceKeyAccessRegistrationTests`: le due teste
  registrano `IDeviceKeyAccess`; desktop su Windows (DPAPI) non offre modi.
- [ ] **Step 2:** implementare Core/Data/Presentation e le due teste.
- [ ] **Step 3:** verdi. Sul telefono: primo avvio con impronta; riavvio con impronta; annullare → passphrase → riprova;
  aggiungere un'impronta nelle impostazioni del telefono → riavvio → riga gialla, passphrase, impronta riattivata.
- [ ] **Step 4:** commit `feat(android): portachiavi sul Keystore con l'impronta`.

---

### Task 11: Impostazioni › Sicurezza, blocca di nuovo, app recenti, ripasso

**Livello:** Sonnet. **Fermarsi e salire:** "Blocca di nuovo" richiede di chiudere il database (non è nella spec).

**Files:**
- Create: `src/Recordum.Presentation/Services/SecurityPreferences.cs`, `src/Recordum.Presentation/Services/AccessPolicy.cs`,
  `src/Recordum.Presentation/ViewModels/Settings/SecuritySettingsViewModel.cs`,
  `src/Recordum.Presentation/ViewModels/Startup/PassphraseReminderViewModel.cs`,
  `src/Recordum.Data/Security/PassphraseCheck.cs`
- Create: `src/Recordum.UI/Views/Settings/SecurityPage.axaml(.cs)`, `src/Recordum.UI/Phone/LockOverlay.axaml(.cs)`,
  `src/Recordum.UI/Phone/PassphraseReminderPage.axaml(.cs)`
- Modify: `src/Recordum.Android/MainActivity.cs` (pausa e ripresa, recenti), `AndroidHead.cs` (registrazioni)
- Test: `tests/Recordum.Presentation.Tests/.../AccessPolicyTests.cs`, `SecuritySettingsViewModelTests.cs`,
  `PassphraseReminderViewModelTests.cs`; `tests/Recordum.Data.Tests/Security/PassphraseCheckTests.cs`;
  `tests/Recordum.UI.Tests/Phone/SecurityPageTests.cs`

**Interfaces — Produces:**

```csharp
public static class AccessPolicy
{
    // timeoutMinutes: 0 = subito, -1 = solo alla chiusura, altrimenti minuti in secondo piano
    public static bool ShouldRelock(DateTimeOffset pausedAt, DateTimeOffset resumedAt, int timeoutMinutes);
    // periodDays 0 = spento; lastCheckUtc null = mai controllato (si conta dall'accensione, che scrive lastCheck)
    public static bool IsReminderDue(DateTimeOffset? lastCheckUtc, DateTimeOffset nowUtc, int periodDays);
}

public sealed class SecurityPreferences   // come HomeRowsPreference: InitializeAsync, valori correnti, Changed
{
    public int RelockMinutes { get; }          // AppSetting "inactivity_timeout_minutes" (esiste già, locale, seed "5")
    public bool HideInRecents { get; }         // "security.hide_in_recents", assente = true
    public int ReminderDays { get; }           // "security.passphrase_reminder_days", assente = 0 (T5: spento)
    public DateTimeOffset? ReminderLastUtc { get; }   // "security.passphrase_reminder_last_utc"
    public event EventHandler? Changed;
}

public sealed class PassphraseCheck(IDbStoragePaths paths, IDbKeyProvider current)   // Data
{
    public Task<bool> MatchesAsync(string passphrase, CancellationToken ct = default); // Argon2id col salt, confronto a tempo costante con la chiave aperta
}
```

- Le chiavi nuove non viaggiano (non sono in `TravellingSettingKeys`); se la guardia di `SyncCatalog` vuole ogni chiave
  classificata, vanno in `LocalSettingKeys`. Nessuna migrazione: assenti = predefinite.
- `SecuritySettingsViewModel(IDeviceKeyAccess, SecurityPreferences, …)`: schermo 2a. Apertura (i modi di
  `SupportedModes`), Blocca di nuovo (0, 1, 5, 30, -1; la riga non c'è con `Remembered`), Nascondi nelle app recenti,
  Ripassa la passphrase (0, 7, 14, 30), Cambia passphrase disattivata ("In arrivo"). Ogni scelta con freccia apre una
  pagina di scelte (schermo 2b). Sul telefono entra in `SettingsWindowViewModel` come categoria `Security` (Task 8); sul
  computer no (spec §8.6).
- Android: `OnPause` registra l'ora (`IClock`), `OnResume` chiede `AccessPolicy.ShouldRelock`; se vero, `LockOverlay`
  copre la cornice e chiama `IDeviceKeyAccess.ConfirmUserAsync()` (impronta o passphrase con `PassphraseCheck`).
  Recenti: `Activity.SetRecentsScreenshotEnabled(!HideInRecents)` all'avvio e a ogni `Changed` (API 33+; sotto, niente).
- Ripasso: dopo lo sblocco, se `IsReminderDue` → `PassphraseReminderPage` (schermo 1d). "Controlla" giusto →
  `ReminderLastUtc = now`, chiude; sbagliato → `Reminder_Wrong`; "Non ora" → `ReminderLastUtc = now`.

- [ ] **Step 1: test rossi:** `AccessPolicyTests` (tabella: 0 → sempre; -1 → mai; 5 con 4:59 → no, con 5:00 → sì;
  promemoria 0 → mai; 14 con 13 g → no, 14 g → sì; `lastCheck` null → no);
  `SecuritySettingsViewModelTests` (scrive le chiavi giuste; con `SupportedModes` senza `Biometric` la scelta non c'è;
  la riga "Blocca di nuovo" sparisce con `Remembered`); `PassphraseCheckTests` (giusta → vero, sbagliata → falso, nessun
  file toccato); `PassphraseReminderViewModelTests`; `SecurityPageTests` PNG 360 chiaro e scuro con asserzioni sulle
  quattro righe.
- [ ] **Step 2–3:** implementare; verdi; sul telefono: blocco dopo 1 minuto (provato con quel valore), recenti coperte,
  screenshot possibile, ripasso acceso a mano e provato.
- [ ] **Step 4:** commit `feat(phone): Sicurezza, blocca di nuovo, app recenti, ripasso della passphrase`.

---

### Task 12: Misure da telefono e GIO-11

**Livello:** Sonnet (adatto ad agy). **Fermarsi e salire:** una misura da telefono peggiora la Larga del computer nei PNG.

**Files:**
- Modify: `src/Recordum.UI/Controls/HomeRowsMetrics.cs`, `src/Recordum.UI/Views/Rows/*.axaml`, la striscia del gruppo
  (`src/Recordum.UI/Views/HabitScreen/Group/`), `src/Recordum.Presentation/ViewModels/HomeViewModel.cs` (rilascio delle
  sottoscrizioni), `HomeRowViewModel.cs` (`ExpandToggled` delle righe sostituite)
- Test: `tests/Recordum.UI.Tests/Phone/PhoneMeasuresTests.cs`, `tests/Recordum.Presentation.Tests/.../HomeViewModelDisposeTests.cs`

**Decisioni:** Compatta sul telefono come nel mockup (righe 38, comando 32, iniziali dei giorni); bersagli toccabili
almeno 40 px nell'area attiva anche dove il disegno è più piccolo; nella striscia del gruppo i nomi non si troncano a
due lettere a 360 px (sonda, punto 7: si accorciano le colonne o si va a capo, non si taglia il nome). GIO-11:
`HomeViewModel` implementa `IDisposable` e rilascia le sette sottoscrizioni al bus; le righe sostituite da un
ricaricamento perdono l'abbonamento a `ExpandToggled`.

- [ ] **Step 1: test rossi:** `PhoneMeasuresTests`: alla fixture "manifesto", 360 px, Compatta: ogni comando della riga
  ha un'area attiva ≥ 40 × 40; nessun nome di riga della striscia del gruppo ridotto a meno di 4 caratteri visibili;
  PNG `phone-home-compact-{light,dark}.png`. `HomeViewModelDisposeTests`: dopo `Dispose` un evento del bus non arriva
  più; dopo un ricaricamento, una riga vecchia che alza `ExpandToggled` non tocca la Home.
- [ ] **Step 2–3:** implementare; verdi; PNG del computer (Larga) invariati a occhio; voce GIO-11 a `fixed` nel tracker.
- [ ] **Step 4:** commit `fix(phone): misure da telefono; GIO-11 le sottoscrizioni della Home`.

---

### Task 13: Avvio sotto i 3 s

**Livello:** Sonnet + io sul telefono. **Fermarsi e salire:** dopo i passi 1–3 si è ancora sopra i 3 s (decide l'AOT
con l'autore, guardando peso e tempo).

**Files:** dipendono da ciò che misura il passo 1; candidati `src/Recordum.Data/Recordum.Data.csproj` (modello compilato
generato dalla build: `EFOptimizeContext`, pacchetto `Microsoft.EntityFrameworkCore.Tasks` 10.0.12, o il comando
`dotnet ef dbcontext optimize` lanciato da uno script con una guardia che fallisce se il modello è vecchio),
`src/Recordum.UI/Bootstrap/AppStartup.cs` (riscaldamento del modello EF in parallelo al riquadro dell'impronta).

- [ ] **Step 1:** misura dopo i Task 10–12 (5 avvii a freddo, mediana; a parte il riquadro dell'impronta) e i tempi per
  passo dalla riga `startup`. Scritti nel §16 della spec.
- [ ] **Step 2:** il passo più lungo per primo. Se è `core-init` e dentro il modello EF: modello compilato generato dalla
  build; test `CompiledModelIsCurrentTests` (il modello compilato e quello costruito a runtime hanno le stesse entità,
  tabelle e colonne; rosso provato aggiungendo una proprietà mappata senza rigenerare).
- [ ] **Step 3:** riscaldamento del modello mentre il dito è sul sensore (non richiede la chiave).
- [ ] **Step 4:** misura di nuovo; se ≥ 3 s, fermarsi e portare all'autore la tabella con l'opzione AOT (peso dell'APK
  e tempo misurati con una build di prova).
- [ ] **Step 5:** commit `perf(startup): <ciò che si è fatto>` con i numeri nel messaggio.

---

### Task 14: Chiusura dell'onda

**Livello:** Opus per la review dell'intero branch; Haiku per i documenti.

- [ ] **Step 1:** `docs/CHECKLIST.md` §5 "Telefono": avvio misurato, impronta (anche dopo un'impronta aggiunta), blocca
  di nuovo, recenti, ripristino dal `.recbak` del computer, aggiornamento dell'APK con `install-phone.sh` senza perdere
  dati, rotazione e tema chiaro/scuro senza riavvio (Review Focus 1), indietro dappertutto, foglio sopra la tastiera.
- [ ] **Step 2:** CLAUDE.md: §4 le due teste e `IAppHead`; §7 i test del telefono a 360 × 780; §10 build dell'APK,
  `install-phone.sh`, la chiave di firma, le trappole della testa Android (using impliciti, `GlobalPropertiesToRemove`,
  APK di debug doppio). `docs/ARCHITECTURE.md`. `docs/ROADMAP.md` ("Dove siamo", passo 4).
- [ ] **Step 3:** suite intere verdi in locale (portabili), CI verde (Linux, Windows, Android).
- [ ] **Step 4:** review dell'intero branch (Opus, o tre modelli agy come per "unisci"), correzioni, di nuovo CI.
- [ ] **Step 5:** PNG del telefono all'autore; prova dell'autore sul telefono con i dati veri.
- [ ] **Step 6:** PR verso `main` (dopo il merge di `unisci`, ribasata), merge dell'autore.

---

## Parole

| Chiave | English | Italiano |
|---|---|---|
| `Startup_Waiting_Opening` | Opening your data… | Apro i tuoi dati… |
| `Startup_Waiting_Preparing` | Getting your day ready… | Preparo la giornata… |
| `Startup_CopyDetails` | Copy details | Copia i dettagli |
| `Phone_Tab_Today` | Today | Oggi |
| `Phone_Tab_Search` | Search | Cerca |
| `Phone_Tab_Statistics` | Statistics | Statistiche |
| `Phone_Tab_Settings` | Settings | Impostazioni |
| `Phone_ComingSoon` | Coming soon | In arrivo |
| `Phone_Search_ComingSoon` | Search across everything is on its way. For now, use the lens on Today. | La ricerca in tutto arriva presto. Per ora c'è la lente in Oggi. |
| `Phone_Statistics_ComingSoon` | Charts are built on the computer. You'll be able to view them here. | I grafici si costruiscono al computer. Qui si potranno guardare. |
| `Biometric_Title` | Unlock your data | Sblocca i tuoi dati |
| `Biometric_Subtitle` | Use your fingerprint | Usa l'impronta |
| `Biometric_UsePassphrase` | Use passphrase | Usa la passphrase |
| `Unlock_Declined_KeyInvalidated` | A fingerprint was added to this phone. For safety, this time we need your passphrase. | È stata aggiunta un'impronta a questo telefono. Per sicurezza, questa volta serve la passphrase. |
| `Unlock_Declined_Cancelled` | Fingerprint unlock was cancelled. | Sblocco con l'impronta annullato. |
| `Unlock_Declined_TooManyAttempts` | Too many fingerprint attempts. Use your passphrase. | Troppi tentativi con l'impronta. Usa la passphrase. |
| `Unlock_Declined_Unavailable` | Fingerprint isn't available on this phone right now. | L'impronta ora non è disponibile su questo telefono. |
| `Unlock_RetryBiometric` | Try fingerprint again | Riprova con l'impronta |
| `Unlock_Biometric_Enable` | Unlock with fingerprint | Sblocca con l'impronta |
| `Unlock_Biometric_ReEnable` | Unlock with fingerprint again | Torna a sbloccare con l'impronta |
| `Unlock_Biometric_Fallback` | Your passphrase stays as the fallback. | La passphrase resta il ripiego. |
| `Security_Title` | Security | Sicurezza |
| `Security_Group_Opening` | Opening | Apertura |
| `Security_Mode_Biometric` | With fingerprint | Con l'impronta |
| `Security_Mode_Biometric_Hint` | Default. Your passphrase stays as the fallback. | Predefinita. La passphrase resta il ripiego. |
| `Security_Mode_Remembered` | Without asking | Senza chiedere |
| `Security_Mode_Remembered_Hint` | The phone's screen lock is enough. Your data stays encrypted on disk. | Basta il blocco schermo del telefono. I dati restano cifrati sul disco. |
| `Security_Mode_Passphrase` | Passphrase every time | Passphrase ogni volta |
| `Security_Mode_Passphrase_Hint` | The most locked down. About a second longer to open. | La più chiusa. Circa un secondo in più all'apertura. |
| `Security_Group_WhileUsing` | While you use it | Mentre la usi |
| `Security_Relock` | Lock again | Blocca di nuovo |
| `Security_Relock_Hint` | When you come back after leaving, it asks again. | Quando torni dopo essere uscito, chiede di nuovo. |
| `Security_Relock_Immediately` | Immediately | Subito |
| `Security_Relock_Minutes` | After {0} minutes | Dopo {0} minuti |
| `Security_Relock_OneMinute` | After 1 minute | Dopo 1 minuto |
| `Security_Relock_OnClose` | Only when the app closes | Solo quando l'app si chiude |
| `Security_Relock_OnClose_Hint` | While Android keeps it open, it stays unlocked. | Finché Android la tiene aperta, resta sbloccata. |
| `Security_HideInRecents` | Hide in recent apps | Nascondi nelle app recenti |
| `Security_HideInRecents_Hint` | The preview stays covered | L'anteprima resta coperta |
| `Security_Reminder` | Practice your passphrase | Ripassa la passphrase |
| `Security_Reminder_Never` | Never | Mai |
| `Security_Reminder_Weekly` | Every week | Ogni settimana |
| `Security_Reminder_TwoWeeks` | Every 2 weeks | Ogni 2 settimane |
| `Security_Reminder_Monthly` | Every month | Ogni mese |
| `Security_ChangePassphrase` | Change passphrase | Cambia passphrase |
| `Reminder_Title` | Do you remember your passphrase? | Ti ricordi la passphrase? |
| `Reminder_Body` | With your fingerprint you never type it, and it's easy to forget. If one day the fingerprint isn't enough, without the passphrase your data is lost. | Con l'impronta non la scrivi mai, ed è facile dimenticarla. Se un giorno l'impronta non bastasse, senza passphrase i dati sono persi. |
| `Reminder_Check` | Check | Controlla |
| `Reminder_NotNow` | Not now | Non ora |
| `Reminder_Right` | That's the one. | È quella giusta. |
| `Reminder_Wrong` | That's not it. Try again. | Non è questa. Riprova. |
| `Lock_Title` | Recordum is locked | Recordum è bloccato |
| `Lock_Unlock` | Unlock | Sblocca |

Le parole passano dalla guardia del glossario (`docs/GLOSSARY.md`); dove una chiave esiste già con lo stesso testo
("Impostazioni", "Annulla", "In arrivo"), si riusa quella.

## Registro

Il registro dell'esecuzione (decisioni prese strada facendo, misure, esiti delle sonde) sta in
`.superpowers/sdd/2026-11-24-telefono-onda-4/progress.md`, come per "unisci".
