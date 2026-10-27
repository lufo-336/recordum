# Roadmap verso la v1.0

> Una pagina: dove siamo e cosa viene dopo. A fine sessione si aggiornano **"Dove siamo"** e gli stati del percorso;
> non si aggiunge cronaca a `CLAUDE.md` (la cronaca sta in `docs/MILESTONES.md`). Il *perché* del prodotto è in
> `docs/PRODUCT.md`; i dettagli di ogni passo nei piani e nelle spec in fondo.

## Dove siamo — 2026-10-27

- **Branch di lavoro**: `f2e-linux-launch`, in pila `avalonia-linux-slice` → `f2-perf1-home` → `f2a-registration` →
  `f2b-creation` → `f2c-data-safety` → `f2d1-tags-photos` → `f2d2-editor` → `f2e-linux-launch`, lineare: un solo
  fast-forward di `f2e-linux-launch` porta tutto in `main`; **nessuno è in `main`** (`main` = `5bf4fdd`, 2026-09-30). I test di prestazioni non
  girano in CI (oscillano sui runner condivisi).
- **Interfaccia**: WPF è congelata e verrà spenta; la v1.0 è la shell Avalonia (`src/Recordum.UI`). **F1** (Home,
  workspace, sblocco), **F1-PERF1**, **F2a** (registrazione), **F2b** (creare e configurare) sono complete. **F2c Dati al
  sicuro** è a codice completo: chiave del DB nel portachiavi (libsecret) con sblocco senza passphrase, backup
  automatico su Avalonia, finestre Backup e Ripristina, finestra Impostazioni con "Dati e backup", `.recbak` incrociati
  Windows↔Linux, file del database 0600. Review dell'intero branch fatta, con un'onda di correzioni (R11: un portachiavi
  momentaneamente irraggiungibile non spegne più l'auto-backup). Dopo la review: "Sfoglia…" per scegliere la cartella
  dei backup automatici (`IFolderPicker`, la cartella di default come suggerimento) e "Salva" che dice l'esito
  (chiude F2c-7); "Apri cartella" nello storico (chiude F2c-1, e rende funzionante "Elimina", che su Avalonia non
  faceva niente); il restore ferma ogni utente del database durante lo scambio dei file e lascia il DB a 0600 (chiude
  F2c-2 e F2c-3); un "Dimentica" non è più annullato da una scrittura tardiva dello sblocco (chiude F2c-4); la finestra di sblocco dice perché la passphrase non si può ricordare (chiude F2c-5); backup e restore non mostrano più testi inglesi grezzi (chiude F2c-6); la pagina dice lo stato vero col portachiavi irraggiungibile e "Prova" dice cosa è
  successo (chiude F2c-8). Cronaca e conteggi in
  `docs/MILESTONES.md`; finding in `docs/phase-4-issues.md`.
- **WPF non si usa più** (decisione dell'autore, 2026-10-17): non si avvia per l'uso quotidiano né come riserva; il
  dogfooding parte e resta su Avalonia. WPF rimane nel repository, congelata, solo come **riferimento di comportamento**
  per i porting di F3–F7 e perché la CI Windows continui a compilare e testare il codice condiviso; si tocca solo dove un
  contratto condiviso lo obbliga. Si cancella a F8, con i criteri della spec Avalonia §7.
- **Aperti**: rail (tranne Impostazioni), Ctrl+5 e i pulsanti di F6–F7 spenti (**F1-RAIL1**); intestazione della Home
  stretta che sovrappone "Nuova" e "Archiviate" (**F2b-5**). **F2c-9** (rekey e sblocco senza cancello) è `triaged`:
  nessun caso reale oggi; "cambia passphrase", quando arriverà, passa dal cancello.
- **Passo 0** (messa in sicurezza): fatto, compreso il key store Linux (F2c). La sonda sul provider di cifratura (0.9)
  propone SQLite3 Multiple Ciphers (`docs/design/2026-09-30-encryption-provider-probe.md`); **D9 decisa il 2026-10-27**:
  SQLite3MC, in un'onda a sé prima dei dati veri.
- **F2 si divide in sotto-onde**: **F2a**, **F2b**, **F2c** (fatte) → **F2d Diario**: F2d-1 (nota veloce con tag e foto D10) e F2d-2 (editor
  della sera) a codice completo; **lancio stabile su Linux** fatto (`f2e-linux-launch`): `scripts/install-linux.sh`
  compila `main` in Release da un `git archive`, lo installa in `~/.local/share/recordum/app` con `recordum.desktop` e
  l'icona (provvisoria), chiede del backup manuale prima di sostituire una build e tiene la precedente in `app.prev`.
  Sullo stesso branch le decisioni dell'autore del 2026-10-27: composer della nota veloce spento durante una modifica
  (F2d1-5), × sulle foto senza riferimento in modifica nel diario (F2d2-5), bozza che ricorda il suo giorno (F2d2-6).
- **Checklist a schermo dell'autore** (GNOME per tutte; Windows per F1; la lista D7 è nel piano F1): F1 (finestra, tab,
  trascinamento, barra di sistema); F2a (i quattro tipi dalla colonna "Oggi" e dal quick-add, "ieri" vicino al reset, timer
  dopo riavvio, separatore decimale, chiaro e scuro, rinomina e cancellazione); F2b (creare da tutti e otto i template e dal
  builder, configurare ogni sezione, finestra stretta, **W2V-16** e **W2V-18**); **F2c su GNOME**: (1) primo avvio con
  "Ricorda su questo dispositivo"; (2) riavvio senza passphrase; (3) Seahorse mostra le due voci "Recordum …" con
  l'etichetta giusta; (4) "Dimentica su questo dispositivo" e riavvio con la passphrase; (5) backup automatico acceso con la sua
  passphrase, e spento (con l'avviso nella pagina) se la si cancella in Seahorse; "Sfoglia…" apre il selettore di
  cartella del sistema e "Salva" dice l'esito; con Seahorse bloccato e il prompt annullato la pagina dice "portachiavi non
  raggiungibile" e il backup automatico resta acceso; nello storico "Apri cartella" apre il file manager ed "Elimina" toglie la riga; (6) "Backup ora" e "Ripristina…" dalle
  Impostazioni; (7) un `.recbak` della VM Windows ripristinato su Linux; (8) `ls -l` della cartella dati: i tre file
  del database sono 0600.
- **F2d-1 (tag e foto nella nota veloce)**: a codice completo su `f2d1-tags-photos` (testa `f8bcf4d5`), in pila sopra
  F1 → PERF1 → F2a → F2b → F2c, **nessuno in `main`**. Schema **2.19**: tag sulle voci (`note_tag`, nel testo con `#` e nella
  riga Tag) e foto **dentro il database cifrato** (ridimensionate e senza metadati, D10); all'avvio le foto già su disco
  entrano nel DB e i file spariscono. Review dell'intero branch fatta, con un'onda di correzioni (voce di sole foto
  modificabile, nomi di tag validi inline, minori M1-M7). Aperti e decisioni: `docs/phase-4-issues.md`, sezione "Onda F2d-1".
- **F2d-2 (editor del diario della sera)**: a codice completo su `f2d2-editor` (in pila sopra `f2d1-tags-photos`, **nessuno in
  `main`**), nessuna migrazione. Col gesto Diario la colonna Diario è un editor solo (`DiaryEditor`, AvaloniaEdit): titoli,
  grassetto e corsivo, elenchi che continuano con Invio, citazioni, `#tag` col menu, immagini nel testo
  (`![nome](photo:<id>)`, "Inserisci immagine…"); sotto, le voci del giorno rese, con Modifica (striscia "Stai modificando…")
  ed Elimina. Salva esplicito più una bozza che si scrive a ogni modifica e torna dopo un crash. Cronologia con chip dei tag e
  "[foto]"; export Markdown come cartella con le immagini, HTML con l'immagine al suo posto. Col gesto Nota veloce resta la
  nota veloce di F2d-1. Review dell'intero branch fatta, con un'onda di correzioni (la modifica ripristinata dalla bozza
  spariva all'apertura; Cronologia ferma dopo una modifica). Aperti: `docs/phase-4-issues.md`, sezione "Onda F2d-2".
- **Checklist a schermo dell'autore per F2d-2** (GNOME; gesto Diario):
  - [ ] diario della sera con un titolo, un elenco (Invio che continua, Invio su voce vuota che chiude), una citazione,
    Ctrl+B e Ctrl+I con e senza selezione;
  - [ ] `#` con la tastiera (frecce, Invio che sceglie senza andare a capo) e con un **vero clic del mouse**;
  - [ ] "Inserisci immagine…": la miniatura appare nel testo fuori dalla riga del cursore; scrivere a lungo: la colonna segue
    il cursore;
  - [ ] Salva, poi rilettura dalla Cronologia: chip dei tag e "[foto]";
  - [ ] Modifica di una voce, Annulla (torna la bozza di prima); Modifica con cambi, poi un'altra voce o un altro giorno: chiede;
  - [ ] chiudere la finestra a metà frase e riaprire: la bozza torna; `kill -9` a metà frase (anche durante una Modifica) e
    riaprire: torna anche la modifica;
  - [ ] ieri dal calendario: le voci di ieri rese con le immagini; una voce nuova scritta lì va su ieri;
  - [ ] chiaro e scuro;
  - [ ] **export** (F2d2-8: su Avalonia arriva con F6): creare la voce con un'immagine sull'app Avalonia della VM Windows ed
    esportare da WPF sulla stessa cartella dati — Markdown aperto in Obsidian con le immagini, HTML con l'immagine al suo
    posto; oppure rimandare questi due punti a F6;
- **Checklist a schermo dell'autore per F2d-1** (GNOME), in quest'ordine:
  - [ ] (Ruling 6) **primo avvio su una COPIA della cartella dati vera** (`--data-dir`, mai l'originale): le foto esistenti
    entrano nel DB, `attachments/` non ha più file, le voci vecchie mostrano le loro foto; **cronometrare il primo avvio**;
    backup e ripristino della copia; `ls -lR` della cartella non mostra nessuna immagine;
  - [ ] nota veloce dal diario del Cibo e da un diario normale;
  - [ ] `#` con suggerimenti (mostrati come `#nome`) e «Crea «#…»»; scegliere un suggerimento con un **vero clic del MOUSE**
    lo inserisce (nei test c'è solo un tap sintetico);
  - [ ] la riga Tag (provare un nome con spazi: rifiutato);
  - [ ] una foto dal telefono esce dritta e ridimensionata; un HEIC è rifiutato con un messaggio che nomina il file; un
    panorama molto grande o una foto da 200 MP dà il messaggio «troppi pixel»;
  - [ ] una voce di sole foto, poi modificarne solo i tag;
  - [ ] modifica con una foto tolta, poi Annulla;
  - [ ] le tre scelte D10 in «Dati e backup»;
  - [ ] l'ora delle voci è l'ora locale;
  - [ ] nessuna immagine leggibile nella cartella dati;
- **Prossimo**: (1) il merge dell'autore, un fast-forward: `git switch main`, poi
  `git merge --ff-only f2e-linux-launch && git push origin main`; (2) **l'onda D9** (SQLite3MC) — **prima** di aprire i dati
  veri con la build installata; (3) la checklist di F2d-1 su una **copia** dei dati veri (`--data-dir`), poi le altre;
  (4) `scripts/install-linux.sh` e il dogfooding. **Finché D9 e la prova sulla copia non sono fatte, la voce Recordum
  del menu apre la cartella dati vera: non avviarla.**


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
| D6 | Builder dei grafici nella v1.0 o nella 1.1 | F7 |
| D8 | Repository pubblico (minuti di CI illimitati) o privato | quando si decide di pubblicare |

**D9 decisa il 2026-10-27**: SQLite3 Multiple Ciphers (`SQLite3MC.PCLRaw.bundle`, MIT) col formato SQLCipher 4
invariato, l'opzione A della sonda (`docs/design/2026-09-30-encryption-provider-probe.md`); si fa in un'onda a sé, con gli
obblighi di test della sonda, **prima dei dati veri**. Decise il 2026-09-25: D1 dogfooding su Windows **e** Linux; D2 niente promemoria prima del dogfooding; D4 merge in `main`
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
