# Roadmap verso la v1.0

> Una pagina: dove siamo e cosa viene dopo. A fine sessione si aggiornano **"Dove siamo"** e gli stati del percorso;
> non si aggiunge cronaca a `CLAUDE.md` (la cronaca sta in `docs/MILESTONES.md`). Il *perché* del prodotto è in
> `docs/PRODUCT.md`; i dettagli di ogni passo nei piani e nelle spec in fondo.

## Dove siamo — 2026-10-12

- **Branch di lavoro**: `avalonia-linux-slice`, tutto pushato, a `c744963`. `main` = `5bf4fdd` (2026-09-30: R9, F0, F1
  fino al Task 5, Passo 0). **CI verde su Linux e su Windows** sul branch (run 37073454341) e su `main`. I test di
  prestazioni non girano in CI (oscillano sui runner condivisi).
- **Interfaccia**: WPF è congelata e verrà spenta; la v1.0 è la shell Avalonia (`src/Recordum.UI`). **F1 è a codice
  completo e ha passato la review dell'intero branch**: la shell si avvia davvero (sblocco del database, Home con righe
  vive, trascinamento, rail e tab del workspace, finestra principale). La review finale ha trovato e fatto correggere
  la CI rossa da `b1fe1dd` (stato condiviso fra test), il glifo di "riduci a icona" che non disegnava, Esc nella
  rinomina che salvava il nome scartato, e due pulsanti che non facevano nulla (spenti fino a F5). Suite su Linux a
  `c744963`: Core 150, Data 541 (+7 test DPAPI `WindowsOnly` che qui falliscono), Plugins 66, ExportEngine 37,
  Presentation 953, UI 190; build 0 errori.
- **Per chiudere F1 mancano due cose, entrambe dell'autore**: la checklist a schermo su Windows e su GNOME (l'handoff è
  in `docs/dev-environment-sync.md`, la lista D7 nel piano F1, Task 10 Step 9) e il merge in `main`.
- **F1-PERF1 corretto** sul branch `f2-perf1-home` (parte da `avalonia-linux-slice`): la Home è una lista piatta
  virtualizzata con le cornici dei gruppi disegnate a fette; 300 righe da ≈ 1,7 s a ≈ 115 ms a caldo in headless.
  Manca la verifica a schermo su GNOME (su Windows, uno sguardo durante la checklist D7 di F1); il merge va dopo
  quello di F1.
- **Aperti, trovati in F1** (`docs/phase-4-issues.md`, sezione "Onda F1"):
  `recordum.db`/`-wal`/`-shm` nascono 0644 (**F1-SEC1**, nel `/security-review`); rail, Ctrl+4/Ctrl+5, "Nuovo >
  Abitudine" e il pulsante dello stato vuoto sono spenti finché F2, F5, F6 e F7 non li riaccendono (**F1-RAIL1**).
- **Passo 0** (messa in sicurezza): fatto tranne il key store Linux di 0.7, che si fa in F2. La sonda sul provider di
  cifratura (0.9) propone SQLite3 Multiple Ciphers (`docs/design/2026-09-30-encryption-provider-probe.md`); **D9 aspetta
  l'autore**.
- Il branch `wip/f1-task6` è superato (il Task 6 è in `aaa676b`) e si può cancellare.
- **F2a (Registrazione su Avalonia) è a codice completo** sul branch `f2a-registration` (parte da `f2-perf1-home`, che
  parte da `avalonia-linux-slice`; **nessuno dei tre è in `main`**; da pushare e da far girare in CI: review dell'intero
  branch e CI le fa il controllore). Porta: la schermata dell'abitudine (intestazione, colonna "Oggi" per i quattro tipi con
  fascia, calendario Mese/Settimana, Cronologia con cancellazione confermata), il quick-add dei cinque tipi con celle
  numeriche che rifiutano il separatore sbagliato, la barra dei timer con cronometri vivi, e il percorso "ieri" provato
  sul DB cifrato vero a cavallo del reset (R9). Difetti latenti corretti: la barra dei timer non si aggiornava mai, il
  cronometro di sessione non scorreva, l'ora delle registrazioni era mostrata in UTC, la cancellazione non chiedeva
  conferma (vale anche in WPF), la rinomina dalla Home non arrivava all'intestazione (`HabitUpdated` ora lo pubblica
  `HabitNodeService`). Misura: tab con un anno di registrazioni (730) ≈ 0,7 s a caldo, 4 s a freddo in headless
  (`Category=Performance`). Finding in `docs/phase-4-issues.md`, sezione "Onda F2a".
- **Checklist a schermo dell'autore (GNOME)**: registrare i quattro tipi (azione, contatore, astinenza, stato) dalla
  colonna "Oggi" e dal quick-add; "ieri" vicino al reset (R9: reset alle 04:00, dopo mezzanotte, giorno scelto sul
  calendario = giorno in cui compare la registrazione); la barra dei timer dopo aver chiuso e riaperto l'app con un
  cronometro acceso; le celle numeriche con l'altro separatore ("2.5" in italiano deve essere rifiutato, mai 25); chiaro e
  scuro; rinomina dalla Home con la schermata aperta; cancellare una registrazione dalla Cronologia (chiede conferma); i W2V
  della sezione §3 del pass visivo (tap counter, somma, cronometro, export).
- **F2 si divide in sotto-onde** (2026-10-06): **F2a** (fatta, vedi sopra) → **F2b Creare su Linux** (picker dei template
  e builder) → **F2c Dati al sicuro** (portachiavi D3, backup automatico e manuale, restore incrociato) → **F2d Diario**
  (nota veloce con foto D10, editor minimo); chiude F2 il lancio stabile su Linux (publish + `.desktop`).
- **Prossimo**: **F2b** (piano da scrivere). All'autore: checklist di F1 e di F2a su GNOME, poi i merge in ordine
  (`avalonia-linux-slice`, `f2-perf1-home`, `f2a-registration`).

## Il percorso

1. **Passo 0 — messa in sicurezza** · ◐ · esce quando la tabella di stato del piano è tutta verde (manca il key store
   Linux di 0.7, che si fa in F2).
2. **F1 — Home su Avalonia** · ◐ codice completo, review di branch fatta, CI verde · esce con la checklist a
   schermo su Windows e su GNOME (D7: su Linux la barra di sistema) e il merge in `main`.
3. **F2 — "si usa ogni giorno"** · ◐ F2a a codice completo (`f2a-registration`) · il dogfooding parte quando il **manifesto delle abitudini vere dell'autore**
   (`docs/DOGFOODING.md`) è coperto **su Linux**, la casa dei dati (D11); Windows segue con dati di prova. Contiene: fascia Oggi e registrazione dei cinque tipi, `TimerStatusBar`, nota veloce, backup automatico e
   manuale con restore da un altro computer, su Linux anche template, builder e sblocco dal portachiavi (D3), la verifica
   di R9 a schermo.
4. **★ Dogfooding** · ☐ · su Windows e su Linux, senza promemoria, con i dati di sviluppo separati (`--data-dir`). Dura
   almeno quattro settimane; dopo due si rivede lo scope con i dati dell'uso vero.
5. **F3 Dettaglio → F4 Diario → F5 Configura e builder → F6 Impostazioni ed export → F7 Grafici** · ☐ · ogni onda chiude
   con review di branch, CI verde, checklist del linguaggio visivo in chiaro e in scuro (PNG della CI), verifica a schermo
   su Windows e GNOME, merge in `main`.
6. **F8 — WPF si spegne** · ☐ · criteri della spec Avalonia (§7).
7. **Verso il pubblico** · ☐ · promemoria (E14), verità sul core e tempi d'avvio (Onda 2.9), le funzioni di potenziamento
   riordinate dopo il dogfooding (Onda 5), poi packaging per Windows e Linux, onboarding, revisione di sicurezza, beta
   (Onda 4).

## Decisioni aperte

| # | Decisione | Serve entro |
|---|---|---|
| D9 | Provider di cifratura al posto del bundle SQLCipher deprecato. Sonda fatta: la proposta è SQLite3 Multiple Ciphers col formato SQLCipher 4 invariato (`docs/design/2026-09-30-encryption-provider-probe.md`) | prima dei dati veri, cioè prima del dogfooding |
| D6 | Builder dei grafici nella v1.0 o nella 1.1 | F7 |
| D8 | Repository pubblico (minuti di CI illimitati) o privato | quando si decide di pubblicare |

Decise il 2026-09-25: D1 dogfooding su Windows **e** Linux; D2 niente promemoria prima del dogfooding; D4 merge in `main`
a ogni onda chiusa; D5 piani fatti di decisioni, interfacce e test. Decise il 2026-09-30: **D7** su Linux la barra del
titolo di sistema (la nostra resta dove il sistema la supporta, cioè Windows); **D3** su Linux lo sblocco passa dal
portachiavi di sistema (Secret Service: GNOME Keyring, KWallet), con la passphrase come ripiego — lo stesso portachiavi
custodisce la chiave dell'auto-backup; si realizza in F2. **D10** le foto all'import si ridimensionano e si puliscono dai
metadati per impostazione predefinita, con tre opzioni per l'utente (anche "solo pulisci" e "tieni gli originali"): il
predefinito serve già in F2, perché il diario del cibo con le foto si usa dal primo giorno. **D11** durante il dogfooding
la casa dei dati veri è **Linux** (la macchina della sera); Windows serve per sviluppo e prove con `--data-dir`.
Il manifesto delle abitudini sta in `docs/DOGFOODING.md`.

## Criteri di rilascio

Si rilascia quando è vero tutto questo, verificato e non presunto:

- **Funziona**: grafici del Dettaglio sensati per i cinque tipi; export leggibili; template che si spiegano da soli;
  builder con anteprima fedele; impostazioni complete; ogni template ricostruibile a mano.
- **Regge**: backup e restore robusti anche oltre i 2 GiB e con file rovinati; aggiornamento da una versione all'altra
  senza perdere dati; Home sotto i 500 ms e avvio sotto i 3 s su un archivio realistico; CI verde su Linux e Windows.
- **È sicura**: revisione di sicurezza senza problemi gravi aperti; un provider di cifratura mantenuto; su Linux nessun
  file in chiaro fuori dalla cartella dati.
- **Si installa**: uno sconosciuto la installa e la usa senza aiuto su Windows e su Linux.
- **È usata**: l'autore l'ha usata ogni giorno per almeno quattro settimane, e le questioni di modello aperte sono state
  decise dall'uso o rimandate esplicitamente.

La tabella completa (E1-E21, riletti per Avalonia) è nel §7 della revisione.

## Regole del percorso

- Fino al dogfooding conta solo ciò che serve all'uso quotidiano.
- Nessuna funzione nuova finché le interfacce sono due; si correggono sempre i difetti di dati e di sicurezza.
- Ogni onda chiusa entra in `main` (il merge lo esegue l'autore: da una sessione Claude è bloccato).
- I piani sono fatti di decisioni, interfacce e test, non di codice da ricopiare.

## Riferimenti

- Revisione del 2026-09-25 — problemi, percorso, criteri riletti, decisioni D1-D10:
  `docs/superpowers/specs/2026-09-25-avalonia-v1-plan-review.md`
- Piano del Passo 0: `docs/superpowers/plans/2026-09-25-passo-0-messa-in-sicurezza.md`
- Piano di F1: `docs/superpowers/plans/2026-09-20-wave-f1-home.md`
- Spec Avalonia, onde F0-F8: `docs/superpowers/specs/2026-09-10-avalonia-as-the-platform-design.md`
- Mappa v1.0 originale, onde 0-5 ed E1-E21 (storica): `docs/superpowers/specs/2026-07-03-v1-redefinition-plan-design.md`
- Tracker dei findings: `docs/phase-4-issues.md` · Cronaca: `docs/MILESTONES.md`
