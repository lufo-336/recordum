# Onda D9 — SQLite3 Multiple Ciphers al posto del bundle SQLCipher deprecato

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** il database cifrato di Recordum passa a un motore mantenuto (SQLite3MC, SQLite recente) **senza cambiare un
byte del formato**: ogni database e ogni `.recbak` di oggi si apre dopo l'aggiornamento come prima.

**Architecture:** si scambia il pacchetto nativo e si dichiara il formato SQLCipher 4 in ogni istruzione che imposta o
cambia una chiave, da un solo punto. `sqlcipher_export` (che MC non ha) si sostituisce con `VACUUM INTO` + `rekey` nel
backup e con copia + `rekey` nel restore. Il guard di SEC-1 cambia domanda. Prima di tutto si congela, con il bundle di
oggi, un database vero come fixture: è la prova che nessun utente perde i dati.

**Tech Stack:** .NET 10, EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite.Core`), `Microsoft.Data.Sqlite`,
`SQLite3MC.PCLRaw.bundle`, xunit.

**Spec:** `docs/design/2026-09-30-encryption-provider-probe.md` (sonda misurata; D9 = opzione A, decisa dall'autore il
2026-10-27). Le misure, le istruzioni esatte e le trappole sono lì: il piano le cita, non le ripete.

## Global Constraints

- Pacchetto: `SQLite3MC.PCLRaw.bundle` **2.6.1** (pubblicato 2026-10-12, non deprecato; la sonda ha misurato la 2.4.0),
  versione esatta in `Directory.Packages.props`; esce `SQLitePCLRaw.bundle_e_sqlcipher`. Un solo bundle SQLitePCLRaw nel
  grafo; `.Sqlite.Core`, mai `.Sqlite` (trappola 10).
- Formato **invariato**: SQLCipher 4. Ogni istruzione che imposta una chiave dichiara prima
  `PRAGMA cipher = 'sqlcipher'; PRAGMA legacy = 4;` (sonda §1). Nessuna configurazione via funzioni SQL
  (`sqlite3mc_config`), nessuna `SELECT` prima della chiave (trappola 4). Il cambio a un cifrario moderno **non** è in
  quest'onda (trappola 6).
- La chiave resta grezza (`x'…'`, 32 byte da `IDbKeyProvider`); `KeyTransitionService` e `DbUnlockService` passano dalla
  factory e non cambiano logica.
- Il file in chiaro del backup e del restore resta solo in `TempDirectory` e si distrugge come oggi (`SecureWipe`,
  `ShredDirectory`; `SystemTempGuardTests`).
- Ogni test nuovo è **provato rosso** (CLAUDE.md §7); i rossi della sonda (13 test con il solo pacchetto scambiato) sono
  attesi e vanno visti.
- Build sempre `--no-incremental`; mai `-p:UseAppHost=false`; su Linux Data con `--filter "Category!=WindowsOnly"`.

## Review Focus

1. **Un database dell'autore fatto con il bundle vecchio** si apre con la stessa passphrase e lo stesso contenuto —
   errore 26 qui sarebbe "passphrase sbagliata" detto a chi ha quella giusta (trappola 1) → Task 1 + Task 2.
2. **Un database rimasto con un journal caldo** (crash a metà transazione col bundle vecchio) si apre col nuovo e il
   journal si annulla, senza errore e senza dati a metà → Task 1 (fixture) + Task 2 (test).
3. **Il DB dentro un `.recbak` nuovo è in chiaro** (`SQLite format 3\0`): un `VACUUM INTO` da solo lo lascerebbe cifrato
   con la chiave della macchina, cioè un backup che si ripristina solo lì (trappola 2) → Task 2.
4. **I `.recbak` già esistenti** (`linux.recbak`, `windows.recbak`, `made-on-linux-v2.18.recbak`) si ripristinano su Linux
   **e** su Windows → Task 2 (test esistenti) + CI.
5. **Un database nuovo nasce in formato SQLCipher 4**, non nel ChaCha20 predefinito di MC: altrimenti il prossimo cambio di
   provider (o un ritorno indietro) non lo leggerebbe → Task 2.

---

### Task 1: Congelare un database del bundle di oggi

**Files:**
- Create: `tests/Recordum.Data.Tests/Security/CipherFixtureGenerator.cs`,
  `tests/Recordum.Data.Tests/Security/LegacyCipherDatabaseTests.cs`,
  `tests/Recordum.Data.Tests/Fixtures/Cipher/` (`sqlcipher-4.5.2-v2.19.db`, `sqlcipher-4.5.2-hot-journal.db` +
  `sqlcipher-4.5.2-hot-journal.db-journal`, `README.md`)
- Modify: `tests/Recordum.Data.Tests/Fixtures/EncryptedDbFixture.cs` (rendere `FixedKeyProvider` `internal`),
  `tests/Recordum.Data.Tests/Backup/RecbakReferenceSeed.cs` (estrarre l'asserzione dei valori noti se oggi vive in
  `CrossPlatformRestoreTests`), `tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj` (copiare `Fixtures/Cipher/**` in output)

**Interfaces:**
- Produces: `CipherFixture.Key` (`byte[]`, 32 byte `0x00..0x1F`), `CipherFixture.Path(string file)`;
  `RecbakReferenceSeed.AssertKnownValuesAsync(RecordumDbContext db)` (le stesse asserzioni di
  `Reference_backup_restores_with_known_values`); i tre file della fixture.

- [ ] **Step 1 — generatore** (`[Trait("Category","FixtureGenerator")]`, `[GeneratorFact]`-like con la variabile
  `RECORDUM_GENERATE_CIPHER_FIXTURE=<cartella>`, skip altrimenti): `EncryptedDbFixture.SeedAsync(new FixedKeyProvider(CipherFixture.Key))`,
  migrazioni reali (schema 2.19), `RecbakReferenceSeed.WriteAsync`, `SqliteConnection.ClearAllPools()`, copia del file in
  `sqlcipher-4.5.2-v2.19.db` (niente `-journal`/`-wal` accanto). Poi il journal caldo: sulla copia `-hot-journal.db`, una
  connessione dalla factory apre `BEGIN IMMEDIATE`, cancella le completion di `RunId` e aggiorna il nome di `RunId` in
  `"HALF"`, forza la scrittura delle pagine (`PRAGMA cache_size = 1` prima del `BEGIN`), e **mentre la transazione è
  aperta** copia `.db` e `.db-journal` nella cartella; poi `ROLLBACK`.
- [ ] **Step 2 — generare con il bundle di oggi** su Linux: `RECORDUM_GENERATE_CIPHER_FIXTURE=tests/Recordum.Data.Tests/Fixtures/Cipher dotnet test … --filter "Category=FixtureGenerator&FullyQualifiedName~CipherFixtureGenerator"`;
  `head -c 16` dei `.db` non è `SQLite format 3`; il `-journal` non è vuoto. README: da quale bundle, quale chiave, perché
  non si rigenera mai dopo il cambio (sarebbe una fixture del bundle nuovo).
- [ ] **Step 3 — test** (`LegacyCipherDatabaseTests`, girano ovunque, Linux e Windows):
  `A_database_made_by_the_sqlcipher_4_5_2_bundle_opens_with_the_same_content` — copia in una cartella temporanea,
  `SqlCipherConnectionFactory.Open` con `CipherFixture.Key`, `PRAGMA integrity_check` = `ok`, schema version `2.19`
  (`DbSchemaVersion`), `AssertKnownValuesAsync` passando da un `RecordumDbContext` con l'interceptor e la chiave della
  fixture; `A_wrong_key_on_the_legacy_database_fails_at_the_first_read_with_26`;
  `A_hot_journal_left_by_the_old_bundle_is_rolled_back` — copia la coppia `.db` + `.db-journal`, apre, nome di `RunId`
  ≠ `"HALF"`, completion di `RunId` presenti, `integrity_check` = `ok`, il `-journal` sparisce.
- [ ] **Step 4** — verdi col bundle di oggi (attesi: è la base). Commit
  `test: un database del bundle SQLCipher di oggi congelato come fixture (D9)`.

### Task 2: Il cambio di provider

**Files:**
- Modify: `Directory.Packages.props`, `src/Recordum.Data/Recordum.Data.csproj` (con il commento su `.Sqlite.Core`
  aggiornato), `src/Recordum.Data/Security/SqlCipherConnectionFactory.cs`,
  `src/Recordum.Data/Interceptors/SqlCipherKeyConnectionInterceptor.cs`, `src/Recordum.Data/Backup/BackupService.cs`
  (`DumpDbAsync`), `src/Recordum.Data/Backup/RestoreService.cs` (`ReEncryptDbAsync`),
  `tests/Recordum.Data.Tests/Security/SqlCipherProviderGuardTests.cs`
- Create: `src/Recordum.Data/Security/CipherPragmas.cs`
- Test: `tests/Recordum.Data.Tests/Security/LegacyCipherDatabaseTests.cs` (Task 1), `CipherFormatTests.cs`,
  `tests/Recordum.Data.Tests/Backup/BackupServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 (fixture, `CipherFixture`, `AssertKnownValuesAsync`).
- Produces: `internal static class CipherPragmas` con `string Key(KeyMaterial key)` →
  `PRAGMA cipher = 'sqlcipher'; PRAGMA legacy = 4; PRAGMA key = "x'<HEX>'";` e `string Rekey(byte[] raw)` (`""` per
  togliere la cifratura: `PRAGMA rekey = '';`). La factory e i due punti dell'interceptor usano `Key(...)` +
  `PRAGMA busy_timeout`; il restore usa `CipherPragmas` per il formato e il rekey; `KeyTransitionService` passa a
  `Rekey(...)` (stessa istruzione di oggi, un solo punto).

- [ ] **Step 1 — perf prima**: `Category=Performance` di Data e UI in locale, numeri nel report (budget: Home < 500 ms).
- [ ] **Step 2 — solo il pacchetto**: scambio in `Directory.Packages.props` e nel csproj, nessun'altra riga. Build e
  suite Data: **attesi i rossi** — i 12 di `sqlcipher_export`, il guard di SEC-1, e i tre di `LegacyCipherDatabaseTests`
  (errore 26: è la trappola 1 vista coi propri occhi). Annotarli nel report.
- [ ] **Step 3 — test nuovi, rossi**: `CipherFormatTests.A_new_database_is_written_in_sqlcipher_4_format` — DB nuovo
  dalla factory, una riga scritta, chiuso; riaperto con **solo** `PRAGMA key` (cifrario predefinito di MC) →
  `SqliteException` 26 alla prima lettura; riaperto con `CipherPragmas.Key` → la riga c'è. (RF 5)
  `BackupServiceTests.The_database_inside_a_new_recbak_is_plaintext` — `CreateAsync`, si apre l'archivio con
  `RecbakReader` (o com'è fatto oggi l'estrazione nei test), i primi 16 byte del DB interno sono `SQLite format 3\0` e
  `PRAGMA integrity_check` senza chiave è `ok`. (RF 3)
- [ ] **Step 4 — implementazione**: `CipherPragmas` e i tre punti chiave (sonda §1); `DumpDbAsync`:
  `VACUUM INTO '<target>'` dalla connessione cifrata, chiusa, poi sul target `SqlCipherConnectionFactory.Open` con la
  stessa chiave e `CipherPragmas.Rekey(empty)` (sonda §2, prima riga; il ciclo su `SQLITE_BUSY` resta);
  `ReEncryptDbAsync`: `File.Copy(plain, destination)`, connessione sul destination **senza** chiave,
  `PRAGMA cipher = 'sqlcipher'; PRAGMA legacy = 4;` poi `PRAGMA rekey = "x'…'"` (sonda §2, "Chiaro → cifrato"); guard di
  SEC-1: `SELECT count(*) FROM pragma_function_list WHERE name = 'sqlite3mc_version'` = 1, con il messaggio che nomina
  `bundle_e_sqlite3` e `SQLite3MC.PCLRaw.bundle`, più un secondo assert che `SELECT sqlite_version()` sia ≥ `3.50`
  (motore mantenuto). Docstring del guard aggiornata (la storia di SEC-1 resta).
- [ ] **Step 5 — rosso del guard contro il provider in chiaro**: in una copia `git archive` nello scratchpad, sostituire il
  bundle con `SQLitePCLRaw.bundle_e_sqlite3` (versione dello stesso ramo di `SQLitePCLRaw.core` che risolve MC) e vedere il
  guard rosso **e** `A_new_database_is_written_in_sqlcipher_4_format` rosso (il file nasce in chiaro: sonda §3). Esito nel
  report; nessuna modifica nel repo.
- [ ] **Step 6 — verdi**: Core, Data (`Category!=WindowsOnly`), Presentation, UI (`Category!=Performance`), Plugins,
  ExportEngine; i tre di `LegacyCipherDatabaseTests`, `CrossPlatformRestoreTests`, `CrossMachineRestoreTests`
  (`made-on-linux-v2.18.recbak`), `DbUnlockService*`, `KeyTransition*`. Perf dopo (Step 1 ripetuto), confronto nel report.
- [ ] **Step 7** — commit `feat(data): SQLite3 Multiple Ciphers al posto del bundle SQLCipher deprecato, formato SQLCipher 4 invariato (D9)`;
  push; CI verde su **entrambi** i job (Windows esegue i nativi win-x64 di MC, il restore incrociato e WPF).

### Task 3: Avvisi di licenza, documenti, review

**Files:**
- Create: `THIRD-PARTY-NOTICES.md` (SQLite3 Multiple Ciphers e `SQLite3MC.PCLRaw.*`: MIT con la nota di copyright
  dell'autore, presa dal pacchetto; SQLite: pubblico dominio; `SQLitePCLRaw.core`: Apache-2.0)
- Modify: `CLAUDE.md` §6 (la riga del bundle: ora SQLite3MC, formato SQLCipher 4 dichiarato in `CipherPragmas`, guard di
  SEC-1 con la domanda nuova; la regola "un solo bundle"), `docs/ARCHITECTURE.md` (dove dice SQLCipher: "formato SQLCipher 4
  tramite SQLite3MC"), `docs/ROADMAP.md` ("Dove siamo": la pila è in `main` dal 2026-10-28; D9 fatta; Passo 0),
  `docs/phase-4-issues.md` (sezione "Onda D9", con eventuali residui), `docs/MILESTONES.md`, la memoria
  `recordum-build-rid-and-cve`
- [ ] **Step 1** — documenti e avvisi; commit e push.
- [ ] **Step 2** — review dell'intero branch (modello più capace) con la sonda in mano; un'onda di correzioni; CI verde.
