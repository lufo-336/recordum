# Recordum — contesto per Claude Code

> **Perché** esiste: `docs/PRODUCT.md`. **Dove siamo**: `docs/ROADMAP.md`. Questo file dice **come si lavora** qui: le
> regole e le trappole già pagate. La cronaca delle sessioni sta in `docs/MILESTONES.md`, non qui.

## 1. Cos'è

Editor di abitudini offline, cifrato, senza account: cinque tipi (azione, contatore, astinenza, diario, stato) che si
compongono in gruppi. Serve chi vuole *capire* la propria vita (salute, allenamento, scrittura, astinenza, diario), non
gamificarla. Autore: Luca Foglia (ITS, Torino). Licenza MIT. Namespace `Recordum`.

## 2. Principi (non negoziabili)

1. **Offline-first**: dati sul dispositivo; la sync passerà dal cloud dell'utente. Un server nostro è un'ipotesi lontana
   anni e solo opzionale: l'app non ne dipenderà mai.
2. **Nessun account obbligatorio.**
3. **Privacy by default**: AI spenta al primo avvio; niente esce dal dispositivo senza consenso informato.
4. **Complessità per composizione**: gruppi di abitudini semplici, mai opzioni gonfiate sulla singola abitudine.
5. **Profondità senza intimidazione**: uso base in 30 s, il resto con progressive disclosure.
6. **Open source**, niente paywall sulle funzioni base.
7. **English-first**: ogni stringa utente in `.resx` dalla prima riga (inglese, poi italiano).
8. **Template = composizione salvata**: se un template fa X, l'utente può fare X nel builder, e ogni composizione si salva
   come template (guardia: `BuilderTemplateParityTests`; un template nuovo vuole una fixture lì).

In dubbio: privacy > convenienza · composizione > opzioni · profondità nascosta > schermata che nasconde funzioni ·
esplicito > implicito (soprattutto cascade e mapping EF).

## 3. Dove siamo

v1.0 in arrivo **su Avalonia** (Windows + Linux + un APK Android, deciso il 2026-11-10); la WPF è spenta (2026-11-24,
spec telefono T3: rimossa dal repo, F8 anticipato). Checklist a schermo dell'autore: `docs/CHECKLIST.md`. Stato e prossimo passo:
`docs/ROADMAP.md` — **a fine sessione si aggiorna quella pagina**, non si aggiunge cronaca qui. Findings: tracker
`docs/phase-4-issues.md` (stati `reported / verified / triaged / in-progress / fixed / verified-fixed`).

## 4. Architettura

| Progetto | Ruolo |
|---|---|
| `Recordum.Core` | dominio e astrazioni (`IClock`, `DayBoundary`, contratti di sicurezza). C# puro. |
| `Recordum.Data` | EF Core + SQLite3MC (formato SQLCipher 4), servizi, query, migrazioni, backup/restore. |
| `Recordum.Plugins.Contracts` / `.Plugins` | contratti e host in-process dei plugin first-party. |
| `Recordum.Presentation` | `net10.0`, **zero UI**: ViewModel (CommunityToolkit.Mvvm), `Strings`, servizi portabili, bootstrap (`AddRecordumCore`, `CoreInitializer`, `RecordumOptions`). Referenzia Data: i ViewModel usano i servizi di query direttamente. |
| `Recordum.UI` | libreria **Avalonia 12** con tutte le viste — la v1.0. |
| `Recordum.Desktop` | testa del computer (Windows, Linux): `Program`, `DesktopHead`. |
| `Plugins.ChartEngine` / `.ExportEngine` | grafici (oggi senza renderer: quello WPF è sparito, quello Avalonia è F7) ed export (PDF, HTML, MD, JSON, CSV). |

Dettagli: `docs/ARCHITECTURE.md`. Autorità tecnica: il codice, le migrazioni e `ARCHITECTURE.md`; per il sistema plugin,
provenance e cifratura la spec `docs/superpowers/specs/2026-05-13-plugin-system-design.md` (§2.4, §11). I `.docx` di un
tempo non fanno più testo.

**Il confine di Presentation lo impone il compilatore**: TFM `net10.0`, mai `-windows` (quell'errore è il confine che
lavora); `NoWpfReferenceTests` asserisce `TargetPlatformAttribute` null. Un ViewModel che ha bisogno dell'host lo chiede
tramite un'astrazione (`ITickSource`, `IApplicationLifetime`, `IClipboard`, `IEditorAvailability`, `IConfirmationDialog`,
dialog e launcher) e non legge mai un pennello da un dizionario di risorse (dichiara un'intenzione, es. `MarkerTint`). Ogni
astrazione nuova: una per testa (Desktop, Android) e un test della registrazione DI — un servizio mancante compila e muore
all'avvio.

**Plugin**: un servizio che crea entità per conto di un plugin lo fa dentro uno scope con `IPluginContext.PluginId`
valorizzato; le funzioni first-party (grafici, export) passano da `IExtensionPoint`, senza scorciatoie.

## 5. Dominio e dati — invarianti

- Tutto è un `HabitNode` (albero via `parent_id`): abitudine o gruppo. **Astinenza** = periodi in `abstinence_period`
  (`IAbstinenceService`), non completion. **Sessione** = contatore con `timer_mode='session'`, non un sesto tipo.
- **Il giorno di un istante si chiede a `DayBoundary`** (da `IResetTimeResolver`: reset dell'abitudine → dei gruppi →
  `daily_reset_time`). Si risolve **una volta** in cima all'operazione e si passa giù; per più abitudini la versione
  batch. Una retroattiva si stampiglia a `NoonOf(giorno)`. Mai `DateOnly.FromDateTime(clock.UtcNow…)`, mai finestre a
  mezzanotte UTC (`DayDerivationGuardTests`, `DayBoundaryParityTests`). `reset_time` non è versionato: cambiarlo
  reinterpreta lo storico.
- **Mai `DateTime.UtcNow/Now`**: il tempo passa da `IClock`.
- `TimestampInterceptor` compila `CreatedAt`, `UpdatedAt`, `UpdatedAtMs` e `CreatedByPluginId`: i servizi non li scrivono.
- Scale: gli scaglioni (`scale_anchors`) sono l'unica fonte dei nomi; il valore salvato è il valore vero in `value_decimal`.
- `active_timer`: un solo timer per abitudine (UNIQUE), ed è il perno del recupero dopo un crash.
- `HabitConfigHistory` fotografa ogni modifica della config (tranne id, timestamp, `quick_complete`, `ai_data_sharing`,
  `reset_time`, `streak_broken_at`).
- SQLite: booleani INTEGER 0/1; date TEXT ISO 8601; `updated_at_ms`/`completed_at_ms` INTEGER Unix ms (LWW per la sync);
  enum TEXT + CHECK; JSON in TEXT. `Note` ha solo `updated_at_ms`.
- `created_by_plugin_id` solo sulle entità top-level (`habit_node`, `habit_template`, `export_template`, `chart`), mai
  sugli eventi figli. Soft delete solo su `HabitNode` (filtro globale; `IgnoreQueryFilters()` per cestino e diagnostica).
- snake_case; cascade e indici sempre espliciti in Fluent API. Schema **2.21**. Chi scrive `HabitConfig`/`Goal` fuori dai
  servizi si porta il proprio gate (`ValidateCrossFields`, `ck_goal_period_consistency`).
- **Unisci (`SyncCatalog`)**: ogni tabella nuova (e colonna, chiave di `app_settings` o indice unico) va classificata in
  `SyncCatalog` (la guardia lo impone); le scritture su tabelle che viaggiano passano dai trigger (un build senza
  `SyncConnectionInterceptor` non può scrivere: "no such function"); una migrazione che ricostruisce `sync_tombstone`
  toglie prima i `trg_sync_*`; durante l'unione `SyncConnectionState.Applying` va acceso solo sulla connessione aperta da EF.
- **Migrazioni**: `dotnet ef migrations add <Nome> --project src/Recordum.Data --startup-project src/Recordum.Data`
  (startup = Data). Nel file generato aggiungere `#pragma warning disable CA1861` dopo `#nullable disable`. Un
  bump di `schema_version` tocca 8 riferimenti nei test, nomi dei metodi compresi. Mai editare a mano `*.Designer.cs` e
  `*ModelSnapshot.cs` di EF.

## 6. Sicurezza e dati dell'utente

- Chiave del DB sempre da `IDbKeyProvider`: DPAPI su Windows; altrove passphrase Argon2id dietro `CachingKeyProvider`
  (l'interceptor chiede la chiave a ogni connessione). Passphrase persa = dati persi. La frase dell'unione sta in
  `sync.passphrase`, mai fuori dal DB.
- All'avvio, in quest'ordine: `RecordumOptions.Resolve(args, env)` (`--data-dir` / `RECORDUM_DATA_DIR`),
  `DataDirectoryLock.TryAcquire`, `ReconcileOrphans()`, `DetectState()`. Un salt esistente non si tronca mai.
- Passphrase sbagliata si riconosce con una **lettura** (`SqliteException` 26), non con l'apertura; `ClearAllPools()` fra
  i tentativi (una connessione in pool già chiavata ignora la chiave sbagliata).
- Su Unix cartella dati 0700; i file in chiaro di backup/restore solo in `TempDirectory`, mai nella temp di sistema
  (`SystemTempGuardTests`). `--seed-demo` rifiuta la cartella dei dati veri.
- Provider: `SQLite3MC.PCLRaw.bundle` (D9), un solo bundle SQLitePCLRaw nel grafo (`.Sqlite.Core`, mai `.Sqlite`). Il
  formato dei file resta **SQLCipher 4**: ogni chiave passa da `CipherPragmas` (senza, MC usa ChaCha20 e un DB di oggi
  risponde 26 come una passphrase sbagliata). Niente `sqlcipher_export`: backup = `VACUUM INTO` + `rekey ''`. Un journal
  caldo del bundle di prima MC non lo annulla (lo scarta): `install-linux.sh` rifiuta di aggiornare sopra un `-journal`.
  La guardia di SEC-1 (`SqlCipherProviderGuardTests`: `sqlite3mc_version` c'è, SQLite ≥ 3.50) deve restare verde.
- Prima di rendere pubblico qualcosa: `/security-review`.

## 7. Test

- **Un test di regressione si prova contro il codice rotto**: si rompe, si vede il rosso, si ripristina.
- Un parametro di stato nuovo va provato con un caso in cui il **default** risponde diverso (il "parametro muto").
- Un difetto di conversione o formattazione **si misura**, non si deduce.
- Test che confrontano etichette localizzate: fissare la cultura (in inglese alcune etichette coincidono con l'enum).
- Su Windows, per leggere un file che SQLite tiene aperto: `FileShare.ReadWrite`.
- Test che dipendono da DPAPI: `[Trait("Category", "WindowsOnly")]`; su Linux `--filter "Category!=WindowsOnly"`.
- Filtri `dotnet test` per **nome di classe**, e controllare il totale eseguito: un filtro vuoto sembra verde.
- Quando cambia una firma, l'elenco dei chiamanti lo dà il compilatore, non un `grep`: un `new(...)` senza tipo li nasconde.
- **Avalonia**: `InitializeComponent()`, mai `AvaloniaXamlLoader.Load(this)`; `x:DataType` su ogni view e template
  (binding compilati); un controllo che resta 0x0 in headless di solito ha perso il suo tema (il "priming" di un tempo
  copriva lo `StyleInclude` di AvaloniaEdit mancante in `App.axaml`); dopo un'azione `RunJobs()` +
  `ForceRenderTimerTick()`, e un PNG non prova niente senza un'asserzione strutturale; un `[Fact]` che
  tocca tipi Avalonia va reso `[AvaloniaFact]`; l'assembly UI gira senza parallelismo. `Recordum.UI.Tests` è xunit v3
  (serve `using Xunit;`, mai referenziare `Presentation.Tests`).

## 8. Trappole di interfaccia già pagate

- Un `ContentControl` con `ContentTemplate` statico costruisce il template anche con `Content` null o nascosto: serve un
  selettore che torni null o un `Content` davvero null. Verificare cercando nell'intera view.
- Mai svuotare una collezione che è `ItemsSource` bindata two-way senza sospendere la persistenza e riaffermare il valore
  dopo (`4d01f9c`: dati azzerati).
- `AsyncRelayCommand.ExecuteAsync` non consulta `CanExecute`: la guardia va anche dentro il metodo.
- La cultura si fissa una volta per processo, quindi cambiarla richiede il riavvio. Avalonia con binding compilati usa la
  cultura del thread.

## 9. Come si lavora

- **Branch**: si lavora su un branch; il merge in `main` lo esegue l'autore (da una sessione Claude l'aggiornamento di
  `main` è bloccato). L'hook `guard-main.js` legge il branch *prima* del comando: cambiare branch in una chiamata separata.
- Aggiornato un `.md` → commit e push subito sul branch di lavoro.
- **Hook del progetto** (`.claude/hooks/`): niente commit o push su `main`; niente modifiche a mano ai file generati da
  EF; cancellare i dati dell'utente (cartella dati, `.recbak`) chiede conferma.
- **Stringhe**: `Strings.resx` + `Strings.it.resx` + accessor in `src/Recordum.Presentation/Resources/Strings.Designer.cs`,
  che è mantenuto a mano e si edita **solo via shell** (l'hook `guard-generated.js` blocca Edit/Write sui `*.Designer.cs`).
- **Piani**: decisioni, interfacce, obblighi di test e trappole — non codice da ricopiare. Ogni onda si chiude con una
  review dell'intero branch (trova ciò che le review per task non vedono) e con la CI verde.
- **Macchine**: Fedora (sviluppo quotidiano), VM Windows (controlli a schermo), Mac secondario. Cosa viaggia e cosa
  no: `docs/dev-environment-sync.md`. Il plugin `superpowers` va installato su ogni macchina.

## 10. Build, test, CI

```bash
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"   # Linux, SDK utente
dotnet build Recordum.slnx -p:EnableWindowsTargeting=true
dotnet test tests/Recordum.Data.Tests --filter "Category!=WindowsOnly"                # Linux
```

- **Mai `-p:UseAppHost=false` da riga di comando**: è già il default in `Directory.Build.props`, e da riga di comando
  rompe `Recordum.UI.Tests` (xunit v3 vuole l'apphost). RID per host (win-x64 / linux-x64), `SelfContained=false`.
- Versioni dei pacchetti **esatte** in `Directory.Packages.props` (niente wildcard: SEC-1 è nato da uno). EF Core 10.
- `TreatWarningsAsErrors` è globale: un warning nuovo rompe la build.
- **CI** (`.github/workflows/ci.yml`): job Linux (suite portabili) e Windows (le stesse più i `WindowsOnly`); i PNG headless negli
  artefatti; i push di sola documentazione non la fanno partire. I test di prestazioni (`Category=Performance`) non girano
  in CI: su un runner condiviso i tempi oscillano; si eseguono in locale. Un push che tocca `.github/workflows/` vuole lo scope
  `workflow` nel token di `gh`.
- La regola `Backup*/` del `.gitignore` di Visual Studio esclude qualunque cartella `Backups/`.
- VM Windows su Google Drive: `git status` può mostrare file modificati che non lo sono; si sblocca con
  `git checkout -- <file>`.

## 11. Linguaggio visivo

Serio ma caldo; tipografia e spazio; serif (Source Serif 4) per diario e marchio; il colore identifica un'abitudine (barra
di 4 px sulla riga, heatmap a 5 gradini), non decora; icone Lucide, mai emoji nell'interfaccia; chiaro e scuro curati
uguali; movimento contenuto; niente gamification. Editor delle abitudini a tre livelli: sempre visibili nome, tipo, colore,
icona; sezioni chiuse per il resto; opzioni rare dietro "Avanzate". Dettagli: `docs/PRODUCT.md` §"Come si presenta".
