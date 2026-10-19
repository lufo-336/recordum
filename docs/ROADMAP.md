# Roadmap verso la v1.0

> Una pagina: dove siamo e cosa viene dopo. A fine sessione si aggiornano **"Dove siamo"** e gli stati del percorso;
> non si aggiunge cronaca a `CLAUDE.md` (la cronaca sta in `docs/MILESTONES.md`). Il *perché* del prodotto è in
> `docs/PRODUCT.md`; i dettagli di ogni passo nei piani e nelle spec in fondo.

## Dove siamo — 2026-10-16

- **Branch di lavoro**: `f2c-data-safety`, in pila `avalonia-linux-slice` → `f1`/`f2-perf1-home` → `f2a-registration` →
  `f2b-creation` → `f2c-data-safety`; **nessuno è in `main`** (`main` = `5bf4fdd`, 2026-09-30). I test di prestazioni non
  girano in CI (oscillano sui runner condivisi).
- **Interfaccia**: WPF è congelata e verrà spenta; la v1.0 è la shell Avalonia (`src/Recordum.UI`). **F1** (Home,
  workspace, sblocco), **F1-PERF1**, **F2a** (registrazione), **F2b** (creare e configurare) sono complete. **F2c Dati al
  sicuro** è a codice completo: chiave del DB nel portachiavi (libsecret) con sblocco senza passphrase, backup
  automatico su Avalonia, finestre Backup e Ripristina, finestra Impostazioni con "Dati e backup", `.recbak` incrociati
  Windows↔Linux, file del database 0600. Review dell'intero branch fatta, con un'onda di correzioni (R11: un portachiavi
  momentaneamente irraggiungibile non spegne più l'auto-backup). Cronaca e conteggi in
  `docs/MILESTONES.md`; finding in `docs/phase-4-issues.md`.
- **WPF non si usa più** (decisione dell'autore, 2026-10-17): non si avvia per l'uso quotidiano né come riserva; il
  dogfooding parte e resta su Avalonia. WPF rimane nel repository, congelata, solo come **riferimento di comportamento**
  per i porting di F3–F7 e perché la CI Windows continui a compilare e testare il codice condiviso; si tocca solo dove un
  contratto condiviso lo obbliga. Si cancella a F8, con i criteri della spec Avalonia §7.
- **Aperti**: rail (tranne Impostazioni), Ctrl+5 e i pulsanti di F6–F7 spenti (**F1-RAIL1**); intestazione della Home
  stretta che sovrappone "Nuova" e "Archiviate" (**F2b-5**); i residui di F2c (**F2c-1…8**: "Apri cartella", quiesce
  del DB durante un restore, modo del file dopo un restore, "ricorda" scaduta, avviso di timeout, stringhe inglesi, "Salva"
  silenzioso, stato della pagina col portachiavi irraggiungibile).
- **Passo 0** (messa in sicurezza): fatto, compreso il key store Linux (F2c). La sonda sul provider di cifratura (0.9)
  propone SQLite3 Multiple Ciphers (`docs/design/2026-09-30-encryption-provider-probe.md`); **D9 aspetta l'autore**.
- **F2 si divide in sotto-onde**: **F2a**, **F2b**, **F2c** (fatte) → **F2d Diario** (nota veloce con foto D10, editor
  minimo); chiude F2 il lancio stabile su Linux (publish + `.desktop`).
- **Checklist a schermo dell'autore** (GNOME per tutte; Windows per F1; la lista D7 è nel piano F1): F1 (finestra, tab,
  trascinamento, barra di sistema); F2a (i quattro tipi dalla colonna "Oggi" e dal quick-add, "ieri" vicino al reset, timer
  dopo riavvio, separatore decimale, chiaro e scuro, rinomina e cancellazione); F2b (creare da tutti e otto i template e dal
  builder, configurare ogni sezione, finestra stretta, **W2V-16** e **W2V-18**); **F2c su GNOME**: (1) primo avvio con
  "Ricorda su questo dispositivo"; (2) riavvio senza passphrase; (3) Seahorse mostra le due voci "Recordum …" con
  l'etichetta giusta; (4) "Dimentica su questo dispositivo" e riavvio con la passphrase; (5) backup automatico acceso con la sua
  passphrase, e spento (con l'avviso nella pagina) se la si cancella in Seahorse; (6) "Backup ora" e "Ripristina…" dalle
  Impostazioni; (7) un `.recbak` della VM Windows ripristinato su Linux; (8) `ls -l` della cartella dati: i tre file
  del database sono 0600.
- **Prossimo**: le checklist dell'autore e i merge in ordine
  (`avalonia-linux-slice`, `f2-perf1-home`, `f2a-registration`, `f2b-creation`, `f2c-data-safety`), poi F2d.


## Il percorso

1. **Passo 0 — messa in sicurezza** · ◐ · esce quando la tabella di stato del piano è tutta verde (il key store Linux di 0.7 è in F2c).
2. **F1 — Home su Avalonia** · ◐ codice completo, review di branch fatta, CI verde · esce con la checklist a
   schermo su Windows e su GNOME (D7: su Linux la barra di sistema) e il merge in `main`.
3. **F2 — "si usa ogni giorno"** · ◐ F2a, F2b e F2c complete (`f2c-data-safety`) · il dogfooding parte quando il **manifesto delle abitudini vere dell'autore**
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
