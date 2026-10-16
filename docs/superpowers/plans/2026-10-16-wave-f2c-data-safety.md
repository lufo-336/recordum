# Onda F2c — Dati al sicuro — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dopo F2c, su Linux (la casa dei dati, D11) Recordum si apre senza passphrase dopo la prima volta, il backup
automatico gira davvero e dice quando si spegne, "Backup ora", "Ripristina…" e lo storico sono raggiungibili da una
finestra Impostazioni con la sola pagina "Dati e backup", un `.recbak` di Windows si ripristina su Linux e viceversa, e il
database nasce 0600.

**Architecture:** Una sola astrazione nuova in Core, `ISecretStore`, con l'implementazione `libsecret` (P/Invoke) in Data
accanto a quelle DPAPI; `DbUnlockService` impara lo sblocco dalla chiave ricordata e la scrittura dopo uno sblocco a
passphrase; l'auto-backup su Linux legge la sua passphrase dallo stesso portachiavi tramite un adattatore
`IAutoBackupKeyStore`. La shell Avalonia avvia `BackupSchedulerHost`, porta le finestre Backup e Ripristina dietro
`IBackupDialogLauncher` e apre una finestra Impostazioni ridotta. Windows non cambia nello sblocco.

**Tech Stack:** C# / .NET 10; `libsecret-1.so.0` via `LibraryImport`; EF Core 10 + SQLCipher; Avalonia 12.1.2 +
`Avalonia.Headless.XUnit` (xunit.v3); `CommunityToolkit.Mvvm` 8.4; GitHub Actions (ubuntu-latest, windows-latest).

**Spec:** [`docs/superpowers/specs/2026-10-16-wave-f2c-data-safety-design.md`](../specs/2026-10-16-wave-f2c-data-safety-design.md)
(perimetro B dell'autore, decisioni 1-7, obblighi di test §5). Contesto: review `2026-09-25-avalonia-v1-plan-review.md` RV-4,
RV-5, §6 Passo 3. Forma del piano (D5): decisioni, interfacce, obblighi di test, trappole; il comportamento WPF indicato è il
riferimento, non il markup.

## Global Constraints

- **Branch**: `f2c-data-safety`, da `f2b-creation` (pila non mergiata: `avalonia-linux-slice` → `f2-perf1-home` →
  `f2a-registration` → `f2b-creation`). Mai commit su `main`; cambiare branch in una chiamata separata. Aggiornato un
  `.md` → commit e push subito.
- **Ambiente Linux**: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"`. Build:
  `dotnet build Recordum.slnx -p:EnableWindowsTargeting=true --nologo`. **Mai `-p:UseAppHost=false`**.
- **Baseline** a `ab0f374`: Data **547** (`--filter "Category!=WindowsOnly"`), Presentation **983**, UI **347**
  (`--filter "Category!=Performance"`); Core, Plugins, ExportEngine si misurano all'inizio del Task 1 e si scrivono nel
  ledger. Si controlla sempre il totale eseguito.
- **La suite UI si prova in `en_US` e in `it_IT`** prima di ogni push; la CI si guarda a ogni push.
- **Sicurezza** (CLAUDE.md §6): la chiave del DB solo da `IDbKeyProvider`; passphrase sbagliata riconosciuta con una
  **lettura** (`SqliteException` 26), `ClearAllPools()` fra i tentativi; nessun file in chiaro fuori da `TempDirectory`
  (`SystemTempGuardTests`); `SqlCipherProviderGuardTests` verde. **Mai** registrare nel log una chiave, una passphrase o il
  contenuto di un segreto. I buffer con la chiave si azzerano (`CryptographicOperations.ZeroMemory`) dopo l'uso.
- **WPF è congelato**: si tocca solo dove un contratto condiviso lo obbliga (decisione 6), con il cambiamento minimo.
- **Presentation non nomina tipi UI**; un'astrazione nuova vuole le sue implementazioni e un test della registrazione DI
  per ciascun sistema.
- **Il parametro muto** (CLAUDE.md §7): i parametri nuovi su servizi e ViewModel condivisi sono **obbligatori**; un test
  prova il caso in cui il default risponderebbe diverso.
- **Avalonia**: `InitializeComponent()`; `x:DataType` ovunque; `[AvaloniaFact]`; `using Xunit;`; DB privato
  (`tests/Recordum.UI.Tests/Fixtures/PrivateDbHost.cs`); finestre chiuse in `finally`; orologi finti.
- **Stringhe**: `Strings.resx` + `Strings.it.resx` + `Strings.Designer.cs` **solo via shell**; cercare le chiavi esistenti
  prima (`Backup.*`, `Unlock.*`, `Settings.*`).
- **Linguaggio visivo** (CLAUDE.md §11): icone Lucide, mai glifi; pulsanti di sola icona con `AutomationProperties.Name` e
  `ToolTip.Tip`; chiaro e scuro curati uguali.
- **`TreatWarningsAsErrors`** globale.
- **Un push che tocca `.github/workflows/` vuole lo scope `workflow`** nel token di `gh`.

## Decisioni prese scrivendo il piano

L'esecutore non le ri-discute; sono qui perché l'autore possa disfarle **prima** dell'esecuzione.

1. **Schema `org.recordum.Secret`**, attributi `kind` (`db-key` | `auto-backup`) e `data_dir` (percorso **normalizzato**:
   `Path.GetFullPath` + `Path.TrimEndingDirectorySeparator`). Etichetta visibile nel portachiavi: "Recordum — chiave del
   database (<data_dir>)" / "Recordum — passphrase del backup automatico (<data_dir>)", così l'utente la riconosce in
   Seahorse. I valori sono stringhe: la chiave del DB in base64, la passphrase dell'auto-backup così com'è.
2. **Le chiamate al portachiavi non girano mai sul thread dell'interfaccia**: `libsecret` sincrono può aspettare un prompt di
   sblocco della collezione. La shell le esegue con `Task.Run`; con un tetto di **10 s** sulla lettura all'avvio, oltre il
   quale si considera il portachiavi non disponibile e si mostra la finestra della passphrase.
3. **La chiave ricordata entra come `StoredKeyProvider`** (Data): un `IDbKeyProvider` con `ProviderId =
   KeyProviderId.Passphrase` (è la stessa chiave che la passphrase deriva) che restituisce copie della chiave e la azzera al
   `Dispose`. Si avvolge in `CachingKeyProvider` come gli altri.
4. **"Ricorda" si scrive dopo**: solo dopo che `CanRead` ha confermato la chiave (inserimento) o dopo la creazione del
   database e la prima lettura (creazione). Un errore del portachiavi in scrittura non blocca l'avvio: log + stato "non
   ricordata" nella pagina "Dati e backup".
5. **Su Windows `ISecretStore` è `NullSecretStore`**: lo sblocco resta DPAPI; l'auto-backup usa `DpapiAutoBackupKeyStore`
   (`RecordumOptions.AutoBackupKeyPath`, come WPF). La riga "chiave ricordata / Dimentica" della pagina non si mostra su
   Windows (la decide la composizione, non il ViewModel).
6. **I dialog di file diventano asincroni**: `IBackupSaveFileDialog.PromptAsync` e `IOpenFileDialog.PromptAsync` /
   `PromptMultipleAsync` (`Task<string?>`, `Task<string[]>`), perché `StorageProvider` di Avalonia è asincrono e bloccare il
   thread dell'interfaccia lo stallerebbe. I chiamanti in Presentation li dà il compilatore; le implementazioni WPF
   restituiscono `Task.FromResult` del dialog sincrono di oggi (cambiamento minimo, imposto dal contratto). `ISaveFileDialog`
   (export) resta com'è: lo porta F6.
7. **La pagina "Dati e backup" ha un suo ViewModel**, `DataAndBackupPageViewModel` (Presentation), che compone
   `BackupSettingsViewModel`, `BackupHistoryViewModel`, i comandi "Backup ora" / "Ripristina…" (via `IBackupDialogLauncher`)
   e lo stato della chiave del dispositivo. `SettingsWindowViewModel` (WPF) non cambia: F6 deciderà come unire le due
   finestre quando WPF si spegne.
8. **Il pulsante Impostazioni del rail si riaccende** (parte di F1-RAIL1): apre la finestra ridotta su "Dati e backup".
9. **I permessi 0600 del database** (F1-SEC1): il file si crea vuoto con `UnixCreateMode = UserRead | UserWrite` prima della
   prima apertura (SQLite crea `-wal` e `-shm` copiando i permessi del database); all'avvio, prima dell'apertura, i tre file
   esistenti più aperti di 0600 si riportano a 0600. Va **misurato** su `-wal` e `-shm`, non dedotto.

## Struttura dei file

| File | Responsabilità | Task |
|---|---|---|
| `src/Recordum.Data/Security/SqlCipherConnectionFactory.cs` (o chi crea il file), `DbUnlockService.cs` | file del DB 0600 | 1 |
| `src/Recordum.Core/Abstractions/Security/ISecretStore.cs`, `SecretKind.cs`, `SecretStoreUnavailableException.cs` | l'astrazione | 2 |
| `src/Recordum.Data/Security/LibsecretSecretStore.cs`, `NullSecretStore.cs`, `SecretStoreAutoBackupKeyStore.cs` | portachiavi e adattatore | 2 |
| `.github/workflows/ci.yml` | Secret Service nel job Linux; generatore del fixture Windows | 2, 5 |
| `src/Recordum.Data/Security/DbUnlockService.cs`, `StoredKeyProvider.cs`; `IDbUnlockService.cs` | sblocco dalla chiave ricordata | 3 |
| `src/Recordum.Presentation/ViewModels/Startup/UnlockViewModel.cs`, `src/Recordum.UI/Views/Startup/UnlockWindow*`, `App.axaml.cs`, `Bootstrap/AppComposition.cs` | casella "Ricorda", avvio senza finestra | 3 |
| `src/Recordum.UI/Bootstrap/AvaloniaShellServices.cs`, `src/Recordum.Presentation/Bootstrap/CoreInitializer.cs` | key store per sistema, scheduler avviato | 4 |
| `tests/Fixtures/recbak/` (o `tests/Recordum.Data.Tests/Fixtures/`) | i due `.recbak` di riferimento | 5 |
| `src/Recordum.Presentation/Services/IOpenFileDialog.cs`, `IBackupSaveFileDialog.cs`, chiamanti; `src/Recordum.WPF/Services/Wpf*FileDialog.cs`; `src/Recordum.UI/Services/Avalonia*FileDialog.cs`, `AvaloniaBackupDialogLauncher.cs`, `Views/Backup/*` | dialog asincroni, finestre Backup e Ripristina | 6 |
| `src/Recordum.Presentation/ViewModels/Backup/DataAndBackupPageViewModel.cs`, `src/Recordum.UI/Views/Settings/*`, `Services/AvaloniaSettingsWindowLauncher.cs`, `WorkspaceRibbon.axaml` | la finestra Impostazioni ridotta | 7 |

## Review Focus

1. **Il portachiavi chiede la sua password all'avvio** (sessione con login automatico, collezione bloccata): ci si aspetta il
   prompt del sistema e, se annullato o lasciato lì, la finestra della passphrase entro 10 s — mai un'app che si blocca o
   una finestra che non risponde. → Task 3, test 3.6.
2. **Una `--data-dir` di prova accanto ai dati veri**: la chiave dell'una non apre, non sovrascrive e non cancella quella
   dell'altra; un percorso scritto con o senza `/` finale è la stessa installazione. → Task 2, test 2.3 e 2.4.
3. **Restore di un backup dell'altra macchina con la chiave ricordata**: dopo il restore l'avvio successivo apre ancora senza
   passphrase. → Task 3, test 3.5.
4. **Il portachiavi svuotato (o la passphrase del backup cancellata in Seahorse) dopo aver acceso il backup automatico**: lo
   scheduler si spegne e la pagina lo dice, non un backup che l'utente crede attivo. → Task 4 test 4.3, Task 7 test 7.3.
5. **Un database creato da una versione precedente, 0644**: al primo avvio con F2c i tre file tornano 0600. → Task 1,
   test 1.2.

---

### Task 1: Il database nasce privato (F1-SEC1)

**Files:**
- Modify: il punto in cui il file del database nasce (cercarlo: `SqlCipherConnectionFactory.Open` con `ReadWriteCreate`,
  `DbUnlockService.CreateAsync`, `CoreInitializer` / migrazioni), `DbUnlockService` (o il passo d'avvio che precede
  l'apertura) per il riallineamento
- Modify: `.github/workflows/ci.yml` (filtro del job Windows)
- Test: `tests/Recordum.Data.Tests/Security/DatabaseFilePermissionsTests.cs`

**Interfaces:**
- Produces: nessuna API nuova; invariante "su Unix `recordum.db`, `-wal`, `-shm` sono 0600".

**Decisioni e trappole:** decisione 9. Su Windows non si fa niente (`OperatingSystem.IsWindows()`). Mai `chmod` *dopo* la
creazione come unica difesa (finestra con umask 022): il file nasce 0600. Il riallineamento non cambia owner né contenuto.
Aggiornare F1-SEC1 nel tracker nel Task 8.

- [ ] **Step 0** — misurare e scrivere nel ledger le baseline mancanti (Core, Plugins, ExportEngine).
- [ ] **Step 1 — test, rossi** (`[Trait("Category","UnixOnly")]`; il job Windows della CI aggiunge `Category!=UnixOnly` al
  suo filtro, come il job Linux esclude `WindowsOnly` — nessuna guardia che ritorna presto e sembra verde): 1.1 un database creato da zero
  con umask 022 e almeno una scrittura in WAL: `recordum.db`, `-wal`, `-shm` sono 0600 (misurati con
  `File.GetUnixFileMode`); 1.2 (Review Focus 5) tre file preesistenti 0644 → dopo il passo d'avvio sono 0600 e il database si
  legge ancora.
- [ ] **Step 2** — implementazione, verdi; mutazione: togliere `UnixCreateMode` → 1.1 rosso; togliere il riallineamento →
  1.2 rosso.
- [ ] **Step 3** — commit `fix(data): il database e i suoi file nascono 0600 su Unix (F1-SEC1)`.

### Task 2: Il portachiavi

**Files:**
- Create: `src/Recordum.Core/Abstractions/Security/ISecretStore.cs`, `SecretKind.cs`, `SecretStoreUnavailableException.cs`;
  `src/Recordum.Data/Security/LibsecretSecretStore.cs`, `NullSecretStore.cs`, `SecretStoreAutoBackupKeyStore.cs`
- Modify: `.github/workflows/ci.yml` (job Linux)
- Test: `tests/Recordum.Data.Tests/Security/LibsecretSecretStoreTests.cs`, `SecretStoreAutoBackupKeyStoreTests.cs`

**Interfaces:**
- Produces:
  - `enum SecretKind { DbKey, AutoBackup }`
  - `interface ISecretStore { bool IsAvailable { get; } string? TryRead(SecretKind kind); void Write(SecretKind kind, string
    value); void Delete(SecretKind kind); }` — ogni guasto del portachiavi esce come `SecretStoreUnavailableException`, mai
    come eccezione nativa o `DllNotFoundException`. `TryRead` torna null quando il segreto non c'è.
  - `LibsecretSecretStore(string dataDirectory)` — `[SupportedOSPlatform("linux")]`; `IsAvailable` prova una volta il
    caricamento di `libsecret-1.so.0` e la raggiungibilità del servizio (risultato memorizzato).
  - `NullSecretStore` — `IsAvailable = false`, `TryRead` null, `Delete` no-op, `Write` lancia `NotSupportedException` (mai
    fingere di aver salvato, come `NullAutoBackupKeyStore`).
  - `SecretStoreAutoBackupKeyStore(ISecretStore store) : IAutoBackupKeyStore` su `SecretKind.AutoBackup`; `ReadKey`
    torna null anche se lo store lancia (lo scheduler si spegne, come già fa).

**Decisioni e trappole:** decisioni 1 e 2. `LibraryImport` con `StringMarshalling.Utf8`; schema e attributi come strutture
native (`SecretSchema` con `SECRET_SCHEMA_NONE` e attributi `STRING`) — copiare la forma da un'implementazione nota (Git
Credential Manager, `src/shared/Core/Interop/Linux/Native/Libsecret.cs`, MIT) leggendola, senza dipendenza. Il `GError**`
si libera (`g_error_free`), la stringa restituita da `secret_password_lookup_sync` si libera con `secret_password_free`
(azzera la memoria). Nessun log del valore.

CI (job Linux, prima di "Test"): `sudo apt-get install -y gnome-keyring libsecret-1-0 dbus-x11`; i test girano dentro
`dbus-run-session -- bash -c 'echo -n ci | gnome-keyring-daemon --unlock --components=secrets && <i dotnet test di oggi>'`.
Il Windows non cambia.

- [ ] **Step 1 — test, rossi** (`[Trait("Category","Keyring")]`; il job Windows li esclude dal filtro, come `UnixOnly`): 2.1 scrivi →
  leggi → stesso valore; cancella → `TryRead` null; 2.2 i due `SecretKind` non si vedono a vicenda; 2.3 (Review Focus 2) due
  `data_dir` diversi non si leggono né si cancellano a vicenda; 2.4 `/tmp/a` e `/tmp/a/` sono la stessa installazione; 2.5
  con la libreria non caricabile (costruttore interno che prende il nome della libreria, es. `libsecret-assente.so`)
  `IsAvailable` è false e `TryRead`/`Write` lanciano `SecretStoreUnavailableException`; 2.6 l'adattatore: `WriteKey` →
  `ReadKey` uguale, `HasKey` vero; con uno store che lancia, `ReadKey` torna null. I test puliscono i segreti che creano
  (`finally`).
- [ ] **Step 2** — implementazione, verdi in locale (GNOME Keyring); mutazione su 2.3 (attributo `data_dir` tolto → rosso).
- [ ] **Step 3** — CI: il passo Secret Service nel job Linux; il controllore pusha e verifica che i test `Keyring` risultino
  **eseguiti** nel trx (non saltati).
- [ ] **Step 4** — commit `feat(data): portachiavi di sistema via libsecret (F2c)` e `ci: Secret Service nel job Linux (F2c)`.

### Task 3: Sblocco dalla chiave ricordata

**Files:**
- Create: `src/Recordum.Data/Security/StoredKeyProvider.cs`
- Modify: `src/Recordum.Core/Abstractions/Security/IDbUnlockService.cs`, `src/Recordum.Data/Security/DbUnlockService.cs`,
  `src/Recordum.Presentation/ViewModels/Startup/UnlockViewModel.cs`, `src/Recordum.UI/Views/Startup/UnlockWindow*`,
  `src/Recordum.UI/App.axaml.cs`, `src/Recordum.UI/Bootstrap/AppComposition.cs`
- Test: `tests/Recordum.Data.Tests/Security/DbUnlockServiceStoredKeyTests.cs`,
  `tests/Recordum.Presentation.Tests/.../UnlockViewModelTests.cs`, `tests/Recordum.UI.Tests/Startup/UnlockWindowTests.cs`

**Interfaces:**
- Consumes: `ISecretStore`, `SecretKind` (Task 2).
- Produces:
  - `DbUnlockService(IDbStoragePaths paths, Func<IDbKeyProvider>? platformKeyFactory, ISecretStore secrets, ILogger<DbUnlockService>? logger)`
    — `secrets` **obbligatorio** (i costruttori di oggi lo ricevono; i chiamanti li dà il compilatore).
  - `enum StoredKeyOutcome { Unlocked, NoStoredKey, StoredKeyRejected, StoreUnavailable }` e
    `record StoredKeyResult(StoredKeyOutcome Outcome, IDbKeyProvider? Provider)`.
  - `IDbUnlockService.TryUnlockWithStoredKeyAsync(CancellationToken ct)` — solo in `NeedsPassphrase`; legge `DbKey`, la
    prova con `CanRead`; `StoredKeyRejected` cancella il segreto.
  - `IDbUnlockService.UnlockAsync(DbUnlockState state, string passphrase, bool rememberOnDevice, CancellationToken ct)` —
    il parametro nuovo è obbligatorio; con `true` e sblocco riuscito scrive la chiave derivata (base64); un
    `SecretStoreUnavailableException` qui si registra e non cambia l'esito.
  - `IDbUnlockService.CanRememberOnDevice` (`= secrets.IsAvailable`).
  - `UnlockViewModel`: `RememberOnDevice` (default `true` se `CanRememberOnDevice`, altrimenti `false` e non modificabile),
    `CanRemember`, `RememberUnavailableHint`, `StoredKeyRejected` (mostra la riga sopra il campo).
  - `App.ResolveKeyAsync`: in `NeedsPassphrase` chiama `TryUnlockWithStoredKeyAsync` con `Task.Run` e tetto di 10 s
    (decisione 2) prima di costruire la finestra; `Unlocked` → nessuna finestra.

**Decisioni e trappole:** decisioni 2, 3, 4. Stringhe nuove (en / it): `Unlock.RememberOnDevice` "Remember on this device" /
"Ricorda su questo dispositivo"; `Unlock.RememberUnavailable` "The system keyring isn't available, so Recordum will ask for
the passphrase at every start." / "Il portachiavi di sistema non è disponibile: Recordum chiederà la passphrase a ogni
avvio."; `Unlock.StoredKeyRejected` "The saved key no longer opens your data. Enter your passphrase." / "La chiave salvata
non apre più i dati. Inserisci la passphrase.". La chiave letta dal portachiavi si decodifica in un `byte[]` che si azzera
dopo la costruzione del provider; una stringa base64 malformata o di lunghezza sbagliata è `StoredKeyRejected`.

- [ ] **Step 1 — test, rossi** (store finto in memoria, DB privato vero): 3.1 chiave giusta → `Unlocked`, nessuna
  derivazione Argon2id (il salt non viene letto: misurarlo con un salt reso illeggibile dopo la creazione); 3.2 chiave
  sbagliata o malformata → `StoredKeyRejected` e il segreto non c'è più; 3.3 store che lancia → `StoreUnavailable`; 3.4
  inserimento con `rememberOnDevice: true` → segreto scritto e lo si usa al giro dopo; **con `false` → niente scritto**
  (parametro muto); creazione con `true` → scritto dopo la creazione; 3.5 (Review Focus 3) restore di un `.recbak` con la
  chiave ricordata → `TryUnlockWithStoredKeyAsync` dà ancora `Unlocked`; 3.6 (Review Focus 1) uno store la cui lettura non
  torna (bloccata) → `ResolveKeyAsync` mostra la finestra entro il tetto (orologio/attesa iniettabile, nessuna attesa vera di
  10 s nel test); 3.7 UI: la casella c'è, è spuntata, ha un nome accessibile; con store non disponibile è spenta e la
  spiegazione è visibile; `StoredKeyRejected` mostra la sua riga.
- [ ] **Step 2** — implementazione, verdi; mutazioni su 3.2 (non cancellare) e 3.4 (scrivere sempre).
- [ ] **Step 3** — commit `feat: sblocco dalla chiave ricordata nel portachiavi (F2c)`.

### Task 4: Il backup automatico acceso su Avalonia

**Files:**
- Modify: `src/Recordum.UI/Bootstrap/AvaloniaShellServices.cs` (o `AppComposition.cs`),
  `src/Recordum.Presentation/Bootstrap/CoreInitializer.cs`; rimuovere `src/Recordum.UI/Services/Null/NullAutoBackupKeyStore.cs`
  se non resta usato
- Test: `tests/Recordum.UI.Tests/Bootstrap/BackupCompositionTests.cs`, `tests/Recordum.Presentation.Tests/.../BackupSchedulerHostStartTests.cs`

**Interfaces:**
- Consumes: `LibsecretSecretStore`, `NullSecretStore`, `SecretStoreAutoBackupKeyStore` (Task 2); `DpapiAutoBackupKeyStore`.
- Produces: registrazioni — Linux: `ISecretStore` → `LibsecretSecretStore(options.DataDirectory)`, `IAutoBackupKeyStore` →
  `SecretStoreAutoBackupKeyStore`; Windows: `ISecretStore` → `NullSecretStore`, `IAutoBackupKeyStore` →
  `DpapiAutoBackupKeyStore(options.AutoBackupKeyPath)`. `BackupSchedulerHost` avviato dalla shell Avalonia dopo
  `CoreInitializer` (stessa attesa di un tick di oggi, nessun backup immediato all'avvio) e fermato all'uscita.

**Decisioni e trappole:** decisione 5. Lo stesso `ISecretStore` serve lo sblocco (Task 3, prima del contenitore) e il
contenitore: si costruisce una volta in `App` e si passa a `AppComposition.BuildServices`. Il commento di `CoreInitializer`
che vieta l'avvio va riscritto, non lasciato a mentire. Lo scheduler deve leggere la passphrase **fuori** dal thread
dell'interfaccia (decisione 2: lo fa già se gira nel suo host; verificarlo).

- [ ] **Step 1 — test, rossi**: 4.1 registrazione DI per sistema (sul sistema dove gira il test, e l'altro ramo provato
  costruendo la composizione con il sistema iniettato); 4.2 con la passphrase nello store finto e `backup.auto.enabled =
  true` scaduto, un giro dello scheduler produce un `.recbak` nella cartella dei backup e una riga di storico; 4.3 (Review
  Focus 4) senza passphrase → `backup.auto.enabled` diventa `false` e `IsEnabled` è falso; 4.4 lo host si avvia sulla shell
  Avalonia (non solo in WPF).
- [ ] **Step 2** — implementazione, verdi; mutazione: registrare di nuovo il Null → 4.1 e 4.2 rossi.
- [ ] **Step 3** — commit `feat(ui): backup automatico su Avalonia, con la passphrase dal portachiavi (F2c)`.

### Task 5: Il restore incrociato

**Files:**
- Create: `tests/Recordum.Data.Tests/Backup/CrossPlatformRestoreTests.cs`, `RecbakFixtureGenerator.cs`; i fixture in
  `tests/Recordum.Data.Tests/Fixtures/recbak/linux.recbak` e `windows.recbak` (+ un `README.md` di due righe: come si
  rigenerano, quale passphrase usano)
- Modify: `tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj` (copia dei fixture in output), `.github/workflows/ci.yml` (job
  Windows: passo del generatore e artefatto)

**Interfaces:**
- Consumes: `IBackupService`, `IRestoreService`, `DemoDataSeeder` (o un seed minimo proprio, deterministico), `FakeClock`.
- Produces: due file di riferimento con contenuto noto; il generatore, attivo solo con `RECORDUM_GENERATE_RECBAK=<cartella>`,
  scrive `<os>.recbak` e altrimenti **non** gira (non è un test verde vuoto: è escluso con un `Trait("Category",
  "FixtureGenerator")` filtrato in CI salvo nel passo dedicato).

**Decisioni e trappole:** il contenuto si sceglie piccolo e deterministico (orologio fisso; 3-4 abitudini di tipi diversi,
qualche completamento, una nota, un'astinenza) e le asserzioni confrontano valori, non solo conteggi. Su Windows il database
sorgente usa la chiave di piattaforma (DPAPI), come un'installazione vera; su Linux la passphrase. La passphrase del
`.recbak` di riferimento è fissa e scritta nel README (sono dati finti). Il fixture Linux lo genera l'implementatore; quello
Windows lo produce la CI: **il controllore** pusha, scarica l'artefatto `recbak-fixture-windows` con `gh run download`, lo
committa. Finché manca, il test del fixture Windows **fallisce** con un messaggio che dice come generarlo (mai uno skip).

- [ ] **Step 1 — test, rossi**: 5.1 ripristinare `linux.recbak` su una cartella privata con la chiave locale → i valori
  attesi ci sono; 5.2 lo stesso con `windows.recbak`; 5.3 il database ripristinato è cifrato con la chiave locale (si legge
  con quella, non si legge con un'altra).
- [ ] **Step 2** — generatore, fixture Linux, passo CI Windows; il controllore produce e committa il fixture Windows; verdi su
  entrambi i job.
- [ ] **Step 3** — commit `test(data): restore incrociato Windows ↔ Linux con fixture di riferimento (F2c)`.

### Task 6: Dialog di file asincroni e finestre Backup e Ripristina

**Files:**
- Modify: `src/Recordum.Presentation/Services/IOpenFileDialog.cs`, `IBackupSaveFileDialog.cs` e i chiamanti (compilatore);
  `src/Recordum.WPF/Services/WpfOpenFileDialog.cs`, `WpfBackupSaveFileDialog.cs`
- Create: `src/Recordum.UI/Services/AvaloniaOpenFileDialog.cs`, `AvaloniaBackupSaveFileDialog.cs`,
  `AvaloniaBackupDialogLauncher.cs`; `src/Recordum.UI/Views/Backup/BackupDialog.axaml(.cs)`, `RestoreDialog.axaml(.cs)`
- Test: `tests/Recordum.UI.Tests/Backup/BackupDialogTests.cs`, `RestoreDialogTests.cs`; i test esistenti dei chiamanti
  aggiornati

**Riferimento WPF:** `src/Recordum.WPF/Views/Backup/BackupDialog.xaml`, `RestoreDialog.xaml`, `WpfBackupDialogLauncher`.

**Interfaces:**
- Consumes: `BackupDialogViewModel`, `RestoreDialogViewModel`.
- Produces: `IBackupSaveFileDialog.PromptAsync(string defaultFileName, string filter)`,
  `IOpenFileDialog.PromptAsync(string filter)`, `PromptMultipleAsync(string filter)` (decisione 6);
  `AvaloniaBackupDialogLauncher : IBackupDialogLauncher` — finestre modali sulla principale; i dialog di file su
  `TopLevel.StorageProvider`, filtro `.recbak`.

**Decisioni e trappole:** decisione 6. Le passphrase stanno in `TextBox` con `PasswordChar` e `AutomationProperties.Name`;
mai in un binding che le scriva nel log. `AsyncRelayCommand.ExecuteAsync` non consulta `CanExecute`: doppio clic su
"Crea"/"Ripristina" non lancia due operazioni (guardia nel ViewModel se manca). Dopo un restore riuscito vale il
comportamento WPF (leggerlo: riavvio richiesto o ricarica) — portarlo uguale. I percorsi restituiti da `StorageProvider` sono
`IStorageFile`: si usa `TryGetLocalPath()`; un file non locale è un errore mostrato, non un'eccezione.

- [ ] **Step 1 — test, rossi**: 6.1 la finestra di backup con un dialog di salvataggio finto crea un `.recbak` sul DB privato
  e lo storico ha la riga; 6.2 la finestra di ripristino: anteprima con la passphrase giusta, errore leggibile con quella
  sbagliata; 6.3 doppio "Crea" → un solo file; 6.4 i chiamanti in Presentation (note, diario, export se tocca) passano dalla
  forma asincrona: i loro test esistenti restano verdi; 6.5 registrazione DI: launcher e dialog veri, non i Null; 6.6 i campi
  passphrase hanno un nome accessibile.
- [ ] **Step 2** — implementazione, verdi; build intera (WPF compreso) verde; mutazione su 6.3.
- [ ] **Step 3** — commit `feat(ui): finestre Backup e Ripristina su Avalonia, dialog di file asincroni (F2c)`.

### Task 7: La finestra Impostazioni ridotta — "Dati e backup"

**Files:**
- Create: `src/Recordum.Presentation/ViewModels/Backup/DataAndBackupPageViewModel.cs`;
  `src/Recordum.UI/Views/Settings/SettingsWindow.axaml(.cs)`, `DataAndBackupPage.axaml(.cs)`;
  `src/Recordum.UI/Services/AvaloniaSettingsWindowLauncher.cs`
- Modify: `src/Recordum.UI/Views/WorkspaceRibbon.axaml` (pulsante Impostazioni visibile), `AvaloniaShellServices.cs`
- Test: `tests/Recordum.Presentation.Tests/.../DataAndBackupPageViewModelTests.cs`,
  `tests/Recordum.UI.Tests/Settings/DataAndBackupPageTests.cs`

**Riferimento WPF:** `SettingsWindow.xaml` (template di `BackupSettingsViewModel`, righe ~92-130), `BackupHistoryView.xaml`.

**Interfaces:**
- Consumes: `BackupSettingsViewModel`, `BackupHistoryViewModel`, `IBackupDialogLauncher` (Task 6), `ISecretStore` (Task 2).
- Produces: `DataAndBackupPageViewModel(BackupSettingsViewModel settings, BackupHistoryViewModel history,
  IBackupDialogLauncher launcher, ISecretStore secrets, bool showDeviceKey)` — tutti obbligatori; `BackupNowCommand`,
  `RestoreCommand`, `ForgetDeviceKeyCommand`, `IsDeviceKeyRemembered`, `ShowDeviceKey`, `AutoBackupOffWarning` (visibile
  quando l'auto-backup è acceso nelle impostazioni ma non ha una passphrase leggibile, o lo scheduler l'ha spento);
  `AvaloniaSettingsWindowLauncher : ISettingsWindowLauncher` (ignora la categoria: c'è solo questa pagina).

**Decisioni e trappole:** decisioni 5, 7, 8. Stringhe nuove: `Settings.DataAndBackup` "Data & backup" / "Dati e backup";
`Settings.DeviceKey.Remembered` "The database key is remembered on this device." / "La chiave del database è ricordata su
questo dispositivo."; `Settings.DeviceKey.NotRemembered` "The database key isn't remembered: Recordum asks for the
passphrase at every start." / "La chiave del database non è ricordata: Recordum chiede la passphrase a ogni avvio.";
`Settings.DeviceKey.Forget` "Forget on this device" / "Dimentica su questo dispositivo". Per l'avviso dell'auto-backup
spento riusare `Backup.Settings.InertWarning` se il testo regge, altrimenti una chiave nuova. "Dimentica" chiede conferma con
`IConfirmationDialog`. Le letture del portachiavi per lo stato vanno fuori dal thread dell'interfaccia (decisione 2). La
passphrase del backup ha la sua conferma e il minimo di 12 caratteri come in WPF.

- [ ] **Step 1 — test, rossi**: 7.1 la pagina mostra le quattro parti (backup automatico, Backup ora / Ripristina, storico,
  chiave del dispositivo); su Windows (`showDeviceKey: false`) la quarta non c'è — e con `true` c'è (parametro muto); 7.2
  "Dimentica" confermato cancella `DbKey` e lo stato passa a "non ricordata"; annullato non cancella; 7.3 (Review Focus 4)
  auto-backup acceso senza passphrase leggibile → l'avviso è visibile; con la passphrase salvata non lo è; 7.4 il pulsante
  Impostazioni del rail è visibile, ha nome e tooltip, e apre la finestra; 7.5 campi passphrase con nome accessibile; 7.6
  schermate chiaro e scuro con un'asserzione strutturale ciascuna (nessun controllo esce dalla finestra a 640 px di
  larghezza).
- [ ] **Step 2** — implementazione, verdi; mutazione su 7.2 (cancellare senza conferma) e 7.3.
- [ ] **Step 3** — commit `feat(ui): finestra Impostazioni con la pagina Dati e backup (F2c)`.

### Task 8: Sul DB vero, misure, chiusura

**Files:**
- Test: `tests/Recordum.UI.Tests/Startup/KeyringStartupIntegrationTests.cs`
- Modify: `docs/phase-4-issues.md` (sezione "Onda F2c"; F1-SEC1, F1-RAIL1 per il pulsante Impostazioni, RV-5), `docs/ROADMAP.md`,
  `docs/MILESTONES.md`, `docs/dev-environment-sync.md` (il portachiavi di prova e `--data-dir`)

- [ ] **Step 1 — test**: 8.1 dall'avvio della shell su una cartella privata con un `LibsecretSecretStore` vero
  (`Category=Keyring`): prima volta con "ricorda" → secondo avvio senza finestra; "Dimentica" → terzo avvio con la finestra;
  8.2 dalla finestra principale: Impostazioni → "Backup ora" → il file c'è → "Ripristina…" dello stesso file → i dati ci
  sono e il quarto avvio apre ancora senza passphrase.
- [ ] **Step 2** — review dell'intero branch (controllore) e CI verde su entrambi i job.
- [ ] **Step 3** — documenti: tracker (F2c, stati), ROADMAP "Dove siamo" (una pagina), MILESTONES; nella ROADMAP la
  checklist a schermo per l'autore su GNOME: primo avvio con "Ricorda", riavvio senza passphrase, Seahorse mostra le due voci
  con l'etichetta giusta, "Dimentica", backup automatico acceso con la sua passphrase (e spento se la si cancella in
  Seahorse), "Backup ora" e "Ripristina…", un `.recbak` della VM Windows ripristinato su Linux. Commit e push.
