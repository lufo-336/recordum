# Roadmap verso la v1.0

> Una pagina: dove siamo e cosa viene dopo. A fine sessione si aggiornano **"Dove siamo"** e gli stati del percorso.
> Qui non entra cronaca (sta in `docs/MILESTONES.md`) e non entrano checklist (stanno in `docs/CHECKLIST.md`).
> Il *perché* del prodotto è in `docs/PRODUCT.md`; i finding nel tracker `docs/phase-4-issues.md`.

## Dove siamo — 2026-11-24

- **In `main`** (`40c31c12`, fast-forward del 2026-11-17, CI verde): la shell Avalonia con Home, registrazione dei
  cinque tipi, creare e configurare, dati al sicuro (portachiavi, backup, ripristino), diario con tag e foto, SQLite3MC,
  la direzione visiva fino all'onda 2b, l'**onda 3a** (creatore e selettore dei modelli) e l'**onda "Giornata"**.
- **Branch di lavoro**: `unisci` (a codice completo, attende prova a mano e PR) e **`telefono-onda4`** (da `unisci`,
  decisione dell'autore del 2026-11-24): spec e piano dell'onda 4 scritti, esecuzione da avviare.
- **Il percorso è stato ridisegnato il 2026-11-10**
  (`docs/superpowers/specs/2026-11-10-v1-ux-review-and-path-design.md`, con le decisioni dell'autore al §6):
  la giornata prima delle schermate; **un APK Android nella v1.0** (l'autore userà l'app soprattutto dal telefono, il
  computer è lo "studio" per grafici e diario lungo); due dispositivi che **uniscono** i dati attraverso una cartella.
- **Sonda Android fatta** (`docs/design/2026-11-10-android-probe.md`): sul Galaxy S21 il database cifrato si apre e la
  Home si disegna; il lavoro è la cornice da telefono, i servizi di piattaforma, il tempo d'avvio (≈ 6 s oggi).
- **WPF**: l'autore ha deciso il 2026-11-24 di **spegnerla subito** (F8 anticipato): è il Task 1 del piano dell'onda 4.
- **Dati veri**: finché le sezioni 1 e 2 di `docs/CHECKLIST.md` non sono fatte, la voce Recordum del menu apre la
  cartella dati vera — **non avviarla**.
- **Onda "Giornata" in `main`** (2026-11-17): piano `docs/superpowers/plans/2026-11-11-giornata-wave.md`, 15 task
  più l'8b, scritta in cooperazione con Antigravity (cronaca in `docs/MILESTONES.md`). La revisione dei PNG con l'autore
  (2026-11-16/17) ha chiuso GIO-2, 4, 6, 9, 12…22, 24, 25 (tracker, sezione "Giornata"); GIO-23 (scale continue:
  schema **2.20**, scaglioni con nome libero e tag ricavato in `scale_anchors`, valore vero in `value_decimal`), GIO-1,
  5, 8, 10 in main il 2026-11-18; GIO-11 (selezione), B7 (durate in parole) e B8 (mappa dell'anno coi mesi, "Insieme"
  a settimane con "Mostra i giorni") in main il 2026-11-19 (PR #1, `d27df7f0`). Di GIO-11 restano le sottoscrizioni
  della Home, da fare con la testa Android.
- **Prossimo — si riprende da qui**: (1) "unisci", parte computer, **a codice completo** sul branch `unisci`
  (spec `docs/superpowers/specs/2026-11-22-unisci-design.md`, piano `docs/superpowers/plans/2026-11-22-unisci.md`, poi
  le decisioni del 2026-11-24 nel piano `docs/superpowers/plans/2026-11-24-unisci-ruling-21-24.md`: busta a blocchi,
  eco una volta, origine delle foto, "voci da sistemare" con l'elenco). Restano la prova a mano con l'autore (due
  `--data-dir` e una cartella condivisa) e la PR verso `main`; il telefono ha un piano a parte dopo la testa Android;
  (2) **il telefono, passi 4–5**: spec `docs/superpowers/specs/2026-11-24-telefono-onde-4-5-design.md` approvata
  dall'autore il 2026-11-24 (mockup `docs/design/2026-11-24-telefono-onde-4-5/`: accesso con impronta e regole in
  Impostazioni › Sicurezza, finestre → pagine e fogli, i fogli per registrare; cornice con le pagine di Avalonia 12;
  pacchetto `io.github.lufo336.recordum`); piano dell'onda 4 `docs/superpowers/plans/2026-11-24-telefono-onda-4.md`
  (14 task), da eseguire su `telefono-onda4`. Prima: le sonde sul Galaxy (pagine di Avalonia, Keystore con impronta,
  notifica del timer, scatto nella Galleria), che chiedono il telefono collegato; app usa e getta in
  `~/dev/_spike-telefono`;
  (3) il passo 3, dati protetti e dogfooding sul computer, **in parallelo** (lo fa l'autore, decisione del 2026-11-24).
  Resta da decidere dall'autore: obiettivi su una metrica di scala non convertiti dalla 2.20 (nessuna
  strada di oggi ne crea uno).

## Il percorso

| # | Passo | Stato | Esce quando |
|---|---|---|---|
| 1 | **Chiudere 3a** | ✓ | PNG guardati dall'autore, merge |
| 2 | **Onda "Giornata"** — la Home col giorno corrente e la testa su una riga; la tastiera; voce e nota; le parole (`docs/GLOSSARY.md` con la guardia); i difetti B1–B6 della revisione; la fixture "manifesto" coi PNG della Home piena; le righe della Home a scelta in Impostazioni › Aspetto, tre versioni (Compatta, Segni, Larga) su computer e telefono | ✓ | PNG della Home piena in chiaro e scuro, nelle tre versioni; CI verde |
| 3 | **Dati protetti → ★ dogfooding sul computer** (prova generale; in parallelo al telefono) | ☐ | sezioni 1 e 2 della checklist; `install-linux.sh` |
| 4 | **Testa Android** — `Recordum.UI` diventa libreria con due teste (`Recordum.Desktop`, `Recordum.Android`); avvio fuori dal thread UI con la schermata d'attesa; la cornice (barra in basso, pagine, fogli dal basso); portachiavi di Android e impronta | spec e piano ✓ | l'APK apre la Home dei dati veri; tempo dall'icona alla Home misurato |
| 5 | **Telefono: registrare tutto** — i cinque tipi, il diario in un campo di testo con la barra, la fotocamera, il timer in background → **★ dogfooding vero**, sul telefono (i dati ci passano con un ripristino) | ☐ | il manifesto di `DOGFOODING.md` coperto dal telefono |
| 6 | **"Unisci"** — sincronizzazione attraverso una cartella: ogni dispositivo scrive il proprio file cifrato e unisce quello dell'altro; lapidi per le cancellazioni. Parte computer fatta sul branch `unisci` (in attesa di review finale e merge); parte telefono dopo la testa Android | ☐ | spec approvata; due dispositivi che scrivono e non perdono niente, provato con test |
| 7 | **In parallelo al dogfooding**: 3b (Impostazioni a categorie, backup, diario a tutta finestra — disegnate una volta per computer e telefono) · export · grafici del Dettaglio · Statistiche e builder dei grafici (computer; sul telefono si guardano) · promemoria (telefono) · **tolleranza della serie e timer di riposo** (GIO-21: oggi si salvano e niente li legge, quindi sono nascosti; decidere cosa fa la tolleranza, farli funzionare, spiegarli in una riga, unità a scelta) · pausa · "Cerca" unico · cambia passphrase · F8 (WPF si spegne) | ☐ | ogni onda: review di branch, CI verde, PNG |
| 8 | **Verso il pubblico** — verità sul core e tempi d'avvio; APK firmato e pacchetti per Windows e Linux; onboarding; revisione di sicurezza; beta | ☐ | i criteri qui sotto |

Dopo due settimane di dogfooding si rivede lo scope con i dati dell'uso vero.
**Alla 1.1**: riepilogo settimanale, modalità Calendario, "Cosa dice il gruppo", palette dei comandi, cattura rapida
globale, import CSV, risparmio e "voglia resistita", routine ordinata.

## Chi esegue

- **Haiku**: solo i task che il piano marca *meccanici* (stringhe, fixture e PNG, porting ripetitivi, documenti). Mai Data,
  migrazioni, sicurezza, `DayBoundary`, firme condivise.
- **Sonnet**: i task normali, e la review di ogni task (in un agente diverso da chi ha scritto).
- **Opus**: la review dell'intero branch e le correzioni non banali; le banali tornano a chi ha scritto.
- Ogni piano porta per task: livello, file, test rossi-poi-verdi, e quando fermarsi e salire di livello.

## Decisioni

| # | Decisione | Stato |
|---|---|---|
| D8 | Repository pubblico o privato | aperta — quando si decide di pubblicare |
| — | Promemoria nella v1.0 del computer | aperta — dopo due settimane di dogfooding (sul telefono servono) |
| D6 | Builder dei grafici e Statistiche | **nella v1.0** (2026-11-10) |
| D1, D11 | Dove vivono i dati | superate il 2026-11-10: il telefono è la casa dei dati, il computer unisce |

Ancora valide: D2 (niente promemoria prima del dogfooding), D3 (su Linux lo sblocco dal portachiavi), D4 (merge in
`main` a ogni onda), D5 (piani di decisioni, interfacce e test), D7 (su Linux la barra del titolo di sistema), D9
(SQLite3MC, formato SQLCipher 4), D10 (foto ridimensionate e senza metadati). Il testo: revisione del 2026-09-25.

## Criteri di rilascio

Si rilascia quando è vero tutto questo, verificato e non presunto:

- **Funziona**: grafici del Dettaglio sensati per i cinque tipi; export leggibili; template che si spiegano da soli;
  builder con anteprima fedele; impostazioni complete; ogni template ricostruibile a mano.
- **Regge**: backup e restore robusti anche oltre i 2 GiB e con file rovinati; aggiornamento da una versione all'altra
  senza perdere dati; due dispositivi che uniscono senza perdere né resuscitare niente; Home sotto i 500 ms e avvio sotto
  i 3 s su un archivio realistico, **anche sul telefono**; CI verde.
- **È sicura**: revisione di sicurezza senza problemi gravi aperti (compresi portachiavi di Android e "unisci"); nessun
  file in chiaro fuori dalla cartella dati.
- **Si installa**: uno sconosciuto la installa e la usa senza aiuto su Windows, Linux e Android.
- **È finita**: nella build pubblica nessuna superficie dice "in arrivo".
- **È usata**: l'autore l'ha usata ogni giorno per almeno quattro settimane, e le questioni di modello aperte sono state
  decise dall'uso o rimandate esplicitamente.

## Regole del percorso

- Fino al dogfooding conta solo ciò che serve all'uso quotidiano.
- WPF non riceve funzioni; si correggono sempre i difetti di dati e di sicurezza.
- Ciò che si disegna da qui in poi si disegna per computer e telefono insieme.
- Ogni onda chiusa entra in `main` (il merge lo esegue l'autore).

## Riferimenti

- Revisione e percorso del 2026-11-10: `docs/superpowers/specs/2026-11-10-v1-ux-review-and-path-design.md`
- Mockup: `docs/design/2026-11-10-giornata-mockups/`, `docs/design/2026-11-10-telefono-mockups/`
- Direzione visiva: `docs/superpowers/specs/2026-10-28-ux-direction-design.md`
- Revisione del 2026-09-25 (D1–D11, E1–E21): `docs/superpowers/specs/2026-09-25-avalonia-v1-plan-review.md`
- Spec Avalonia: `docs/superpowers/specs/2026-09-10-avalonia-as-the-platform-design.md`
- Checklist: `docs/CHECKLIST.md` · Tracker: `docs/phase-4-issues.md` · Cronaca: `docs/MILESTONES.md`
