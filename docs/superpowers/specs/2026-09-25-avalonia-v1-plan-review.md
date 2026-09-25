# Revisione del percorso verso la v1.0 su Avalonia — problemi, piano rivisto, previsione d'uso

> **Data**: 2026-09-25 · **Branch**: `avalonia-linux-slice` a `0d8323a` · **Stato**: revisione — *propone*, decide l'autore (§9).
> **Decise dall'autore lo stesso giorno**: D1 = dogfooding su **entrambi** i sistemi · D2 = **niente promemoria** prima del
> dogfooding (E14 dopo F8) · D4 = la pila in `main` **adesso**, poi a ogni onda chiusa · D5 = piani da F2 fatti di **decisioni,
> interfacce e test**. Le conseguenze sono già scritte in §6 (Passo 3), §7 e §9; le altre sei decisioni restano aperte.
> **RV-1 chiuso lo stesso giorno**: l'autore ha portato `main` in fast-forward a `911ed24` (`d6cc97e..911ed24`, SEC-1 compreso).
>
> **Cosa è stato rivisto**: la spec di piattaforma (`2026-09-10-avalonia-as-the-platform-design.md`, onde F0–F8), la mappa
> v1.0 (`2026-07-03-v1-redefinition-plan-design.md`, E1–E21), il piano F1 (`2026-09-20-wave-f1-home.md`, Task 6–13 ancora da
> eseguire), il ledger F1, il tracker, il codice e i binari.
>
> **Come**: letture e misure dirette, più cinque revisioni delegate ai plugin installati fra il 16 e il 24/09 —
> `feature-dev:code-explorer` (deriva del piano F1 rispetto al codice), `pr-review-toolkit:silent-failure-hunter` (sblocco del
> DB, permessi su Linux), `pr-review-toolkit:pr-test-analyzer` (test inerti), `ponytail-audit` (cosa non portare), una verifica
> di piattaforma con `context7` + web (Avalonia 12, Velopack, SQLitePCLRaw). **Ogni affermazione è stata riverificata sul codice,
> sui binari o sulla macchina dell'autore prima di finire qui**, tranne quelle marcate *(da documentazione)*; stime e previsioni
> sono marcate come tali.

---

## 0. In una pagina

**Tre cose da fare prima di scrivere altro codice di porting:**

1. **`main` costruisce un database non cifrato.** Il fix di SEC-1 (`feae26b`) vive solo sulla pila di branch; `main` referenzia
   ancora il pacchetto EF completo con versioni flottanti — la configurazione che SEC-1 ha misurato come "cifratura spenta". E
   `main` è, per la regola di E21, la sola build su cui può girare il dogfooding.
2. **Il livello di cifratura è deprecato e porta uno SQLite del 2022.** `SQLitePCLRaw.bundle_e_sqlcipher` 2.1.11 è l'ultima
   versione (marzo 2025), marcata *Legacy* su NuGet dal 2025-10-20; il suo nativo contiene **SQLite 3.39.2**, quindi rientra
   nella CVE-2025-6965, e l'audit NuGet tace perché l'advisory elenca solo `e_sqlite3`. Per un'app che promette privacy è
   bloccante per la v1.0, e va deciso **prima** che esista il DB vero del dogfooding.
3. **Lo sblocco può chiudere fuori l'utente per sempre.** Con "salt presente, database assente" (doppio avvio al primo uso, crash
   in mezzo al restore) `DbUnlockService` riscrive il salt: il database che esiste o che sta per nascere diventa illeggibile, senza
   avviso. Nessun lock di istanza singola; il salt non viene sincronizzato su disco.

**Il piano ha un buco strutturale.** La spec Avalonia ordina il *porting*; la mappa v1.0 (exit criteria, onde 2.9/3/4/5, E14,
dogfooding) non è mai stata ribasata su di essa. La conseguenza più concreta: **la soglia di dogfooding (F2) non soddisfa i
prerequisiti che la mappa stessa impone** — su Avalonia non esiste backup fino a F6, il backup automatico su Linux si spegne da
solo, su Linux non si può creare un'abitudine fino a F5, i promemoria (E14) non hanno un'onda.

**La previsione d'uso** (§5) dice che la grande maggioranza delle sessioni tocca solo Home, fascia Oggi e registrazione; che sul
desktop la registrazione retroattiva è quotidiana, non un caso limite; e che il profilo "diario con foto" romperà il backup entro
pochi mesi, perché le foto non vengono mai ridimensionate e il backup cifra tutto in RAM con un tetto di 2 GiB.

**Il percorso rivisto** (§6) aggiunge un **Passo 0** di messa in sicurezza (Linux, niente UI), corregge il piano F1 (cinque errori
verificati, già corretti nel file), allarga F2 a ciò che serve per usare l'app ogni giorno, e fa della checklist estetica un
cancello di ogni onda invece di un'Onda 3 senza casa.

---

## 1. Fuori dal piano — sicurezza e dati

### RV-1 · `main` costruisce un database in chiaro — *subito*

- **Evidenza**: `git merge-base --is-ancestor feae26b main` → no. `main:src/Recordum.Data/Recordum.Data.csproj` referenzia
  `Microsoft.EntityFrameworkCore.Sqlite` `9.0.*` insieme a `SQLitePCLRaw.bundle_e_sqlcipher` `2.1.*`: è esattamente la coppia che
  SEC-1 ha misurato come "`PRAGMA key` diventa un no-op". `main` è ferma al 2026-08-12.
- **Proposta**: portare in `main` la pila fino a R9 (RV-10) — o, come passo minimo, un hotfix con il solo `feae26b` più
  `SqlCipherProviderGuardTests`.
- **Decisione D4**: la pila fino a R9 va in `main` adesso. `main` è antenato di `r9-day-boundary` (33 commit, SEC-1 compreso,
  suite verde sulla VM l'11/09), quindi è un fast-forward. **Lo esegue l'autore**: da una sessione Claude il classificatore della
  modalità automatica blocca l'aggiornamento di `main` (e l'hook `guard-main.js` vieta commit e push su `main`).
- **Fatto** (2026-09-25): `main` = `911ed24`, pushato.

### RV-2 · SQLCipher deprecato, con uno SQLite del 2022 — *decidere prima dei dati veri; bloccante per la v1.0*

- **Evidenza**: `src/Recordum.Data/obj/project.assets.json` → `bundle_e_sqlcipher/2.1.11`, `lib.e_sqlcipher/2.1.11`.
  `strings libe_sqlcipher.so` (linux-x64) → `3.39.2`, `2022-07-21`. La registrazione NuGet del bundle: ultima versione 2.1.11
  (2025-03-07), `deprecation.reasons = ["Legacy"]`, con rimando alla wiki di SQLitePCL.raw *"SQLite encryption options"*.
- **Doppia sorpresa nel repo**: il commento su NU1903 in `Directory.Build.props` è stantio in entrambe le direzioni — dopo SEC-1
  `e_sqlite3` non è più nel grafo, e l'esposizione vera sta nel nativo SQLCipher, che nessun audit vede.
- **Proposta**: una sonda di una sessione su Linux, sulle due strade indicate dalla wiki (build SQLCipher di Zetetic, a
  pagamento; SQLite3 Multiple Ciphers), con tre domande: apre un DB SQLCipher 4 esistente? cosa sostituisce `sqlcipher_export`
  (backup e restore lo usano)? cosa sostituisce `PRAGMA cipher_version` nel guard di SEC-1? Verificare anche FTS5 (Onda 5.4) e la
  licenza rispetto al MIT del repo. Decisione dell'autore sul risultato (D9).

### RV-3 · Lo sblocco può sovrascrivere il salt di un database vero — *prima del dogfooding*

- **Evidenza**: `DetectState()` torna `NeedsCreation` quando esiste il salt ma non il DB (`DbUnlockService.cs:78`); `CreateAsync`
  controlla solo `HasDatabase()` (`:120`) e `WriteSalt` apre con `FileMode.Create` (`:186`), cioè tronca. Nessun `Mutex` né lock
  file in tutto `src/`. `RestoreService.cs:176-178`: fra le due `File.Move` il file `recordum.db` non esiste. Il salt non viene
  sincronizzato su disco (`:190`, niente `Flush(true)`), mentre SQLite sincronizza ogni commit. Stessa forma per `db.key` in
  `DpapiKeyProvider.cs:15-23`.
- **Scenari**: due finestre "crea passphrase" dopo un doppio clic al primo avvio (la finestra di vulnerabilità è Argon2id — da 0,7
  a 17 s misurati — più l'inizio della migrazione); crash durante il restore; blackout nei secondi dopo la creazione (btrfs, il
  default di Fedora, consolida ogni 30 s). In tutti i casi: al riavvio **ogni passphrase, anche quella giusta, è "sbagliata"**.
- **Proposta** (piccola): lock file esclusivo nella cartella dati per tutta la vita del processo, preso prima di `DetectState` —
  è anche l'istanza singola che l'Onda 4 chiede; un salt esistente non si tronca mai (`FileMode.CreateNew`; "salt senza DB"
  diventa uno stato esplicito che chiede cosa fare); `Flush(flushToDisk: true)`. Ogni punto con un test provato rosso — oggi la
  corsa al primo avvio non ha nessun test.

### RV-4 · Su Linux i dati non sono privati come su Windows — *prima di collegare il backup (F2/F6)*

Misurato con umask 022 e letto nel codice:

| Cosa | Dove | Modo | Esposizione |
|---|---|---|---|
| Cartella dati, `attachments/`, `backups/` | `RecordumOptions.cs:19,22-23` | 0755 | elenco dei file |
| `recordum.db` | default di SQLite | 0644 | cifrato: copiabile e attaccabile offline |
| Foto allegate | `AttachmentService.cs:58` (`File.Copy`) | eredita il sorgente, di solito 0644 | **in chiaro, leggibili** |
| `.recbak` | `BackupService.cs` | 0644 | cifrato |
| **Temp di backup e restore** | `BackupService.cs:38`, `RestoreService.cs:32,85-86` → `Path.GetTempPath()` | 0755/0644 in **`/tmp`** | **dump in chiaro dell'intero DB**, leggibile da ogni utente locale finché il backup gira; resta dopo un crash |

Su Fedora la home è 0700, quindi sulla macchina dell'autore l'esposizione è nascosta; con una home 0755 (default di altre
distribuzioni) no. Su Windows `%TEMP%` e `%LocalAppData%` sono privati per ACL, ed è per questo che il problema non è mai emerso.

**Proposta**: temp di backup/restore **dentro** la cartella dati; `Directory.CreateDirectory(dir, UserRead | UserWrite |
UserExecute)` e chmod 0700 se esiste già. Mezza giornata.

### RV-5 · Il backup automatico su Linux si spegne da solo, in silenzio — *prima del dogfooding*

- **Evidenza**: `BackupScheduler` sta in **Data** e legge la chiave con `ProtectedData.Unprotect` (DPAPI, `BackupScheduler.cs:53`);
  se il file manca o DPAPI fallisce riscrive `backup.auto.enabled = false` (`:41`, `:62`) senza avvisare. La shell Avalonia
  registra `NullAutoBackupKeyStore` e `CoreInitializer` non avvia `BackupSchedulerHost`: **oggi su Avalonia non esiste backup di
  alcun tipo**. Il commento di `NullAutoBackupKeyStore.cs:6` descrive un controllo (`HasKey()`) che lo scheduler non fa.
- **Conseguenza**: la disciplina dati di E21 ("auto-backup attivo dal giorno uno") è impossibile sulla shell su cui la spec fa
  migrare il dogfooding.
- **Proposta**: lo scheduler ottiene la chiave tramite `IAutoBackupKeyStore` (niente DPAPI in Data), con un'implementazione Linux
  (D3); lo spegnimento automatico diventa visibile (log + stato in Impostazioni).

### RV-6 · Sviluppo e dati veri condividono la stessa cartella — *prima del dogfooding*

- **Evidenza**: `RecordumOptions.Default()` usa `LocalApplicationData/Recordum` senza alcun override. `--seed-demo` semina lì;
  una build di branch con una migration nuova migra il DB vero al primo `dotnet run`. L'hook `guard-userdata.js` riconosce solo
  percorsi Windows (`appdata/local/recordum`) e `.recbak`: `rm -rf ~/.local/share/Recordum` passa senza conferma.
- **Proposta**: override della cartella dati (`RECORDUM_DATA_DIR` o `--data-dir`: non è un segreto) e build Debug su una cartella
  separata per default; l'hook riconosce `~/.local/share/Recordum` e `$XDG_DATA_HOME`. È la versione strutturale della disciplina
  di E21, che oggi si regge sulla memoria di chi lancia i comandi.

### RV-7 · Foto: mai ridimensionate, metadati intatti, backup tutto in RAM — *bloccante per la v1.0*

- **Evidenza**: `AttachmentService.AddAsync` fa `File.Copy` del file sorgente (`:58`): nessun ridimensionamento, nessuna rimozione
  dei metadati — la posizione GPS di una foto scattata col telefono resta nel file, e l'export HTML la incorpora in base64. La spec
  1b l'ha scelto consapevolmente (*"No auto-compression — file size user's responsibility"*) rimandando *"Image optimization —
  future Onda 3 polish"*: l'Onda 3 non esiste più come onda, e l'impegno è rimasto senza casa. `AesGcmBackupCipher.EncryptAsync`
  copia l'intero archivio in un `MemoryStream`, poi `ToArray()`, poi cifra in un colpo (`:58-65`; il restore fa lo stesso,
  `:145-154`): fino a 3-4 volte la dimensione dell'archivio in RAM, con un tetto rigido di 2 GiB (`MaxCipherTextBytes`, e comunque
  il limite di `MemoryStream`).
- **Stima**: con 2-3 foto al giorno da telefono (3-5 MB l'una) un utente Food diary accumula 2-5 GB l'anno. Il backup settimanale
  occupa gigabyte di RAM a ogni esecuzione e **supera il tetto in 4-10 mesi**; da quel momento il backup non si crea più.
- **Proposta**: (a) un importer portabile che ridimensiona (lato lungo 2048 px) e toglie i metadati — SkiaSharp è già una
  dipendenza, e F4 ne ha bisogno comunque, perché oggi non esiste un importer da portare; (b) formato `.recbak` **v2 cifrato a
  blocchi** (il byte `FormatVersion` c'è già) **prima della v1.0**: dopo, ogni utente pubblico avrà file v1 da leggere per sempre
  (E17).

### RV-8 · EF Core 9 finisce il supporto il 10 novembre 2026 — *prima della v1.0, conviene subito*

- **Evidenza**: `Microsoft.EntityFrameworkCore.*`, `EFCore.NamingConventions`, `Microsoft.Extensions.DependencyInjection`,
  `System.Security.Cryptography.ProtectedData` sono `9.0.*`. EF Core 9 e .NET 9 sono STS con fine supporto 2026-11-10; la v1.0
  uscirà dopo.
- **Proposta**: salto a EF Core 10 (LTS) su Linux, dove Data gira; `dotnet ef migrations has-pending-model-changes` deve restare
  pulito — è il guasto che questo repo conosce (`PendingModelChangesWarning`). Insieme a RV-9.

### RV-9 · Versioni flottanti su pacchetti crittografici — *prima del dogfooding*

- **Evidenza**: `2.1.*` (SQLCipher), `1.3.*` (Argon2id), `9.0.*` (EF, ProtectedData), `8.4.*`, `1.0.*`; nessun
  `Directory.Packages.props`, nessun lock file. SEC-1 è nato così (*"è uscita la 2.1.12 … sotto un wildcard"*): il fix ha cambiato
  pacchetto e ha lasciato i wildcard.
- **Proposta**: Central Package Management con versioni esatte + `RestorePackagesWithLockFile`, restore `--locked-mode` in CI e
  nelle build di release. La build che apre i dati veri deve essere riproducibile.

---

## 2. Fuori dal piano — stato del repo e metodo

### RV-10 · La pila non sa che R9 è stato verificato

- **Evidenza**: `r9-day-boundary` ha tre commit dell'11/09 che `avalonia-linux-slice` non contiene (`23be772`, `7b9f52b`,
  `911ed24`): suite completa verde sulla VM (WPF 1044/1044), 14 sonde "rosso prima", due lacune di copertura chiuse con test
  nuovi. Sulla pila, CLAUDE.md §7, la spec §14 e F0-D1 dicono ancora che la suite WPF di R9 non è mai stata eseguita e che il
  prerequisito del Task 7 di F0 "è stato scavalcato". Il fix della fixture State (`23be772`) è stato riscoperto e rifatto
  diversamente da F0: conflitto annunciato. Per la spec stessa (§14: verifica a schermo e merge "non bloccano niente") R9 è
  mergiabile da due settimane.
- **Proposta**: merge di `r9-day-boundary` nella pila (i due test nuovi vanno portati in `Recordum.Presentation.Tests`, la fixture
  si riconcilia), documenti corretti; poi `main` avanza (RV-1, D4).

### RV-11 · Nessuna CI

- **Evidenza**: non esiste `.github/`. Il debito "compila ma non è mai stato eseguito" si è presentato con R9, con F0 (38 file di
  test WPF) e con il ramo DPAPI di F1, ogni volta risolto a mano sulla VM giorni dopo.
- **Proposta**: GitHub Actions con due job — `ubuntu` (Core, Data, Presentation, UI headless, Plugins, ExportEngine) e `windows`
  (tutto, WPF serializzato finché esiste, DPAPI, ChartEngine) — con i PNG headless come artefatti, così le schermate si guardano dal
  browser. La VM resta per gli occhi. Su repo privato il piano gratuito dà 2.000 minuti al mese e i runner Windows contano doppio:
  bastano per i branch attivi; un repo pubblico non ha limite (D8).

### RV-12 · CLAUDE.md pesa 98 KB

- **Evidenza**: 98.086 byte (~25k token), caricati in ogni sessione e nei subagenti che ricevono il contesto di progetto — cioè
  in ogni implementatore e reviewer. Con 3-5 dispatch per task SDD sono ~75-125k token di contesto ripetuto **per task**. §7 è un
  diario che contiene fatti ormai falsi (R9 "mai eseguito"; "nessun merge finché la v1.0 WPF non ha fatto il suo pass visivo";
  §8 "RID vincolato a win-x64"; superpowers "pin v6.0.3", installato 6.4.1) e §10 dà la precedenza a quattro `.docx` che non sono
  nel repo né su questa macchina (e descrivono lo schema 2.2: il codice è a 2.18).
- **Proposta**: ≤ 15 KB — invarianti, stato corrente, puntatori; la cronaca in `docs/MILESTONES.md`; §10 riscritto (autorità =
  codice + migration + `ARCHITECTURE.md`). La potatura era rimandata "a fine onde", ma le onde sono cambiate e il costo si paga a
  ogni sessione. Il plugin `claude-md-management` è installato per questo.

### RV-13 · Igiene minore

- **Ledger SDD** git-ignored, copia unica su questa macchina (`.superpowers/sdd/*/progress.md`): è il punto di ripresa dichiarato
  dopo ogni limite di sessione. Committare `progress.md` (non i diff di review).
- **Tracker**: una settantina di voci in stati aperti sparse in 1.716 righe; la sezione "Aperti — bloccanti" contiene B11
  (`verified-fixed`), B1 (`fixed`) e B3 ("tante altre cose non funzionano", maggio). Serve un indice di una pagina: ID · stato ·
  onda che lo chiude.
- **`Markdown.Avalonia 12.0.0-a3`** (alpha) è ancora referenziato da `Recordum.UI` per l'editor di sonda, benché la spec §10 lo
  dichiari uscito dalle dipendenze: renderlo un criterio d'uscita di F4.
- **Branch remoti**: `davide/test` (una struttura `AGENTS.md` proposta da un collaboratore a luglio, mai mergiata né chiusa);
  `plan/v1-redefinition` e `wave-2-5-builder`, già in `main`.
- **Plugin `github`**: il suo server MCP non si connette (*"Authorization header is badly formatted"*) — token da rigenerare.

---

## 3. Problemi del piano

### P-1 · La mappa v1.0 non è mai stata ribasata su Avalonia

La spec Avalonia (§6) ordina solo il porting; la mappa v1.0 (§10) ordina ancora "2.9 → 3 → 5 → 4" su WPF. Nessuno dei due
documenti dice dove finiscono E14 (promemoria), l'Onda 2.9 (E15, E20), l'Onda 3 (E8), l'Onda 5 (E19), l'Onda 4 (E10-E12, E17),
né cosa diventa E13 ("suite Data+WPF verdi sulla VM"). In più la regola di convivenza della spec (§7: *"nessuna feature nuova su
nessuna delle due finché la piattaforma non è una sola"*) vieta E14 fino a F8, mentre la mappa ne fa un prerequisito del
dogfooding. → Una mappa sola (§6 di questo documento) e gli exit criteria riletti (§7). **Risolta con D2**: il dogfooding parte
senza promemoria, quindi il congelamento resta intatto e E14 torna dopo F8.

### P-2 · La soglia di dogfooding (F2) non regge

Cosa manca su Avalonia a fine F2, secondo la spec attuale:

| Serve per usare l'app ogni giorno | Dove sta oggi | Costo di anticiparlo |
|---|---|---|
| Backup automatico e "Backup ora" | F6 — e rotto su Linux (RV-5) | tre dialog, 111 righe XAML in WPF, più il key store |
| Restore (anche solo per portare dati dalla VM a Linux: un DB Windows su Linux è `Unsupported`) | F6 | compreso sopra |
| Creare un'abitudine | F5 (picker dei template 151 righe; builder 696 + anteprima 227) | picker piccolo; builder grande |
| Nota veloce / diario | F4 (`JournalQuickEntryView`, 118 righe) | piccolo |
| Promemoria (E14) | nessuna onda, e vietato dal congelamento | dipende da D2 |
| Aprire l'app senza digitare la passphrase ogni volta (Linux) | F6 (spec §18.2) | dipende da D3 |

Su Windows la situazione è più morbida (DPAPI senza interfaccia; WPF può ancora creare e configurare sullo stesso DB — ma due shell
aperte sullo stesso file sono il rischio `SQLITE_BUSY` già visto in B-dbLock). **Su Linux, a fine F2, non si può creare la prima
abitudine e non esiste backup.** → La soglia la definiscono le abitudini vere dell'autore (manifesto, §5.5), non il numero di
un'onda; il perimetro di F2 si allarga di conseguenza (§6, Passo 3).

### P-3 · Le Onde 4 e 5 presuppongono Windows; il desktop dell'autore è GNOME 50.5 su Wayland

| Presupposto (spec) | Realtà su Linux | Proposta |
|---|---|---|
| Tray opzionale (Onda 4) | `TrayIcon` usa StatusNotifierItem senza ripiego: su GNOME senza l'estensione AppIndicator l'icona non compare e nessuno lo segnala. **Verificato**: sulla macchina dell'autore non c'è un `StatusNotifierWatcher` sul bus. | "Chiudi nel tray" solo se il watcher esiste; altrimenti l'opzione non si offre. |
| Toast nativi (E14) | Avalonia 12 ha solo notifiche dentro la finestra; quelle di sistema passano per `Avalonia.Labs.Notifications` (sperimentale: Windows 17763+, Linux via portal) o per codice nostro. *(da documentazione)* | Scegliere in E14; il ripiego dentro la finestra è gratis. |
| Hotkey globale via `RegisterHotKey` (Onda 5) | Su Wayland solo il portal GlobalShortcuts (GNOME ≥ 48), con conferma dell'utente, un app-id reverse-DNS e un `.desktop` installato; nessuna libreria .NET pronta. *(da documentazione)* | Windows in v1.0; Linux via portal solo se il riordino dell'Onda 5 lo mette in alto. |
| Chrome custom della finestra (F1, Task 10) | Avalonia su Linux gira via X11/XWayland; l'hint di estensione è a supporto limitato e le decorazioni disegnate sono un'opzione separata (`X11PlatformOptions.EnableDrawnDecorations`, presente in 12.1.2 — **verificato**) con un bug aperto sul doppio clic *(da documentazione)*. Il piano F1 ripiega su `WindowDecorations=None` e otto maniglie fatte in casa, senza ombra né snap (F1-11). | Su Linux decorazioni native (snap, ombra e ridimensionamento li dà il gestore di finestre); chrome custom dove l'hint è onorato. Meno codice, più nativo (D7). |
| Velopack (Onda 4, E11, E17) | Su Linux solo `.AppImage`, aggiornamento in `/var/tmp` *(da documentazione)*. **Verificato**: sulla macchina dell'autore c'è `fuse3` ma non `fuse-libs` (libfuse2). | Provare un AppImage su una Fedora pulita e decidere il canale Linux (AppImage o Flatpak) prima di promettere E11/E17 su Linux. |
| Istanza singola "mutex globale" | Un `Mutex` nominato funziona fra processi su Linux, con il prefisso `Global\`; Avalonia non ha un'API di istanza singola. *(da documentazione)* | Il lock file di RV-3, più un canale per riportare in primo piano la finestra esistente. |
| Identità dell'app | Portal di notifiche e di scorciatoie vogliono un app-id e un `.desktop` installato; un AppImage nudo non li ha. *(da documentazione)* | Il packaging Linux installa un `.desktop`. |

**Reggono**: accessibilità (UIA su Windows, AT-SPI2 su Linux), dialog di file via portal (che possono ignorare la cartella
iniziale), `CalendarDatePicker` come equivalente del `DatePicker` WPF, drag & drop senza immagine (già scritto in F1-6). **Da
provare a mano su Fedora**: tasti morti, Compose e IBus in AvaloniaEdit — nessuna fonte certa; `EnableIme` è la leva se falliscono.

### P-4 · Lo sblocco quotidiano su Linux, ed E20

La passphrase si digita a ogni avvio e Argon2id costa da 0,7 a 17 s misurati (piano F1, handoff VM). Con 1-3 avvii al giorno
(§5.2) è l'attrito più ripetuto dell'app su Linux, e E20 (*"avvio → Home interattiva ≤ 3 s"*) così com'è scritto non è
raggiungibile. La spec (§18.2) lo rimanda a F6. → Decisione D3 prima del dogfooding su Linux; E20 riformulato (§7).

### P-5 · L'Onda 3 (E8) non ha una casa

La spec l'ha "assorbita" nel porting, ma nessuna onda F contiene la checklist del linguaggio né l'audit sui due temi. → **Cancello
per onda**: ogni onda F chiude con la checklist spuntata sulle proprie view, in chiaro e in scuro, con i PNG headless allegati (sono
già un artefatto della CI, RV-11). E8 si accumula; alla fine resta uno sweep, non un'onda.

### P-6 · I difetti di logica aspettano l'onda della loro view

La spec (§7) dice: *"il difetto è nella logica → si corregge in `Recordum.Presentation`, e lo prendono entrambe le shell"*. Eppure
i findings di logica sono parcheggiati "all'onda corrispondente". Candidati a un **batch Presentation** su Linux, parallelo a F1/F2,
ognuno col suo test provato rosso:

- **W2V-15** — il builder compone un journal con una metrica durata che nessuna superficie sa registrare (difetto di modello);
- **F0-Y1** — la heatmap-anno si riempie per righe (probabile causa di A5), nella schermata che ripaga l'uso (§5.3, punto 4);
- **W2V-10**, **W2V-12**, **W2V-13**, **W2V-14**, **W2V-17 a/b**, **W2V-20**, **W2V-5**;
- **F0-M4**, **F0-M5**, **F0-Q1**, **F0-T3**, e i tre test inerti del §4.

Così F3 e F5 portano view, non bug.

### P-7 · Cosa non portare (ponytail-audit, riverificato)

- **`DataSourceSectionViewModel`** (34 righe): scrive `habit.{id}.data_source`, che nessun codice legge; non ha una view
  (`HabitEditorViewModel.cs:81` la esclude) ma viene istanziato e caricato a ogni apertura dell'editor. **Cancellare.**
- **`AiSharingSectionView`** e il suo VM (~50 righe): l'AI è fuori dalla v1.0 e l'unico broker spedito è
  `AlwaysAllowPermissionBroker`, quindi il consenso salvato non lo onora nessuno. È una UI placeholder. **Non portare**; la colonna
  di schema resta.
- **`NotificationsSectionView`**: si porta **insieme** al motore di E14, mai prima — altrimenti Avalonia riproduce F0-14.
- **Contagocce (`IScreenColorPicker`)**: solo Windows, per sempre; fuori da Windows `NullScreenColorPicker` è la risposta
  definitiva. (Il portal Screenshot offre `PickColor`, ma chiede D-Bus scritto a mano: non vale per la v1.0.)
- **`ThemeProbe`** (luminanza del pennello calcolata a mano per LiveCharts): a F7 si alimenta da `IThemeService`, non con un
  secondo meccanismo.

### P-8 · La politica di merge è rimasta quella di WPF

*"Nessun merge finché la v1.0 WPF non ha fatto il suo pass visivo"* non ha più oggetto: la v1.0 è Avalonia e il pass visivo è stato
spostato. Intanto `main` — la build del dogfooding, per regola — è 86 commit indietro e insicura (RV-1). → Si mergia in `main` a
ogni onda chiusa (review di branch + CI verde), cominciando dalla pila di oggi (D4).

---

## 4. Errata del piano F1 (Task 6-13)

**Corretti in `2026-09-20-wave-f1-home.md` (nota in testa al file)** — trovati da `feature-dev:code-explorer`, riverificati:

| # | Dove | La prima stesura diceva | Il vero | Effetto se eseguito così |
|---|---|---|---|---|
| 1 | T12, due test | `.Single(c => c.Name == "Tree")` | il Task 7 chiama l'`ItemsControl` `RowsHost`; nessun "Tree" esiste | 2 dei 6 test d'integrazione esplodono, e lo step "rosso prima" non può partire |
| 2 | T11, `UnlockWindow` | code-behind che legge `viewModel.State` | `UnlockViewModel` non espone lo stato (è privato) ma ha già `Heading` | errore di compilazione; ora è un binding a `Heading` |
| 3 | T12 Step 9 | "UI 61, Presentation 918" | dopo T5: UI 106, Presentation 947 | chi lo usa come cancello crede la build rotta |
| 4 | T10 Step 6 | `Passed: 13` | 8 fact + 1 theory × 4 = 12 | falso allarme |
| 5 | Tabella dei file, baseline | `IDbUnlockService` in Presentation; baseline di fine F0 | in `Recordum.Core.Abstractions.Security`; baseline dopo T5 | confusione |

Il resto del piano (firme, comandi, chiavi di risorsa, stringhe, i VM di Home/Workspace/MainWindow) combacia con il codice, e nessun
task tocca WPF oltre lo spostamento del Task 3.

**Da decidere e aggiungere prima dei Task 9-11** (dalle revisioni del codice T1-T5, riverificate):

- **Task 11** — oltre al `CrashLogger`, un sink di log su file (0600) e il log dell'eccezione nel `catch` di
  `DbUnlockService.UnlockAsync` (`:95`). Oggi ogni errore di sblocco — salt corrotto, permessi, disco pieno, pagina 1 corrotta che
  SQLite riporta come codice 26, cioè "passphrase sbagliata" — produce lo stesso messaggio e nessuna traccia.
- **Task 9** — i comandi Backup/Ripristina/Export del ribbon puntano a launcher `Null` sempre abilitati: nasconderli o
  disabilitarli finché le loro onde non arrivano.
- **Task 10** — checklist del chrome anche su GNOME Wayland (il desktop vero, non solo X11), e decisione D7.
- **Codice T1-T5**: il commento in `AvaloniaShellServices.cs` dice che il cast del lifetime torna null, ma un cast diretto lancia,
  e un lifetime null arriva fino a "Esci" come `NullReferenceException` (serve `ThrowIfNull`); `AvaloniaThemeService` assegna
  `_initialized`/`_mode` prima che il salvataggio riesca.
- **Test inerti** (`pr-test-analyzer`, riverificati): nessun test osserva `SubmitCommand.CanExecuteChanged`
  (`UnlockViewModel.cs:118`), il `provider.Dispose()` sul percorso di passphrase errata (`DbUnlockService.cs:145,148`), o
  `pluginManager.ListEnabled()` (`CoreInitializer.cs:36`). **Mancano del tutto**: la corsa al primo avvio (RV-3), l'idempotenza di
  `CachingKeyProvider.Dispose`, una composizione completa `AddRecordumCore` + `AddRecordumAvaloniaShell` risolta insieme.

---

## 5. Previsione d'uso — chi userà la v1.0, e come

> **Previsione, non misura.** Parte da ciò che abbiamo deciso sulla v1.0: desktop Windows e Linux, offline, senza account, niente
> mobile fino alla Fase 5; cinque tipi e otto template; niente gamification; un pubblico che arriva da Obsidian e dalla privacy
> (analisi del 2026-07-03). Il dogfooding (E21) serve a falsificarla.

### 5.1 Chi

| Profilo | Abitudini tipiche (tipo · template) | Cosa cerca | Superfici calde | Dove il piano lo tocca |
|---|---|---|---|---|
| **L'autore**, primo utente | un po' di tutto: sonno, umore, diario, allenamento, azioni quotidiane | un'app che usa davvero, dalla tastiera | tutte — e ogni giorno anche da sviluppatore | RV-6, P-4 |
| **Salute cronica** | State (sintomi, umore 1-5) · Counter (farmaci, acqua) · Journal — *Mood tracker, Water intake, Daily journal* | uno storico di cui fidarsi, e un PDF da portare al medico | Oggi, registrazioni retroattive, export PDF | RV-7 (metadati nell'export), R9, E3 |
| **Allenamento / scrittura** | Counter con metriche, serie, sessioni e timer; obiettivi settimanali — *Workout log* | vedere i progressi | quick-add a serie, timer, Dettaglio, grafici | F7 arriva per ultima |
| **Astinenza** | Resistance — *Stop smoking* | l'orologio che scorre; un posto dove segnare la ricaduta senza giudizio | riga Home (orologio, Inizio/Ricaduta) | già coperto da F1 |
| **Diario / Food diary** | Journal "nota prima" con foto (U7) — *Food diary, Daily journal* | velocità: apri, scrivi, foto | nota veloce, editor, foto | RV-7, F4 |

### 5.2 Ritmi

| Momento | Frequenza (utente attivo) | Durata | Superfici | Onda |
|---|---|---|---|---|
| Avvio e sblocco | 1-3 al giorno | 2-20 s | sblocco (su Linux: passphrase), Home a freddo | F1 |
| Check-in veloce (spunta, +1, un'occhiata all'orologio) | 2-6 al giorno | 5-30 s | riga Home e azione rapida | F1 |
| Valori misurati (durata del sonno, ml, serie, stato) | 1-4 al giorno | 20-60 s | tab dell'abitudine → Oggi → quick-add | F2 |
| Recupero retroattivo ("ieri non ho segnato") | 2-5 a settimana | 30-90 s | calendario / giorno → registrazione | F2-F3 |
| Nota di diario / food diary | 1-3 al giorno, per chi tiene un diario | 1-10 min | nota veloce, editor, foto | F4 |
| Sguardo indietro | 1 a settimana | 3-10 min | Dettaglio: heatmap, streak, obiettivo, andamento | F3, F7 |
| Ritocchi di configurazione | spesso nella prima settimana, poi 1-2 al mese | 1-5 min | Configura | F5 |
| Creazione di abitudini | 5-15 nella prima settimana, poi ~1 al mese | 2-5 min | template, builder | F5 |
| Backup (controllo o manuale) | ~1 al mese e prima degli aggiornamenti | 1 min | backup, storico | F6 |
| Export | 0-1 al mese (salute cronica: prima di una visita) | 2 min | export PDF, CSV | F6 |
| Grafici personalizzati | raro | 5-15 min | builder dei grafici | F7 |
| Restore | quasi mai — ma è il momento peggiore per scoprire un difetto | 5 min | restore | F6 |

### 5.3 Cosa ne segue per il piano

1. **Il prodotto è Home + Oggi + registrazione.** Stima: l'85-90% delle sessioni tocca solo le superfici di F1 e F2. Latenza, zero
   crash e tastiera valgono lì più che altrove, ed è lì che deve partire il dogfooding.
2. **Sul desktop la registrazione retroattiva è quotidiana**, perché la vita succede lontano dal PC. "Ieri" deve costare quanto
   "oggi", e la correttezza di R9 è un fatto di tutti i giorni → la verifica a schermo di R9 appartiene a F2.
3. **La prima settimana decide chi resta** (E6, E7, E10): template, builder, onboarding. Per il dogfooding contano poco (l'autore sa
   già cosa tracciare), per la release sono decisivi.
4. **Senza gamification, la ricompensa è lo sguardo indietro**: heatmap, streak e obiettivi nel Dettaglio. F0-Y1 (heatmap-anno per
   righe) sta nella schermata più gratificante → batch Presentation (P-6).
5. **Su desktop i promemoria valgono meno di quanto la mappa presume**: l'app è chiusa per gran parte del giorno e su GNOME non c'è
   tray. Il promemoria che rende di più è **uno al giorno, "fai il punto"**, all'ora scelta; quelli per singola abitudine vengono
   dopo. È un E14 minimo più piccolo di quello scritto.
6. **L'attrito più ripetuto su Linux è lo sblocco** (P-4): 1-3 volte al giorno, con Argon2id da 0,7 a 17 s.
7. **Volumi da cui misurare E9 ed E20** (stima): 8-20 abitudini, 1-3 anni → da 5.000 a 50.000 registrazioni; chi tiene un diario
   300-1.000 note l'anno; chi usa Food diary 500-1.500 foto l'anno da telefono (2-7 GB l'anno senza ridimensionamento). Il "dataset
   realistico" di E9 va definito con questi numeri, e il backup deve reggere l'ultimo (RV-7).
8. **Chi arriva da Obsidian vive sulla tastiera**: le scorciatoie di riga (decisione 5 del piano F1) e la palette comandi (Onda
   5.6) saranno fra le cose più usate.
9. **Export**: PDF (per il medico) e CSV (per i fogli di calcolo) faranno quasi tutto; HTML, Markdown e JSON sono rari ma costano
   nulla (l'ExportEngine è già `net10.0`).
10. **I grafici personalizzati sono la funzione più rara e l'onda più costosa (F7).** I grafici del Dettaglio (andamento, orario) si
    guardano ogni settimana; il builder dei grafici no. È l'unico candidato serio alla 1.1 se il calendario stringe — E2 resta una
    scelta dell'autore (D6).

### 5.4 Un'ipotesi per il riordino dell'Onda 5

La mappa (§1-bis.1) riordina l'Onda 5 dopo almeno due settimane di dogfooding. Questa è la previsione da falsificare, dalla più
alla meno usata:

1. **Palette comandi + quick switcher (5.6)** — quotidiane per chi usa la tastiera; su Avalonia sono un overlay e un registro.
2. **Pausa / vacanza (UI)** — il motore c'è; evita la frustrazione dello streak dopo le ferie.
3. **Routine ordinata** — per chi usa *Morning routine*.
4. **Ricerca FTS5** — cresce col diario: inutile al lancio, preziosa dopo due o tre mesi (FTS5 va verificato nel provider di RV-2).
5. **Quick capture globale** — molto utile su Windows; su GNOME Wayland solo via portal, con conferma (P-3).
6. **Risparmio + craving** — solo per il profilo astinenza.
7. **Digest settimanale** — si sovrappone allo sguardo indietro nel Dettaglio.
8. **Import CSV** — per chi arriva nuovo conta poco: il CSV è simmetrico al *nostro* export.

### 5.5 Il manifesto di dogfooding (da compilare dall'autore)

La soglia la decidono le abitudini vere, non il numero di un'onda. Il perimetro di F2 diventa l'unione della colonna "Superficie":

| Abitudine reale | Tipo | Gesto quotidiano | Superficie che serve | Onda che la porta |
|---|---|---|---|---|
| *es.* Sonno | Counter durata, ripetibile ("Night"/"Nap") | durata al mattino, a volte il giorno dopo | Oggi + quick-add durata + giorno precedente | F2 |
| *es.* Umore | State 1-5 | la sera | Oggi (slider) | F2 |
| *es.* Diario | Journal "Diary" | la sera, 5 minuti | editor | F4 → anticipare? |
| … | | | | |

---

## 6. Il percorso rivisto

I passi 0 e 2 girano su Linux senza interfaccia e si possono affiancare a F1.

**Passo 0 — Mettere in sicurezza la base** (Linux, ~2 sessioni)
- 0.1 Riconciliare R9 nella pila (RV-10); con la CI verde, portare in `main` la pila fino a R9 — o almeno SEC-1 (RV-1).
- 0.2 Sblocco sicuro: lock di istanza singola, salt mai troncato, fsync (RV-3); cartella dati 0700 e temp di backup dentro di essa
  (RV-4); cartella dati sovrascrivibile e hook che conosce Linux (RV-6). Ogni fix con test provato rosso.
- 0.3 Pacchetti: Central Package Management + lock file (RV-9); EF Core 10 LTS prima del 10 novembre (RV-8); **sonda sul provider
  di cifratura** (RV-2) — la decisione sul risultato è dell'autore.
- 0.4 CI GitHub Actions (RV-11).
- 0.5 CLAUDE.md ≤ 15 KB, ledger committato, indice del tracker (RV-12, RV-13).

**Passo 1 — Chiudere F1**, con l'errata già applicata e le aggiunte del §4 decise; chrome secondo D7.

**Passo 2 — Batch Presentation** (affiancato): P-6; le cancellazioni di P-7; l'importer delle foto (ridimensiona e toglie i
metadati, RV-7a); il formato `.recbak` v2 a blocchi (RV-7b) — quest'ultimo può slittare al Passo 7, **non oltre la v1.0**.

**Passo 3 — F2 ridefinita: "si usa ogni giorno", su Windows e su Linux** (D1 = entrambi, D2 = niente promemoria). Oggi, i
quick-add dei cinque tipi, `TimerStatusBar` (come da spec), più:
- la nota veloce (`JournalQuickEntryView`, da F4) — e l'editor del diario se il manifesto lo chiede (§5.5);
- il backup su **tutti e due** i sistemi: scheduler avviato, key store per sistema (DPAPI su Windows senza passare da WPF, su Linux
  secondo D3), "Backup ora", restore, storico (da F6; RV-5);
- **il restore incrociato**: un `.recbak` fatto su Windows si ripristina su Linux e viceversa. È il ponte fra le due macchine (un DB
  Windows su Linux è `Unsupported` per costruzione). Il formato non dovrebbe dipendere dalla piattaforma — il DB viaggia come dump e
  si ricifra con la chiave locale — ma nessun test l'ha mai provato: serve un test di andata e ritorno, e i due job della CI lo
  rendono reale;
- per Linux: il picker dei template e il builder (da F5), perché lì WPF non c'è e senza di loro non si crea la prima abitudine; lo
  sblocco secondo D3 (fino alla decisione: passphrase con cache, come oggi);
- per Windows: il lock di istanza singola (RV-3) vale **anche fra WPF e Avalonia** — stessa cartella dati, e due shell aperte
  insieme sono il rischio `SQLITE_BUSY` di B-dbLock. WPF lo prende come correzione di un difetto di dati, che il congelamento ammette;
- il percorso retroattivo e la verifica a schermo di R9 (§5.3, punto 2);
- il ricontrollo dei `W2V` della sezione §3 della checklist del pass visivo.

**Niente promemoria** (D2): `NotificationsSectionView` non si porta in F5 finché non arriva E14, dopo F8 (P-7). F2 esce quando il
manifesto (§5.5) è coperto su entrambe le macchine.

**Passo 4 — ★ Dogfooding (E21) su Avalonia, su Windows e su Linux**, senza promemoria, con `.recbak` come ponte fra le due macchine e
la disciplina dati resa strutturale (RV-6). Findings `DF-*`. Dopo almeno due settimane: riordino dell'Onda 5, partendo
dall'ipotesi del §5.4.

**Passo 5 — F3 → F4 → F5 → F6 → F7**, ognuna chiusa da: review di branch, CI verde su Linux e Windows, checklist del linguaggio in
chiaro e in scuro con i PNG (P-5), verifica a schermo su Windows e su GNOME, merge in `main` (P-8). In F7 prima i grafici del
Dettaglio, poi il builder dei grafici (D6).

**Passo 6 — F8**: WPF si spegne, secondo i criteri della spec (§7), che a questo punto sono in gran parte già maturati.

**Passo 7 — Verso il pubblico**: E14 (promemoria, con i vincoli di P-3; il più utile è uno al giorno, §5.3 punto 5) → Onda 2.9 su
Avalonia (E15, E20) → Onda 5 riordinata, con la fattibilità su Linux scritta voce per voce (P-3) → Onda 4 su due sistemi: packaging Windows (Velopack) e Linux (canale deciso), onboarding, `/security-review` (provider
di cifratura e permessi Linux inclusi), beta su Windows, Fedora GNOME e almeno un secondo ambiente Linux.

---

## 7. Exit criteria riletti per Avalonia (proposta)

| # | Oggi | Proposta |
|---|---|---|
| E1 | Chart del Dettaglio sensati per i 5 tipi | idem, su Avalonia (F7), Windows e Linux |
| E2 | Charts custom end-to-end | idem, oppure 1.1 (D6) |
| E3 | Export 5 formati leggibili | + nessun metadato di posizione nelle immagini esportate (RV-7) |
| E4 | Backup/restore robusto, file corrotti inclusi | + backup oltre i 2 GiB; + nessun file in chiaro fuori dalla cartella dati (RV-4, RV-7) |
| E5 | Foto nelle note + inclusione nel backup | + ridimensionamento all'import |
| E6, E7 | Template auto-esplicativi; builder con anteprima viva | su Avalonia (F2/F5) |
| E8 | Audit estetico, zero superfici fuori linguaggio | cancello per onda (P-5) + sweep finale; due temi, due sistemi |
| E9 | Home < 500 ms su dataset realistico | dataset definito dai volumi del §5.3, punto 7 |
| E10, E11 | Onboarding; packaging su macchina pulita | Windows **e** Linux (Fedora GNOME + un secondo ambiente) |
| E12 | `/security-review` senza findings alti | + provider di cifratura manutenuto (RV-2) + permessi Linux (RV-4) |
| E13 | Suite Data+WPF verdi sulla VM | **CI verde su Linux e Windows** per tutte le suite |
| E14 | Promemoria minimi (toast ad app aperta/tray) | **dopo F8** (D2): un promemoria giornaliero di sistema sui due OS; tray solo dove esiste (P-3) |
| E15, E16 | Verità sul core; template = composizione | invariati, su Avalonia |
| E17 | Aggiornamento sicuro | + lettura dei `.recbak` v1 e v2; percorso di aggiornamento su entrambi i sistemi |
| E18 | Impostazioni complete | invariato (F6) |
| E19 | Onda 5 | riordinata, con la fattibilità su Linux dichiarata per ogni voce |
| E20 | Avvio → Home interattiva ≤ 3 s | misurato da "sblocco concesso" (keyring o passphrase inviata); Argon2id a budget separato |
| E21 | Dogfooding ≥ 4 settimane | su Avalonia, su Windows **e** Linux (D1), senza promemoria (D2), con `.recbak` come ponte e i dati isolati dallo sviluppo (RV-6) |

---

## 8. Costi e metodo

- **Il piano F1 è lungo 10.361 righe per 13 task** (~800 righe per task, codice completo). Il codice è stato scritto e verificato
  in worktree usa-e-getta dagli autori del piano, poi buttato e ribattuto dagli implementatori partendo dalla prosa: 99-141k token per
  implementatore, misurati su T1, T2 e T5, review escluse. F2-F7 sono più grandi (`HabitScreen.xaml` 1.412 righe, il builder 923,
  le 13 sezioni di Configura). Proposta: piani fatti di **decisioni, interfacce, obblighi di test e trappole** (≤ 2.000 righe per
  onda), con il codice solo dove il codice *è* la decisione; oppure il worktree dell'autore del piano diventa il ramo
  d'implementazione e si rivede come qualsiasi diff (D5).
- **CLAUDE.md** a 15 KB invece di 98 risparmia ~20k token a ogni dispatch (RV-12): su un'onda da 13 task, con 3-5 dispatch
  ciascuno, dell'ordine di un milione di token.
- **Modelli**: il ledger dice che Haiku non è più veloce di Sonnet in tempo reale e produce errori "muti" che la review prende. Con
  piani meno trascritti conviene Sonnet per implementare, Opus sulle review dei task a rischio (sblocco, chrome, backup) e sulla
  review di branch — quella che ha trovato i Critical di 2.4, 2.5 e R9, e che va tenuta.
- **La CI sostituisce l'handoff VM per i test** (RV-11); la VM resta per gli occhi. Senza CI su Windows, ogni onda chiusa è una
  scommessa sul ramo DPAPI e sui 23 test legati a WPF.
- **Rischio di calendario, detto prima**: con il passo attuale restano sei onde di porting, poi 2.9, 5 e 4. EF Core 9 scade prima
  (RV-8), e il provider di cifratura va scelto prima dei dati veri (RV-2).

---

## 9. Decisioni per l'autore

| # | Decisione | Opzioni | Proposta | Esito |
|---|---|---|---|---|
| D1 | Su quale sistema fai dogfooding? | Linux (GNOME Wayland) / Windows (VM) / entrambi | Linux | **Entrambi** (2026-09-25) → F2 su due sistemi, con il restore incrociato come ponte (§6, Passo 3) |
| D2 | Promemoria prima del dogfooding? | sì, minimo / sì, completo / no | sì, minimo | **No** (2026-09-25) → E14 dopo F8; il congelamento delle feature resta intatto |
| D3 | Sblocco su Linux | portachiavi di sistema (Secret Service) / file 0600 "ricorda su questo dispositivo" / passphrase a ogni avvio | portachiavi, con ripiego sulla passphrase | aperta — **serve prima di F2** |
| D4 | Politica di merge | la pila in `main` adesso, poi ogni onda / solo l'hotfix SEC-1 | adesso | **Adesso + ogni onda** (2026-09-25) → `main` ← `r9-day-boundary` in fast-forward, **eseguito dall'autore** (`911ed24`); F0 dopo che la suite WPF ha girato su Windows; poi ogni onda F alla chiusura |
| D5 | Formato dei piani da F2 | decisioni + interfacce + test / codice completo come F1 / worktree del piano = implementazione | decisioni + interfacce + test | **Decisioni + interfacce + test** (2026-09-25), ≤ 2.000 righe per onda |
| D6 | Builder dei grafici | in v1.0 / 1.1 | decidere a F7 | aperta — si decide a F7, dopo i grafici del Dettaglio |
| D7 | Chrome della finestra su Linux | decorazioni native / chrome custom con maniglie fatte in casa (piano F1) | native su Linux, custom dove l'hint è onorato | aperta — **serve prima del Task 10 di F1** |
| D8 | CI | repo privato a minuti limitati / repo pubblico prima | a scelta: la CI serve in entrambi i casi | aperta — serve al Passo 0.4 |
| D9 | Provider di cifratura | build Zetetic / SQLite3 Multiple Ciphers / altro | dopo la sonda (RV-2) | aperta — la sonda è al Passo 0.3 |
| D10 | Foto | ridimensionare e togliere i metadati all'import / conservare gli originali | ridimensionare; "conserva l'originale" solo se qualcuno lo chiede | aperta — serve al Passo 2 |

**Il merge di D4, pronto da eseguire** (dal tuo terminale, o col prefisso `!` in una sessione):

```bash
git checkout main && git merge --ff-only r9-day-boundary && git push origin main && git checkout avalonia-linux-slice
```

Dopo, sul branch Avalonia resta da riconciliare R9 (RV-10: i due test nuovi della VM in `Recordum.Presentation.Tests`, la fixture
State rifatta due volte): è il primo lavoro del Passo 0.

---

## Fonti esterne

- Fine supporto di EF Core 9 / .NET 9: [.NET STS releases supported for 24 months](https://devblogs.microsoft.com/dotnet/dotnet-sts-releases-supported-for-24-months/) · [EF Core releases and planning](https://learn.microsoft.com/en-us/ef/core/what-is-new/)
- Deprecazione del bundle SQLCipher: registrazione NuGet di `sqlitepclraw.bundle_e_sqlcipher` (letta il 2026-09-25) · [SQLite encryption options for use with SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw/wiki/SQLite-encryption-options-for-use-with-SQLitePCLRaw)
- Notifiche native in Avalonia: [issue #2155](https://github.com/AvaloniaUI/Avalonia/issues/2155) · [Avalonia.Labs.Notifications](https://www.nuget.org/packages/Avalonia.Labs.Notifications)
- Decorazioni disegnate su X11: [issue #22239](https://github.com/AvaloniaUI/Avalonia/issues/22239)
- Velopack su Linux: documentazione Velopack, sezione Linux (via context7 `/velopack/velopack.docs`)
