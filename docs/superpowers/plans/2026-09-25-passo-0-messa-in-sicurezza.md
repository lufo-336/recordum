# Passo 0 — Messa in sicurezza della base — piano

> **Formato** (decisione D5 dell'autore, 2026-09-25): decisioni, interfacce, obblighi di test, trappole. Il codice lo scrive chi
> esegue, dentro questi confini. **Spec**: la revisione `docs/superpowers/specs/2026-09-25-avalonia-v1-plan-review.md` (§1-§2 per
> i problemi, §6 Passo 0, §9 decisioni). **Branch**: `avalonia-linux-slice`. **Ambiente**: Linux (Fedora), vincoli in CLAUDE.md §8
> e nei *Global Constraints* del piano F1 — in particolare: mai `-p:UseAppHost=false` da riga di comando; `Strings.Designer.cs` si
> edita solo via shell; ogni test di regressione si prova rosso contro il codice rotto; niente commit su `main` (i merge in `main`
> li esegue l'autore).

## Decisioni già prese (non si ridiscutono qui)

| # | Decisione | Fonte |
|---|---|---|
| A | "Salt senza database" → **recupera o accantona**: se esiste `recordum.db.old` (restore interrotto) torna al suo posto; altrimenti il salt si sposta in `db.salt.orphan-<yyyyMMdd-HHmmss>` e si crea un DB nuovo. Niente si distrugge, tutto va nel log. | autore, 2026-09-25 |
| B | Dati di sviluppo isolati con un **override esplicito**: `--data-dir <path>` o `RECORDUM_DATA_DIR`; il default resta la cartella vera; `--seed-demo` **rifiuta** di seminare nella cartella di default. | autore, 2026-09-25 |
| C | **CI adesso**, sul repo privato: job Linux + job Windows, esclusi i push di sola documentazione. | autore, 2026-09-25 |
| D | Niente lock file NuGet: con il RID per-host (win-x64 / linux-x64) il lock file cambia per sistema e `--locked-mode` fallirebbe sull'altro. Bastano versioni **esatte** in Central Package Management. | questo piano (ponytail) |

## Stato

| Task | Cosa | Stato |
|---|---|---|
| 0.1 | Riconciliare R9 nella pila | ✅ `28ff31e` (949 verdi in Presentation, due test riprovati rossi) |
| 0.2 | CI GitHub Actions | ✅ primo run `36154875389`: Linux verde; Windows con due rossi, **entrambi difetti di test** (condivisione del file su Windows, test che dipendeva dalla lingua) — corretti in `f27d3a7`. Nessuna regressione di F0: la suite WPF dopo la rinomina ha girato su Windows |
| 0.3 | Backup e restore testati anche su Linux | ✅ fixture con chiave fissa; 10 test di backup/restore girano su Linux per la prima volta; `CrossMachineRestoreTests` + `Fixtures/CrossMachine/made-on-linux-v2.18.recbak` (non `Backups/`: il `.gitignore` di Visual Studio esclude `Backup*/`, ed è la CI ad averlo scoperto) |
| 0.4 | Sblocco sicuro | ◐ primitive in Data (`b933731`): `ReconcileOrphans`, salt `CreateNew` + fsync, log dell'eccezione, `DataDirectoryLock` — sette test, tutti provati rossi. **Restano i collegamenti**: avvio Avalonia (Task 11 di F1) e WPF (`App.OnStartup`). Il `db.key` DPAPI non cambia: la sua corsa la chiude il lock |
| 0.5 | Privacy dei file su Linux | ✅ `0b7d0c8`: cartelle 0700 (anche se esistevano più larghe), `TempDirectory` dentro la cartella dati, `SystemTempGuardTests`, resti dei backup interrotti distrutti con `SecureWipe` anche nella vecchia posizione |
| 0.6 | Cartella dati sovrascrivibile | ✅ `0b7d0c8`: `RecordumOptions.Resolve(args, env)` (WPF lo usa già; Avalonia al Task 11), `AttachmentService` segue le opzioni, `--seed-demo` rifiuta i dati veri, hook per Linux |
| 0.7 | Chiave dell'auto-backup dietro un'astrazione | ☐ (la metà Linux aspetta D3) |
| 0.8 | Versioni esatte + EF Core 10 | ☐ entro il 2026-11-10 |
| 0.9 | Sonda sul provider di cifratura | ☐ (decisione D9 sul risultato) |
| 0.10 | Documenti: CLAUDE.md ≤ 15 KB, indice del tracker, ledger | ☐ |

---

## 0.2 — CI GitHub Actions

**Interfacce**: `.github/workflows/ci.yml`; un tratto `[Trait("Category", "WindowsOnly")]` sulle classi di test che non possono
girare fuori da Windows.

**Decisioni di dettaglio**
- Trigger: `push` su ogni branch e `pull_request`; `paths-ignore` per `**/*.md` e `docs/**` (l'autore pusha a ogni `.md`: quei push
  non devono consumare minuti). `concurrency` per branch con `cancel-in-progress`.
- Job **linux** (`ubuntu-latest`): SDK .NET 10; `dotnet build Recordum.slnx -p:EnableWindowsTargeting=true`; `dotnet test` su Core,
  Data, Presentation, UI, Plugins, Plugins.ExportEngine, con `--filter "Category!=WindowsOnly"`.
- Job **windows** (`windows-latest`): build + tutte le suite, WPF **serializzata** (`-- xUnit.ParallelizeTestCollections=false`,
  gotcha W24-E1), ChartEngine compresa.
- Artefatti: i PNG di `tests/Recordum.UI.Tests/bin/**/Screenshots/` (sempre) e i `.trx` (se qualcosa fallisce).
- `timeout-minutes` su ogni job (W24-E1: la suite WPF può appendersi).
- Classi `WindowsOnly` al primo giro — i 18 fallimenti misurati su Linux il 2026-09-25: `DpapiKeyProviderTests`,
  `KeyTransitionServiceTests`, `BackupSchedulerTests`, `BackupServiceTests`, `RestoreServicePreviewTests`,
  `RestoreServiceRestoreTests`, `AttachmentBackupRestoreTests`, `AttachmentEndToEndTests`. Il task 0.3 ne toglie la maggior parte.

**Obblighi**: il primo run va letto per intero. Il job Windows eseguirà per la prima volta la suite WPF dopo la rinomina di F0
(F0-D1): un rosso lì non è un difetto della CI ma il debito che la CI è venuta a riscuotere — si trascrive a tracker e si corregge in
un task a parte.

**Trappole**: `UseAppHost` resta quello di `Directory.Build.props` (mai da riga di comando: rompe `Recordum.UI.Tests`); un filtro che
non seleziona niente sembra verde (controllare il totale eseguito).

## 0.3 — Backup e restore testati anche su Linux

**Il problema**: tutti i test di backup e restore dipendono da `EncryptedDbFixture`, che chiede la chiave a DPAPI; su Linux falliscono
tutti, quindi backup e restore — il ponte fra le due macchine del dogfooding (D1) — non sono mai stati eseguiti fuori da Windows.

**Decisioni**: la fixture ricava la chiave da un provider portabile (passphrase + salt temporaneo, attraverso `CachingKeyProvider`
per non pagare Argon2id a ogni connessione). DPAPI resta testato solo dove esiste davvero: `DpapiKeyProviderTests`, i rami DPAPI di
`KeyTransitionServiceTests` e `BackupSchedulerTests` (quest'ultimo fino al task 0.7) restano `WindowsOnly`.

**Obblighi di test**
- Le classi di backup e restore tolgono il tratto `WindowsOnly` e girano verdi su Linux.
- **Restore incrociato**: un `.recbak` minimo, creato una volta e committato come fixture, si ripristina da un test che gira in
  **entrambi** i job della CI. È la prova che un file fatto su un sistema si apre sull'altro.

## 0.4 — Sblocco sicuro (RV-3)

**Interfacce**
- `DbUnlockService.WriteSalt`: `FileMode.CreateNew` (mai troncare) e `Flush(flushToDisk: true)` prima di chiudere.
- Riconciliazione dei file orfani (decisione A), eseguita **prima** di `DetectState` e mai dentro (`DetectState` resta senza effetti
  collaterali): nuovo metodo pubblico su `IDbUnlockService` che restituisce cosa ha fatto, per il log.
- `DpapiKeyProvider`: la creazione della chiave usa `CreateNew`; se perde la corsa, legge la chiave dell'altro invece di
  sovrascriverla.
- `DataDirectoryLock` (in `Recordum.Data/Security`): lock esclusivo su `<cartella dati>/recordum.lock` tenuto per la vita del
  processo; `TryAcquire` restituisce il lock o l'informazione che un'altra istanza è aperta. Il collegamento all'avvio Avalonia è
  un'aggiunta al Task 11 di F1; quello a WPF (`App.OnStartup`) è la correzione di un difetto di dati, ammessa dal congelamento, e si
  verifica col job Windows della CI.
- `DbUnlockService` riceve un `ILogger` e registra l'eccezione nel `catch` di `UnlockAsync` (oggi la inghiotte).

**Obblighi di test**, ognuno provato rosso contro la rottura corrispondente:
- due chiamate `UnlockAsync(NeedsCreation, …)` in corsa sulla stessa cartella: **mai** due salt diversi, e il DB creato resta
  apribile con la passphrase di chi ha vinto;
- salt esistente + creazione → nessuna troncatura;
- orfano con `recordum.db.old` → recuperato, stato `NeedsPassphrase`; orfano senza `.old` → salt accantonato, stato `NeedsCreation`,
  file `db.salt.orphan-*` presente;
- secondo `TryAcquire` sulla stessa cartella → rifiutato; dopo `Dispose` del primo → concesso.

**Trappola**: su Unix `FileShare.None` è un lock *advisory* (`flock`) — vale fra processi .NET, non contro un `rm`. È quello che
serve (due istanze dell'app), non una protezione dei file.

## 0.5 — Privacy dei file su Linux (RV-4)

**Interfacce**
- `RecordumOptions`: su Unix la cartella dati e le sottocartelle nascono `0700`, e una cartella già esistente con permessi più larghi
  viene ristretta (`File.SetUnixFileMode`).
- `IBackupStoragePaths` guadagna `TempDirectory` (sotto la cartella dati, `0700`); `BackupService` e `RestoreService` smettono di
  usare `Path.GetTempPath()`.

**Obblighi di test** (su Unix; su Windows il test si limita a verificare che il percorso sia sotto la cartella dati): i modi `0700`
misurati con `File.GetUnixFileMode`; un backup non lascia nulla fuori dalla cartella dati né durante né dopo (spia sulla cartella
temp del sistema).

## 0.6 — Cartella dati sovrascrivibile (RV-6, decisione B)

**Interfacce**
- `RecordumOptions.Resolve(string[] args, Func<string, string?> env)` (il `Default()` di oggi resta come caso senza override):
  `--data-dir` vince su `RECORDUM_DATA_DIR`, che vince sul default. Espone `IsDefaultLocation`.
- `AttachmentService` riceve la radice dei dati dalle opzioni **in entrambe le registrazioni** (Presentation e WPF): oggi se la ricava
  da `LocalApplicationData` per conto suo, quindi con un override le foto finirebbero comunque nella cartella vera.
- `DemoDataSeeder`: se `IsDefaultLocation`, rifiuta con un messaggio che dice come passare `--data-dir`.
- `.claude/hooks/guard-userdata.js` riconosce anche `~/.local/share/Recordum` e `$XDG_DATA_HOME/Recordum`.

**Obblighi di test**: precedenza dei tre livelli; con l'override, un allegato finisce sotto la cartella indicata (non sotto quella
di default); il seeder rifiuta la cartella di default.

## 0.7 — Chiave dell'auto-backup dietro un'astrazione (RV-5)

**Interfacce**: `BackupScheduler` (in Data) smette di chiamare `ProtectedData` e chiede la passphrase a un'astrazione portabile
(oggi `IAutoBackupKeyStore` vive in Presentation: spostarla dove Data possa vederla, o passare la passphrase dal chiamante — scegliere
la forma col diff più piccolo). L'auto-spegnimento resta, ma **si registra nel log** e lascia uno stato leggibile dalle Impostazioni.

**Rinviato a D3**: l'implementazione Linux del key store (portachiavi di sistema o file `0600`). Fino ad allora su Linux
l'auto-backup resta spento **e lo dice**.

## 0.8 — Versioni esatte + EF Core 10 (RV-9, RV-8)

**Interfacce**: `Directory.Packages.props` con `ManagePackageVersionsCentrally`; ogni `PackageReference` perde `Version`; versioni
**esatte**, prese da quelle risolte oggi (`obj/project.assets.json`) così che il primo commit non cambi comportamento. Poi, in un
commit separato, EF Core, `EFCore.NamingConventions`, `Microsoft.Extensions.DependencyInjection` e `ProtectedData` passano a 10.x.

**Obblighi**: dopo il salto a EF 10, `dotnet ef migrations has-pending-model-changes --project src/Recordum.Data --startup-project
src/Recordum.Data` pulito; suite Data e Presentation verdi su Linux, CI verde su Windows; `SqlCipherProviderGuardTests` verde (SEC-1
non deve riaprirsi). Il commento su NU1903 in `Directory.Build.props` va riscritto (RV-2): dopo SEC-1 `e_sqlite3` non è più nel
grafo.

## 0.9 — Sonda sul provider di cifratura (RV-2)

Un rapporto in `docs/design/` che risponde, misurando: (1) quale sostituto di `bundle_e_sqlcipher` (build Zetetic; SQLite3 Multiple
Ciphers) apre un DB SQLCipher 4 creato oggi; (2) cosa sostituisce `sqlcipher_export` in backup e restore; (3) cosa sostituisce
`PRAGMA cipher_version` nel guard di SEC-1; (4) FTS5 disponibile (Onda 5.4); (5) licenza compatibile col MIT del repo. **Nessun
cambio di provider nel codice**: la decisione (D9) è dell'autore.

## 0.10 — Documenti (RV-12, RV-13)

CLAUDE.md sotto i 15 KB (la cronaca di §7 in `docs/MILESTONES.md`; §10 riscritto: autorità = codice + migration +
`ARCHITECTURE.md`); un indice di una pagina in testa al tracker (ID · stato · onda che lo chiude, con B1/B11/B3 sistemati); il
`progress.md` dei ledger SDD committato in `docs/` (i diff di review restano ignorati).
