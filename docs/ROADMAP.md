# Roadmap verso la v1.0

> Una pagina: dove siamo e cosa viene dopo. A fine sessione si aggiornano **"Dove siamo"** e gli stati del percorso;
> non si aggiunge cronaca a `CLAUDE.md` (la cronaca sta in `docs/MILESTONES.md`). Il *perché* del prodotto è in
> `docs/PRODUCT.md`; i dettagli di ogni passo nei piani e nelle spec in fondo.

## Dove siamo — 2026-09-30

- **Branch di lavoro**: `avalonia-linux-slice`, tutto pushato. `main` = `911ed24` (R9 + SEC-1). CI GitHub Actions verde
  su Linux e su Windows.
- **Interfaccia**: WPF è congelata e verrà spenta; la v1.0 è la shell Avalonia (`src/Recordum.UI`), oggi a metà
  dell'onda F1 (Task 1-5 di 13).
- **Passo 0** (messa in sicurezza): 0.1-0.8 e 0.10 fatti — documenti riorganizzati: `PRODUCT.md`, questa pagina, il
  nuovo `CLAUDE.md`, l'archivio delle spec chiuse (`docs/superpowers/archive/`), l'indice in testa al tracker. Resta la
  sonda sul provider di cifratura (0.9).
- **Prossimo**: 0.9, poi il Task 6 di F1. **Per riprendere il Task 6**: `git cherry-pick --no-commit wip/f1-task6`
  (il lavoro a metà — 9 file nuovi in `Recordum.UI` — si applica pulito sullo stato attuale; il merge fast-forward
  indicato prima non funziona più, perché il branch è andato avanti col Passo 0), poi si ri-affida il Task 6 dicendo di
  partire da quei file. Manca ancora `using Avalonia;` in `RowCardTintTests`.

## Il percorso

1. **Passo 0 — messa in sicurezza** · ◐ · esce quando la tabella di stato del piano è tutta verde.
2. **F1 — Home su Avalonia** · ◐ Task 1-5 · esce col Task 13 e con la checklist del chrome vista su Windows e su GNOME.
   Il Task 10 segue D7 (su Linux la barra di sistema); il Task 11 collega all'avvio lock, riconciliazione del salt, `RecordumOptions.Resolve` e
   il log (Passo 0).
3. **F2 — "si usa ogni giorno"** · ☐ · il dogfooding parte quando il **manifesto delle abitudini vere dell'autore**
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
| D9 | Provider di cifratura al posto del bundle SQLCipher deprecato | dopo la sonda 0.9, prima dei dati veri |
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
