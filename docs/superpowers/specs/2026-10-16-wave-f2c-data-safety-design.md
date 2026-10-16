# F2c — Dati al sicuro (design)

> Data: 2026-10-16 · Branch: `f2c-data-safety` (da `f2b-creation`) · Autorità: review `2026-09-25-avalonia-v1-plan-review.md`
> §6 Passo 3, RV-4, RV-5; decisioni D3 e D11 (ROADMAP). Perimetro scelto dall'autore il 2026-10-16: **opzione B** — il nucleo
> (portachiavi, backup automatico e manuale, restore, storico, restore incrociato) **più F1-SEC1**. Il `.recbak` v2 a blocchi
> (RV-7b) resta fuori: va prima o dentro F2d, quando arrivano le foto.

## 1. Perché

Su Linux, che per D11 è la casa dei dati veri, oggi Recordum chiede la passphrase a ogni avvio (Argon2id ogni volta) e non ha
backup di alcun tipo: la shell Avalonia registra `NullAutoBackupKeyStore`, non avvia `BackupSchedulerHost` e non ha le finestre
del backup. Il dogfooding (E21, "auto-backup attivo dal giorno uno") non può partire così.

**Fatto quando**: su Linux Recordum si apre senza digitare niente dopo la prima volta; il backup automatico gira davvero, e se
si spegne lo dice; "Backup ora", "Ripristina…" e lo storico sono raggiungibili; un `.recbak` fatto su Windows si ripristina su
Linux e viceversa, e un test lo prova su entrambi i job della CI; il database nasce 0600.

## 2. Decisioni

1. **Sblocco su Linux dal portachiavi di sistema** (D3), via **P/Invoke su `libsecret-1.so.0`**
   (`secret_password_store_sync` / `_lookup_sync` / `_clear_sync`, schema `org.recordum.Secret`, attributi `kind` e
   `data_dir`). Scartati: Secret Service su D-Bus scritto a mano con Tmds.DBus (3-4 volte il codice, il prompt di sblocco
   della collezione da gestire noi) e `secret-tool` (non installato di serie, segreti a un processo esterno).
2. **Nel portachiavi va la chiave derivata** (32 byte, base64), non la passphrase: niente Argon2id all'avvio (E20), e la
   passphrase resta il solo modo di recuperare i dati se il portachiavi si perde.
3. **Casella "Ricorda su questo dispositivo" nella finestra di sblocco, spuntata per default**, sia alla creazione sia
   all'inserimento. Spenta, con una riga che spiega perché, quando il portachiavi non è disponibile. "Dimentica su questo
   dispositivo" sta nella pagina "Dati e backup".
4. **Due segreti, una astrazione**: `db-key` (la chiave del database) e `auto-backup` (la passphrase dei `.recbak`
   automatici). L'attributo `data_dir` (percorso assoluto della cartella dati) separa le installazioni: una `--data-dir` di
   prova non legge né sovrascrive la chiave dei dati veri.
5. **Windows non cambia**: lo sblocco resta DPAPI senza finestra; la shell Avalonia su Windows registra il
   `DpapiAutoBackupKeyStore` che c'è già in Data. Niente `DpapiSecretStore`: non servirebbe a nessuno.
6. **Finestra Impostazioni ridotta a una pagina, "Dati e backup"**, scheletro che F6 riempie con le altre pagine: backup
   automatico (acceso, frequenza, cartella, passphrase del backup), "Backup ora", "Ripristina…", lo storico, "Dimentica su
   questo dispositivo". Usa i ViewModel che esistono (`BackupSettingsViewModel`, `BackupDialogViewModel`,
   `RestoreDialogViewModel`, `BackupHistoryViewModel`); i dialog di file passano da `StorageProvider`.
7. **F1-SEC1**: `recordum.db`, `-wal`, `-shm` nascono 0600 su Unix, e all'avvio si riallineano se sono più aperti.

## 3. Componenti

| Unità | Dove | Cosa fa |
|---|---|---|
| `ISecretStore` | Core, `Abstractions/Security` | `byte[]? TryRead(SecretKind)`, `void Write(SecretKind, ReadOnlySpan<byte>)`, `void Delete(SecretKind)`, `bool IsAvailable`. Errori del portachiavi → `SecretStoreUnavailableException` (mai un'eccezione nativa che risale). |
| `LibsecretSecretStore` | Data, `Security` (accanto a `Dpapi*`), `[SupportedOSPlatform("linux")]` | P/Invoke; costruito con la cartella dati (attributo `data_dir`). `DllNotFoundException` / errore GLib → non disponibile. |
| `NullSecretStore` | Data | `IsAvailable = false`, legge null, `Write` lancia (come `NullAutoBackupKeyStore`: mai fingere di aver salvato). |
| `SecretStoreAutoBackupKeyStore` | Data | `IAutoBackupKeyStore` sopra `ISecretStore` (`SecretKind.AutoBackup`, UTF-8). Prende il posto di `NullAutoBackupKeyStore` su Linux. |
| `DbUnlockService` | Data | Nuovo passo nello stato `NeedsPassphrase`: `TryUnlockFromStoreAsync()` — legge `db-key`, la prova con una **lettura** (`SqliteException` 26 = chiave sbagliata, `ClearAllPools()` dopo), restituisce il provider o null. Chiave sbagliata → `Delete(DbKey)`. Dopo uno sblocco a passphrase riuscito con "ricorda" → `Write(DbKey, chiave derivata)`. |
| `UnlockViewModel` | Presentation | `RememberOnDevice` (default true), `CanRemember` (= store disponibile), `StoredKeyRejected` (la riga "la chiave salvata non era più valida"). |
| Shell Avalonia | UI | `App.ResolveKeyAsync` prova lo store prima di mostrare la finestra; registra `LibsecretSecretStore` + `SecretStoreAutoBackupKeyStore` su Linux, `DpapiAutoBackupKeyStore` su Windows; avvia `BackupSchedulerHost`; finestra Impostazioni con la pagina "Dati e backup". |

`CoreInitializer` oggi dice di **non** avviare `BackupSchedulerHost` perché lo scheduler legge DPAPI: con `IAutoBackupKeyStore`
(0.7) la ragione è caduta, e il commento va aggiornato insieme all'avvio.

## 4. Flussi e casi di errore

**Avvio su Linux**: opzioni → lock → `ReconcileOrphans()` → `DetectState()` (invariati). In `NeedsPassphrase`:
- `db-key` presente e giusta → si apre la Home senza finestra (niente Argon2id);
- presente e sbagliata (es. database ricreato altrove) → cancellata, finestra della passphrase con `StoredKeyRejected`;
- portachiavi non disponibile (`libsecret` assente, nessun servizio, collezione bloccata e prompt annullato, timeout) →
  finestra della passphrase come oggi, casella spenta con la spiegazione;
- in `NeedsCreation` la casella c'è, spuntata; la chiave si scrive solo **dopo** che il database è creato e letto.

Una scrittura fallita nel portachiavi dopo uno sblocco riuscito non blocca l'avvio: si apre l'app, si scrive nel log, e la
pagina "Dati e backup" mostra che la chiave non è ricordata.

**Restore**: ricifra il database con la chiave locale, quindi `db-key` resta valida — lo asserisce un test.

**Backup automatico**: `BackupSchedulerHost` parte su entrambi i sistemi. Senza passphrase del backup resta spento e lo dice
(riga nella pagina + log, come fa già lo scheduler); idem se la passphrase sparisce dopo.

**Restore incrociato**: il `.recbak` porta un dump in chiaro cifrato con Argon2id + AES-GCM, senza nulla di dipendente dal
sistema; si verifica con file di riferimento prodotti su ciascun sistema (§5).

## 5. Test (obblighi)

- **`LibsecretSecretStore` contro un Secret Service vero**: scrivi → leggi → cancella → leggi null; due `data_dir` diversi
  non si vedono. `[Trait("Category", "Keyring")]`. In locale GNOME Keyring; in CI il job Linux avvia `gnome-keyring-daemon`
  sbloccato dentro `dbus-run-session` (push che tocca `.github/workflows/`: serve lo scope `workflow`).
- **Sblocco** (store finto, DB privato vero): chiave giusta → nessuna passphrase; sbagliata → cancellata e stato
  `NeedsPassphrase`; store che lancia → ripiego sulla passphrase; "ricorda" spuntata → scritta, **non spuntata → non
  scritta** (parametro muto); dopo un restore la chiave salvata apre ancora il database.
- **Registrazione DI** per sistema (Linux: libsecret + adattatore; Windows: DPAPI) e avvio del `BackupSchedulerHost`.
- **Backup automatico**: con la passphrase nello store finto gira e produce un `.recbak`; senza, resta spento e la pagina
  mostra l'avviso.
- **Restore incrociato**: due `.recbak` di riferimento in `tests/Fixtures/` (dati demo noti). Quello Linux si genera in
  locale; quello Windows lo genera il job Windows della CI con un test generatore attivato da una variabile d'ambiente, e si
  committa. Un test ripristina **entrambi su entrambi i sistemi** e confronta abitudini, completamenti e note.
- **Pagina "Dati e backup"** (headless): sezioni presenti, campi passphrase con `AutomationProperties.Name`, avviso visibile
  quando serve; schermate chiaro/scuro con asserzione strutturale. `SystemTempGuardTests` resta verde (nessun file in chiaro
  fuori da `TempDirectory`).
- **F1-SEC1**: un test dei permessi su Linux, rosso contro il codice di oggi.

Ogni test di regressione si prova rosso contro il codice rotto (CLAUDE.md §7).

## 6. Fuori

`.recbak` v2 a blocchi (RV-7b) e foto (D10): F2d. Le altre pagine delle Impostazioni: F6. Cambio della passphrase del
database: non richiesto. KWallet senza il bridge Secret Service: non supportato (ripiego sulla passphrase).
