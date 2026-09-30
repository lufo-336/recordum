# Sonda sul provider di cifratura — Passo 0.9 (RV-2), per la decisione D9

**Branch**: `avalonia-linux-slice`. **Tipo**: sonda, non una feature — l'esito è un rapporto misurato. **Nessun cambio di
provider nel repo**: tutto il codice di prova è vissuto in una cartella usa-e-getta fuori dal repository, comprese le copie
del codice su cui ho girato le suite. **Dove**: Fedora 44, linux-x64, .NET SDK 10.0.401, 2026-09-30. **Windows e macOS non
sono stati misurati** (vedi "Cosa non è stato misurato").

## Verdetto

**SQLite3 Multiple Ciphers**, dal pacchetto NuGet che il suo autore pubblica direttamente (`SQLite3MC.PCLRaw.bundle`
2.4.0, MIT, gratuito), **apre, scrive e ricrea i database SQLCipher 4 di Recordum** senza convertirli, e i file che
scrive si riaprono sia col bundle di oggi sia con SQLCipher 4.19.0. Il cambio, provato su una copia del codice, è di
**7 file (+22 / −36 righe)** e lascia verdi tutte le suite portabili (Data 533, Presentation 953, UI 106, Core 150,
Plugins 66, ExportEngine 37). È la strada
che propongo per D9.

Le altre due strade funzionano ma costano: i pacchetti di **Zetetic** sono a pagamento per applicazione e per
piattaforma, e chi clona un repo MIT non potrebbe compilarlo; **compilarsi SQLCipher da soli** è fattibile (misurato su
Linux) ma diventa un lavoro ricorrente di rilascio e di sicurezza su almeno due sistemi.

## Cosa c'è oggi, misurato

| | `SQLitePCLRaw.bundle_e_sqlcipher` 2.1.11 (oggi) |
|---|---|
| Nativo | `e_sqlcipher` — `PRAGMA cipher_version` → **`4.5.2 community`** |
| SQLite dentro | **3.39.2** (`sqlite_source_id` 2022-07-21), compilato con gcc 9.4.0 |
| Stato su NuGet | ultima versione 2025-03-07, `deprecation: Legacy` (lo sono tutte, almeno dalla 2.1.7) |
| Futuro | la wiki di SQLitePCL.raw (agosto 2025): *"Beginning with the 3.0 release […] I no longer maintain and distribute encryption-enabled SQLite builds without cost. The `.bundle_e_sqlcipher` and `.bundle_e_sqlite3mc` packages are deprecated."* Non ci saranno altre versioni |
| Grafo | `Microsoft.Data.Sqlite.Core` 10.0.12 chiede `SQLitePCLRaw.core` 2.1.12 |

Quindi il motore che cifra i dati dell'utente è fermo a **quattro anni fa** e non riceverà più correzioni.

## Le strade

| Strada | Apre i DB SQLCipher 4? | Pacchetti | Costo | Sorgente |
|---|---|---|---|---|
| **SQLite3 Multiple Ciphers** | sì (misurato) | `SQLite3MC.PCLRaw.bundle` su nuget.org, dell'autore della libreria | gratis | MIT |
| **SQLCipher di Zetetic**, pacchetti ufficiali | sì (è lo stesso formato) | dal portale clienti di Zetetic, con `SQLitePCLRaw.provider.sqlcipher` | da 999 USD **per applicazione, per piattaforma, per anno** (pagina "buy", letta oggi) → Windows + Linux ≈ 2.000 USD/anno | BSD-3 (community); i binari commerciali hanno licenza propria |
| **SQLCipher community compilato da noi** | sì (misurato con la 4.19.0) | nessuno: i nativi li produciamo e li spediamo noi | il nostro tempo, per sempre | BSD-3 |
| SQLite3MC dal servizio di build SourceGear | sì | feed privato | da 995 USD/anno | MIT |
| SEE (team di SQLite) | **no** — non legge i DB SQLCipher | — | licenza a pagamento | proprietario |

Note sulle fonti: la wiki di SQLitePCL.raw **non nomina** i pacchetti `SQLite3MC.PCLRaw.*` (parla solo del servizio
SourceGear); esistono dal 2025-11-18 (prima `2.2.5-rc.1`), 11 versioni, ~257.000 download del bundle. La pagina dei
prezzi di SourceGear elenca esplicitamente `SourceGear.sqlite3` e `SourceGear.SEE`; che il servizio copra anche SQLite3MC
lo dice la wiki, non la pagina dei prezzi.

## Metodo

Un solo programma (`Probe.cs`, ~280 righe) compilato quattro volte, ognuna contro un nativo diverso, sempre con
`Microsoft.Data.Sqlite.Core` 10.0.12:

| Sigla | Pacchetto / nativo | SQLite | Cifratura |
|---|---|---|---|
| **OGGI** | `bundle_e_sqlcipher` 2.1.11 | 3.39.2 | SQLCipher 4.5.2 |
| **MC** | `SQLite3MC.PCLRaw.bundle` 2.4.0 | **3.53.4** (2026-07-24) | SQLite3 Multiple Ciphers 2.4.0 |
| **ZET** | `SQLitePCLRaw.provider.sqlcipher` 3.0.5 + `libsqlcipher.so` compilata qui dai sorgenti v4.19.0 | **3.53.4** | SQLCipher 4.19.0 community |
| **CHIARO** | `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5 | 3.53.4 | nessuna (lo scenario di SEC-1) |

Il database di prova è **un database Recordum vero**: creato da OGGI con `SqlCipherConnectionFactory.Open` e le
migrazioni reali (schema 2.18, 32 tabelle), più una tabella di contenuto vario (5.000 righe con testo Unicode, blob da
20 KB, pagine di overflow) — 3,0 MB. Di ogni apertura si calcola un'**impronta** (SHA-256 dello schema e di ogni riga di
ogni tabella) e `PRAGMA integrity_check`. "Uguale" sotto significa impronta identica, `0719FD717FBD1C52`, e
`integrity_check: ok`.

Poi, per misurare il costo vero: una copia del repo (`git archive HEAD`), il pacchetto scambiato, le suite girate prima
e dopo.

## Le cinque domande

### 1. Quale sostituto apre un DB SQLCipher 4 creato oggi?

**Tutti e due.**

| Prova | MC | ZET |
|---|---|---|
| Apre il DB con l'istruzione dell'app **invariata** (`PRAGMA key = "x'…'"`) | **no: errore 26** | sì, uguale |
| Apre dichiarando prima il formato: `PRAGMA cipher = 'sqlcipher'; PRAGMA legacy = 4; PRAGMA key = …` | **sì, uguale** | — |
| Apre col formato nell'URI: `file:…?cipher=sqlcipher&legacy=4` + `PRAGMA key` | sì, uguale | — |
| Default di processo con `sqlite3mc_config('default:cipher', …)` | **no: errore 26** (così come l'ho provato) | — |
| Scrive nel DB; OGGI lo rilegge, scrive a sua volta, e il nuovo rilegge | sì, uguale, entrambe le righe presenti | sì |
| Chiave sbagliata | nessun errore all'apertura, **errore 26 alla prima lettura** — identico a oggi | identico (in più scrive tre righe di diagnostica su stderr) |
| `PRAGMA rekey` con chiave grezza (`KeyTransitionService`) | sì; OGGI apre con la chiave nuova e rifiuta la vecchia | — (stesso codice di oggi) |
| File **cifrati da MC** riletti da SQLCipher vero | OGGI (4.5.2): uguale · ZET (4.19.0): uguale | — |

L'ultima riga è quella che conta per la reversibilità: SQLCipher verifica l'HMAC di ogni pagina, e accetta le pagine
scritte da MC. **Finché il formato resta SQLCipher 4, la scelta del provider si può disfare** in qualunque momento.

### 2. Cosa sostituisce `sqlcipher_export` in backup e restore?

MC **non ha** `sqlcipher_export` (`no such function`). Sostituti misurati:

**Cifrato → chiaro** (`BackupService.DumpDbAsync`)

| Metodo | Esito |
|---|---|
| `VACUUM INTO '<file>'`, poi sulla copia `PRAGMA rekey = ''` | **chiaro**, uguale, 2.961.408 byte (identico al file di `sqlcipher_export`) |
| `VACUUM INTO 'file:<file>?key='` | chiaro, uguale, 3.010.560 byte (restano gli 80 byte riservati per pagina: innocui) |
| `VACUUM INTO '<file>'` da solo | **resta cifrato**, con la stessa chiave e lo stesso salt |
| API di backup di SQLite (`BackupDatabase`) | rifiutata: *"backup is not supported with incompatible source and target databases"* |

**Chiaro → cifrato con la chiave locale** (`RestoreService.ReEncryptDbAsync`)

| Metodo | Esito |
|---|---|
| copia del file, poi `PRAGMA cipher = 'sqlcipher'; PRAGMA legacy = 4; PRAGMA rekey = "x'…'"` | cifrato; **OGGI e ZET lo aprono**, uguale |
| `VACUUM INTO 'file:<file>?cipher=sqlcipher&legacy=4&key=x''…'''` | cifrato; OGGI lo apre, uguale |

Il dump in chiaro prodotto **oggi** da `sqlcipher_export` — cioè il contenuto di ogni `.recbak` già esistente — MC lo
ricifra e lo riapre uguale: i backup fatti prima del cambio restano ripristinabili dopo. Lo conferma anche la fixture
`made-on-linux-v2.18.recbak`, verde nella copia col provider nuovo. I tempi sono gli stessi di oggi (190-400 ms sul DB
da 3 MB, contro 270-330).

Con ZET non cambia niente: `sqlcipher_export` c'è.

### 3. Cosa sostituisce `PRAGMA cipher_version` nel guard di SEC-1?

| Domanda al provider | OGGI | MC | ZET | CHIARO |
|---|---|---|---|---|
| `PRAGMA cipher_version` | `4.5.2 community` | **nessuna riga** | `4.19.0 community` | nessuna riga |
| `SELECT sqlite3mc_version()` | errore | `SQLite3 Multiple Ciphers 2.4.0` | errore | errore |
| `sqlite3mc_version` in `pragma_function_list` | 0 | **1** | 0 | **0** |
| `PRAGMA cipher` | nessuna riga | `chacha20` | nessuna riga | nessuna riga |

Con MC il guard diventa "la funzione `sqlite3mc_version` esiste" (o `SELECT sqlite3mc_version()` non fallisce). Il guard
**serve ancora**: ho rifatto lo scenario di SEC-1 col provider in chiaro e le istruzioni nuove
(`PRAGMA cipher…; PRAGMA legacy…; PRAGMA key…`) — nessun errore, e il file nasce **in chiaro** (`SQLite format 3`).
Col solo scambio del pacchetto il guard di oggi va rosso, come deve.

### 4. FTS5 è disponibile?

Sì in tutti: `ENABLE_FTS5` in `PRAGMA compile_options` e una tabella `fts5` creata, riempita e interrogata con `MATCH`
(2 righe attese, 2 trovate) su OGGI, MC e ZET. Anche FTS3/4, R*Tree e JSON.

### 5. La licenza è compatibile col MIT del repo?

| Componente | Licenza | Obblighi |
|---|---|---|
| SQLite3 Multiple Ciphers e i pacchetti `SQLite3MC.PCLRaw.*` | **MIT** | la nota di copyright nella distribuzione |
| SQLite | pubblico dominio | — |
| `SQLitePCLRaw.core` / `.provider.sqlcipher` | Apache-2.0 | come oggi |
| SQLCipher community | BSD-3-Clause | nota di copyright e licenza **anche nei binari** distribuiti |
| Pacchetti commerciali di Zetetic | licenza commerciale | per applicazione e piattaforma; non ridistribuibili con un repo aperto |

Tutte compatibili con un repo MIT tranne l'ultima, che è compatibile sulla carta ma contraddice il principio 6: chi
clona Recordum non potrebbe compilarlo senza comprare una licenza.

## Il costo del cambio, misurato su una copia del codice

| Stato della copia | Data (533) | Presentation (953) | UI (106) |
|---|---|---|---|
| Codice di oggi, bundle di oggi | 533 verdi | — | — |
| **Solo il pacchetto scambiato**, nessuna riga toccata | **13 rossi** | — | — |
| Pacchetto + 7 file (+22 / −36) | **533 verdi** | **953 verdi** | **106 verdi** |

I 13 rossi: 12 sono `no such function: sqlcipher_export` (backup, restore, scheduler, allegati, restore incrociato), 1
è il guard di SEC-1. **Gli altri 520 restano verdi, e questo è il pericolo** (trappola 1).

Il cambio, per intero:

- `Directory.Packages.props`, `Recordum.Data.csproj`: `SQLitePCLRaw.bundle_e_sqlcipher` → `SQLite3MC.PCLRaw.bundle`.
- `SqlCipherConnectionFactory`, `SqlCipherKeyConnectionInterceptor` (due punti): `PRAGMA cipher = 'sqlcipher';
  PRAGMA legacy = 4;` davanti a `PRAGMA key`.
- `BackupService.DumpDbAsync`: `VACUUM INTO` e poi `PRAGMA rekey = ''` sulla copia, al posto di
  `ATTACH … KEY ''` + `sqlcipher_export` + `DETACH`.
- `RestoreService.ReEncryptDbAsync`: copia del file e `PRAGMA rekey` con la chiave locale.
- `SqlCipherProviderGuardTests`: la domanda al provider (punto 3).

`KeyTransitionService` e `DbUnlockService` non cambiano: passano dalla factory. `SQLitePCLRaw.core` sale da 2.1.12 a
3.0.2 sotto `Microsoft.Data.Sqlite` 10.0.12 senza conseguenze (le note della 3.0 dicono che il core non è cambiato; le
suite lo confermano).

## Prestazioni

Stesso programma, stessa macchina, DB cifrato con chiave grezza: 200.000 insert in una transazione, una scansione
completa, 20.000 letture per chiave primaria. Tre giri ciascuno, in millisecondi.

| | insert | scansione | 20.000 letture |
|---|---|---|---|
| OGGI (SQLCipher 4.5.2) | 1.804 – 1.828 | 391 – 392 | 2.748 – 2.770 |
| **MC, formato SQLCipher 4** | 1.616 – 1.648 | **210** | 2.626 – 2.663 |
| ZET (SQLCipher 4.19.0, OpenSSL 3.5) | 1.490 – 1.534 | 156 – 158 | **1.298 – 1.305** |
| senza cifratura (riferimento) | 1.473 | 74 | 666 |

MC non è più lento di oggi in nessuna delle tre, ed è quasi il doppio più veloce in scansione; SQLCipher 4.19 con
OpenSSL è il più veloce, soprattutto nelle letture singole. È un confronto
indicativo, non un benchmark: i budget veri (Home sotto i 500 ms, avvio sotto i 3 s) si misurano con
`Category=Performance` al momento del cambio.

## Compilarsi i nativi da soli è sostenibile?

Misurato su Linux con SQLCipher 4.19.0: `./configure` + `make sqlite3.c` **29 s**, compilazione dell'amalgamation
**2 min 04 s**, libreria da 1,7 MB. Funziona col codice dell'app invariato. Ma:

- dipende da **OpenSSL** (`libcrypto.so.3` di sistema, collegata dinamicamente): su Linux va dichiarata come dipendenza
  del pacchetto o collegata staticamente; su Windows OpenSSL va compilata o portata a mano — **non misurato**;
- servono i nativi per ogni RID che si rilascia (almeno win-x64 e linux-x64), una pipeline che li costruisce e li firma,
  e qualcuno che la rifaccia a ogni correzione di sicurezza di SQLite o di SQLCipher;
- la versione che Fedora impacchetta è la **4.5.2** (SQLite 3.39.2): la stessa età del bundle di oggi, quindi "usare
  quella del sistema" non risolve niente.

Per un progetto di una persona è un costo ricorrente senza contropartita, **finché esiste un pacchetto mantenuto da chi
scrive la libreria**. Resta però il piano di riserva giusto: i nativi di SQLite3MC non dipendono da nessuna libreria
esterna (`ldd`: solo `libc`), quindi compilarsi *quelli* sarebbe più semplice che compilarsi SQLCipher.

## Trappole

1. **Lo scambio del solo pacchetto è un difetto di dati silenzioso.** Compila, 520 test su 533 passano, ma il cifrario
   predefinito di MC è ChaCha20: i database nuovi nascono in un altro formato, e un database esistente risponde
   **errore 26 — lo stesso segnale di "passphrase sbagliata"**. L'utente con la passphrase giusta si sentirebbe dire che
   è sbagliata. Il formato va dichiarato in **ogni** istruzione che imposta una chiave (tre oggi, più il `rekey` del
   restore), e va coperto da un test che apre un DB fatto col bundle vecchio.
2. `VACUUM INTO '<file>'` senza altro **non decifra**: la copia resta cifrata con la chiave della macchina. Se finisse
   così dentro un `.recbak`, il backup si ripristinerebbe solo sulla macchina che l'ha fatto — cioè non sarebbe un
   backup. Serve un test che guardi l'intestazione del DB dentro l'archivio (`SQLite format 3`).
3. L'API di backup di SQLite non passa da cifrato a chiaro: non è un'alternativa.
4. I default di processo via `sqlite3mc_config(...)` non hanno funzionato come li ho provati, e le FAQ di SQLite3MC
   avvertono che da SQLite 3.48 una `SELECT` prima di `PRAGMA key` tocca il file e fallisce: la configurazione si fa con
   `PRAGMA` (o con l'URI), non con funzioni SQL.
5. SEC-1 è possibile anche col provider nuovo (misurato): il guard resta, con la domanda nuova, e va **provato rosso**
   contro il provider in chiaro.
6. **Il formato SQLCipher 4 è "vecchio" per entrambi gli autori.** SQLite3MC lo chiama modo *legacy* e lo sconsiglia per
   i database nuovi; SQLCipher **5.0.0-beta** (2026-09-15) cambia formato di default (AES-256-GCM, pagine da 8192,
   architettura a VFS) e per i DB della 4 chiede `PRAGMA cipher_compatibility = 4` o una migrazione. Restare su
   SQLCipher 4 oggi è la scelta che tiene aperte tutte e due le porte; passare a un cifrario moderno (ChaCha20-Poly1305
   o AEGIS in MC) è una decisione separata, da prendere dopo, e **non l'ho misurata**.
7. Il pacchetto NuGet di MC segue i sorgenti con ritardo: su nuget.org c'è la 2.4.0 (2026-07-28), i sorgenti sono alla
   2.5.1 (2026-08-27). La 2.3.0 è marcata come ritirata perché basata su SQLite 3.52.0, a sua volta ritirato, e la
   2.3.1 è uscita sei giorni dopo: il segno di qualcuno che guarda.
8. **Un solo manutentore**: `utelle` ha 436 commit, il secondo contributore umano 11. Nel 2026 ha rilasciato ogni mese (il repo è del 2018) e
   segue SQLite entro pochi giorni (SQLite 3.53.4 del 24 luglio → MC 2.4.0 il 25 → NuGet il 28). Zetetic è un'azienda.
   Il rischio si copre con la trappola 6 letta al contrario: finché il formato è SQLCipher 4, si torna indietro.
9. I pacchetti `SQLite3MC.PCLRaw.*` sono firmati dal repository (nuget.org), non dall'autore — come quelli di oggi.
10. Resta la regola di SEC-1: un solo bundle SQLitePCLRaw nel grafo (`.Sqlite.Core`, mai `.Sqlite`).

## Cosa non è stato misurato

- **Windows e macOS**: tutto sopra è linux-x64. I nativi per win-x64, win-arm64, osx-x64, osx-arm64 e linux-arm64 sono
  nel pacchetto, ma non li ho eseguiti. Al momento del cambio li misura il job Windows della CI, che gira anche
  il restore incrociato.
- **I pacchetti commerciali di Zetetic**: non li ho comprati. ZET qui è il sorgente community 4.19.0 compilato da me,
  caricato dallo stesso provider che i pacchetti ufficiali usano.
- **La qualità crittografica** delle due implementazioni: la sonda misura che sono *interoperabili* (ognuna legge e
  convalida le pagine dell'altra), non che una sia più robusta. Non ho cercato audit indipendenti di nessuna delle due.
- **Un archivio grande** (il criterio di rilascio parla di 2 GiB): il DB di prova è di 3 MB.
- Il passaggio a un cifrario diverso da SQLCipher 4 (trappola 6).

## Per la decisione D9

| Opzione | Pro | Contro |
|---|---|---|
| **A. SQLite3MC dal NuGet dell'autore, formato SQLCipher 4 invariato** *(proposta)* | gratis, MIT, SQLite 3.53.4, nessuna conversione dei dati, reversibile, 7 file | un manutentore; pacchetto giovane (10 mesi); niente `sqlcipher_export` |
| B. Pacchetti commerciali Zetetic | gli autori di SQLCipher, supporto, zero modifiche al codice | ~2.000 USD/anno per due piattaforme; chi clona il repo non compila |
| C. SQLCipher community compilato da noi | zero modifiche al codice, nessun terzo | pipeline di nativi e OpenSSL su ogni sistema, a nostro carico per sempre |
| D. Restare | niente da fare | SQLite del 2022 senza più correzioni: contraddice il criterio "un provider di cifratura mantenuto" |

Se la scelta è **A**, gli obblighi di test del cambio (ognuno provato rosso):

- un DB creato col **bundle di oggi**, committato come fixture, si apre col provider nuovo con la stessa chiave e lo
  stesso contenuto (trappola 1) — è la prova che nessun utente perde i dati all'aggiornamento;
- il DB dentro un `.recbak` appena creato ha l'intestazione in chiaro (trappola 2);
- il guard di SEC-1 con la domanda nuova, rosso contro `bundle_e_sqlite3` (trappola 5);
- `made-on-linux-v2.18.recbak` resta verde su Linux **e** su Windows;
- i test `Category=Performance` in locale, prima e dopo.

Il cambio è piccolo e sta bene in un'onda a sé, **prima dei dati veri** (cioè prima del dogfooding): dopo, ogni
database dell'autore sarebbe un database da non rompere.

## Fonti

- Wiki di SQLitePCL.raw, *SQLite encryption options for use with SQLitePCLRaw* e *Expectation setting for e_sqlcipher*
  (ultima modifica 2026-01-21); note di rilascio di SQLitePCLRaw 3.0 (`v3.md`).
- nuget.org: registrazioni di `SQLitePCLRaw.bundle_e_sqlcipher`, `SQLite3MC.PCLRaw.bundle`/`.lib`/`.provider`,
  `SQLitePCLRaw.provider.sqlcipher`, `Microsoft.Data.Sqlite.Core` 10.0.12.
- SQLite3 Multiple Ciphers: documentazione (cifrario `sqlcipher`, PRAGMA, FAQ), `utelle/SQLite3MultipleCiphers` e
  `utelle/SQLite3MultipleCiphers-NuGet` su GitHub.
- Zetetic: pagina dei prezzi di SQLCipher; note di rilascio 4.19.0 (2026-09-08) e 5.0.0-beta (2026-09-15).
- SourceGear: *SQLite build service* (prezzi).
