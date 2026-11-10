# Revisione UX e percorso verso la v1.0 — proposta

**Data**: 2026-11-10 · **Stato**: proposta, in attesa dell'autore · **Branch**: `ux3a-creator-templates`
**Fonti**: i PNG headless del 2026-11-06 (`tests/Recordum.UI.Tests/bin/.../Screenshots`), `PRODUCT.md`, `DOGFOODING.md`,
la spec della direzione visiva (2026-10-28), le viste di `Recordum.UI` contro quelle di `Recordum.WPF`.
**Come leggerla**: i finding sono fatti osservati (col PNG o il file che li prova); le **raccomandazioni sono mie** e
diventano decisioni solo quando l'autore le approva. Dove una raccomandazione rivede una decisione già presa, lo dice.

## 1. La tesi

Il codice non è il collo di bottiglia. Sette checklist a schermo aspettano, il dogfooding non è partito, e l'app non ha
ancora vissuto una giornata vera. Le onde della direzione visiva hanno curato **le schermate**; quello che resta scoperto
è **la giornata**: il giro "apro, segno, chiudo" e il recupero della sera. Lì si decide se l'app è ottima.

## 2. Finding

### A. La giornata (Home)

- **A1 — Ieri dalla Home non esiste.** `PRODUCT.md`: "il giorno prima deve costare quanto oggi". La Home non ha un giorno:
  la striscia non si clicca (`HeatmapStrip` non ha gesti), il log rapido scrive solo oggi, `HomeViewModel` non ha stato
  del giorno. Recuperare ieri per le nove abitudini del manifesto = aprirle una a una e passare da "per un altro giorno"
  (un link grigio a 12 px). **Raccomandazione**: la Home ha un **giorno corrente**. L'intestazione dei giorni diventa
  cliccabile (e ‹ › accanto al titolo): il titolo passa da "Oggi" alla data, con "torna a oggi" ben visibile; log rapido e
  valori della riga valgono per quel giorno (retroattiva a `NoonOf`, giorno risolto una volta da `DayBoundary`). È la
  modifica che vale di più in tutto il percorso.
- **A2 — La tastiera registra poco.** Scorciatoie di oggi: Ctrl+1/2/3 (Attive, Archivio, Cestino: le destinazioni più
  rare), Ctrl+W, Ctrl+F, F2, Canc, Ctrl+E, Ctrl+R, Esc. Non ho trovato un tasto che registri dalla riga, né Ctrl+N. Una
  delle cinque persone "lavora con la tastiera". **Raccomandazione**: mappa minima per la v1.0 — ↑↓ fra le righe, Spazio =
  log rapido, Invio = apri, Ctrl+N nuova, ← → cambia giorno (con A1), Ctrl+, Impostazioni — e l'elenco delle scorciatoie
  in Impostazioni › Informazioni. La palette dei comandi è da 1.1.
- **A3 — La testa della Home serve le cose rare.** Segmenti Attive/Archiviate/Cestino, una ricerca a tutta larghezza,
  "fatte" e l'ordinamento occupano due righe prima della prima abitudine; con nove abitudini la ricerca non serve, e la
  giornata non ha il suo conto (i gruppi dicono "0 di 2", la Home no). **Raccomandazione** (rivede §5 della direzione
  visiva): una riga sola — "Oggi · data · 3 di 9" a sinistra; a destra cerca (icona che si apre, Ctrl+F), "fatte",
  ordinamento, e un ⋯ con Archivio e Cestino.
- **A4 — Non esiste un PNG della Home vissuta.** L'unica Home con righe (`shell-light.png`) ha tre abitudini senza dati.
  La schermata più importante non è mai stata guardata piena. Serve una fixture "manifesto": le nove abitudini di
  `DOGFOODING.md`, tre settimane di dati, metà giornata, chiaro e scuro, larga e stretta. In più: quasi tutti i PNG della
  schermata usano lo stesso cremisi, e ogni schermata sembra uno stato d'errore; le fixture usino colori diversi della
  palette.

### B. La schermata dell'abitudine

- **B1 — La stessa voce fino a quattro volte** (`habit-all-columns-light.png`): in "Voci di questo giorno", nel giorno
  scelto della Cronologia, nell'elenco della Cronologia, nella colonna Note. Per ogni tipo, con oggi selezionato, il blocco
  del giorno e la prima riga dell'elenco sono lo stesso record (astinenza: la scheda "In corso" due volte di seguito).
  **Raccomandazione**: l'elenco non ripete il giorno scelto.
- **B2 — Tre parole per scrivere.** Diario (voci), Note (colonna), nota del giorno (in Cronologia), nota veloce. Su un
  diario la colonna Note elenca le voci del diario **col Markdown grezzo** (`**lenta**`: viola UX-10) e offre "+ Nuova
  nota": non si capisce dove va un pensiero. **Raccomandazione** (decisione di modello dell'autore; chiude il brainstorm
  sospeso sulle note): due concetti soli. Su un diario esiste la **voce** (la colonna Note non c'è); sugli altri tipi
  esiste la **nota del giorno** (la colonna Note la elenca per periodo, resa). Nessuna migrazione: è una regola di vista.
- **B3 — Lo stato: cinque quadrati vuoti** (`habit-state-light.png`). Sembrano caselle da spuntare, non portano il valore
  e quello registrato oggi non è marcato. **Raccomandazione**: il numero dentro, il gradino scelto pieno.
- **B4 — "serie" vuol dire due cose**: lo streak ("serie attuale") e i set ("Registra N serie"); sulla sessione "3 serie"
  è ambiguo (UX3A-Q3). **Raccomandazione**: streak = "di fila" ("3 giorni di fila", come già dice il gruppo); "serie"
  resta ai set.
- **B5 — "Ho ceduto…" e la ricaduta in rosso** (`habit-abstinence-dark.png`). La promessa è "segnare una ricaduta senza
  giudizio". **Raccomandazione** (rivede la tabella §6.2): "Segna una ricaduta…", e "Ricaduta il 16 ott" in testo
  attenuato, con la data nel formato del resto dell'app.
- **B6 — "+ a mano" è un pulsante pieno di grigio** (`habit-session-light.png`): sembra spento, contro §4.4. Difetto.
- **B7 — La durata come orologio accanto a un'ora**: "09:00 / tempo: 20:00" si legge "alle venti". Decisione dell'autore
  (evento singolo = orologio); la segnalo perché a schermo inganna: "20 min" sotto l'ora, l'orologio da un'ora in su.
- **B8 — Mappa dell'anno**: senza mesi, non riempie la larghezza; nel gruppo "Insieme" all'anno schiaccia le righe a
  destra e compare una linea orfana (`group-brief-overview-light.png`). Rifinitura.
- **B9 — I valori nel colore dell'abitudine.** Decisione dell'autore (§4.3). Con un colore caldo, nome, quattro numeri,
  comando e mappa insieme pesano. Non propongo di cambiarla ora: si riguarda sui PNG di A4, con l'alternativa "numeri in
  inchiostro" accanto.

### C. Le parole

"journal" e "diario", "resistenza" e "astinenza", "gesto primario" (W2V-20), "2 hours" (UX3A-1), "Tipo di
pianificazione", "Frequency (days) / Keep last N backups". **Raccomandazione**: un `docs/GLOSSARY.md` di una trentina di
termini (inglese, italiano, cosa vuol dire, cosa non si scrive) e una guardia che cerca nelle `.resx` i termini vietati.
È anche ciò che serve di più a un modello piccolo per non reinventare le parole.

### D. Fiducia

- **D1 — Chi non configura il backup non ha backup.** Serve una seconda passphrase, scritta in Impostazioni. La perdita
  dei dati è l'unico errore che l'app non può permettersi. **Da decidere in 3b**: backup automatico acceso dal primo
  avvio, con la passphrase dei dati come predefinita dove c'è (Linux); su Windows la finestra di primo avvio la chiede.
- **D2 — "In arrivo".** Barra con due voci spente, Impostazioni con tre categorie spente (Sincronizzazione, Plugin, AI).
  Va bene finché l'app è nostra; in pubblico sembra un prodotto non finito, e `PRODUCT.md` dice che nella v1.0 l'AI non
  c'è. **Raccomandazione**: criterio di rilascio nuovo — nella build pubblica nessuna superficie "in arrivo".

## 3. Il percorso

La voce "F3 → F4 → F5 → F6 → F7" della ROADMAP è superata: Configura e creatore (F5) sono fatti da 2b e 3a, il diario
(F4) da F2d-2 più 3b, Note e Panoramica (F3) da 2a e 2b. Confrontando le viste, su Avalonia manca davvero: Impostazioni
Generale e Aspetto, diario a tutta finestra, finestre del backup rifatte (**3b**); `ExportDialog` (**export**);
`TrendChartControl` e `HourlyHistogramControl` (**Dettaglio**); `ChartsView` (**Statistiche**); la sezione Notifiche
(**promemoria**).

1. **Chiudere 3a** — review dell'intero branch, CI, PNG, merge dell'autore.
2. **Onda "Giornata"** (nuova, da questa revisione) — A1, A2, A3, A4, B1–B6, il glossario. È ciò che serve per usarla.
3. **Una checklist a schermo sola** al posto di sette: per percorso (primo avvio, mattino, sera, ieri, backup), solo ciò
   che un test headless non vede (mouse vero, portachiavi, foto vere, la migrazione delle foto su una **copia** dei dati).
4. **★ Dogfooding**, quattro settimane — **in parallelo** al resto. Rivede la decisione "il dogfooding aspetta la
   direzione": sono settimane di calendario che non si comprimono, e ciò che resta (3b, export, grafici) non tocca il
   giro quotidiano. Ogni aggiornamento della build installata passa dal backup che `install-linux.sh` già chiede.
5. Durante il dogfooding, in quest'ordine: **3b** → **export** (il PDF per il medico è nella promessa) → **Dettaglio**
   (i grafici per tipo) → **F8** (WPF si spegne).
6. **Dopo due settimane d'uso** si rivede lo scope coi dati veri: promemoria sì o no nella v1.0, le questioni di modello
   del contatore.
7. **Verso il pubblico**: verità sul core e tempi d'avvio (2.9), packaging, onboarding, revisione di sicurezza, beta.

**Fuori dalla v1.0, in 1.1** (raccomandazione): il builder dei grafici e la pagina Statistiche (chiude **D6**), la
modalità Calendario, "Cosa dice il gruppo", la palette dei comandi.

## 4. Chi esegue

- **Haiku**: solo i task che il piano marca *meccanici* — stringhe e traduzioni, fixture e PNG, porting ripetitivi,
  documenti. Mai Data, migrazioni, sicurezza, `DayBoundary`, né un task che cambia una firma condivisa.
- **Sonnet**: i task normali, e la review di ogni task (di Haiku e propri, in un agente diverso da chi ha scritto).
- **Opus**: la review dell'intero branch a fine onda e le correzioni che la review marca non banali. Le correzioni
  banali tornano a chi ha scritto il codice, che ha già il contesto.
- Ogni piano porta per task: **livello**, file toccati, test che devono diventare rossi e poi verdi, e "quando fermarsi
  e salire di livello" (due tentativi falliti, o il task tocca qualcosa fuori dai suoi file).
- Tre definizioni in `.claude/agents/` (esecutore, revisore, correttore), nel repository.

## 5. I documenti

| File | Oggi | Dopo |
|---|---|---|
| `ROADMAP.md` | 19 KB, di nuovo cronaca, con righe superate | una pagina: stato, prossimo passo, percorso, decisioni aperte |
| `docs/CHECKLIST.md` | — (sette liste sparse) | la checklist unica dell'autore |
| `phase-4-issues.md` | 348 KB, indice del 6 ottobre | `docs/ISSUES.md`: solo le voci aperte, una riga più il dettaglio che serve; il resto in `docs/archive/` |
| `MILESTONES.md` | 152 KB | resta (cronaca, non si legge per lavorare); nessun documento di lavoro lo cita come lettura obbligata |
| `CLAUDE.md` | 13 KB sempre caricati | il nucleo sempre caricato; le trappole di Avalonia, EF e test in `.claude/rules/` legate ai percorsi |
| `MEMORY.md` | 11 KB, metà milestone chiuse | l'indice dei soli ricordi che cambiano il comportamento |

Le voci dell'era WPF che riguardano solo viste WPF si archiviano: WPF si spegne. Restano aperte quelle che toccano
Presentation, Data o un comportamento che Avalonia ha ereditato.

## 6. Cosa decide l'autore

1. Il dogfooding in parallelo, dopo l'onda "Giornata" (§3.4) — rivede "il dogfooding aspetta". **Deciso dall'autore il
   2026-11-10: sì.** Parte dopo 3a in `main`, l'onda "Giornata" e il pezzo di checklist che protegge i dati veri.
2. Il giorno corrente in Home (A1) e la testa su una riga (A3) — rivedono §5 della direzione visiva. Mockup cliccabile:
   `docs/design/2026-11-10-giornata-mockups/home-giorno.html`. **Deciso dall'autore il 2026-11-10: sì, com'è nel
   mockup** — i giorni dell'intestazione sono pulsanti, ‹ › e ← →; il titolo dice il giorno; la colonna del giorno è
   marcata su ogni riga (anche oggi); su un giorno passato la fascia d'avviso con "Torna a oggi", e la Home torna su oggi
   alla riapertura e al cambio di giorno; i tipi a modulo aprono la scheda già su quel giorno, l'astinenza la ricaduta
   con quella data; testa su una riga (conto della giornata, lente, "fatte", ordinamento, ⋯ con Archivio e Cestino).
3. Due concetti per il testo (B2). **Deciso dall'autore il 2026-11-10**, corretto rispetto a B2 dopo aver guardato la
   colonna Note: **voce** solo nei diari (colonna Diario, lunga o breve col gesto "Nota veloce"; si rilegge in
   Cronologia; la colonna Note non c'è), **nota** su tutte le altre abitudini (l'appunto di un giorno, con foto e
   versioni; si scrive nel blocco del giorno o nella colonna Note, che **resta**). Via l'etichetta "Nota veloce" dalle
   note; il pulsante "Cronologia" dentro Note diventa "Versioni"; righe dell'elenco e nota del blocco del giorno rese,
   Markdown grezzo solo mentre si scrive. Nessuna migrazione; il "modello B" (diario dentro `Note`) non serve alla v1.0.
   **Protezione**: un diario che ha già entità `Note` tiene la colonna, che mostra solo quelle e non ne crea di nuove.
4. Le parole. **Decise dall'autore il 2026-11-10** (vanno in `docs/GLOSSARY.md` con l'inglese accanto, e una guardia sulle
   `.resx` fa fallire la build su una parola vietata):
   - streak → **"di fila"** ("3 giorni di fila", "record: 12"; in Configura la riga "Giorni di fila"); mai "serie" né
     "Streak" in italiano. In inglese resta "streak".
   - **"serie"** resta solo all'unità della palestra (`sets`, UX3A-Q3: "3 serie da 10").
   - più registrazioni nello stesso giorno → **"registrazioni"** (non "nomi delle serie").
   - il tipo → **"astinenza"** ovunque (mai "resistenza"); il tipo diario → **"diario"** (mai "journal", UX3A-Q1).
   - **"Segna una ricaduta…"** (non "Ho ceduto…"; inglese "Log a relapse…"); "Ricaduta il 16 ott" in testo attenuato,
     non in rosso; **"libero"** ("Libero da", "giorni liberi"), mai "pulito"; **"ricadute in 90 g"**, non "tentativi".
   - unità accanto ai valori (UX3A-Q4): il simbolo se c'è ("2 h", "20 min"), altrimenti il nome tradotto ("12 pagine").
   - **"Si registra con"** al posto di "Gesto primario" (W2V-20).
   - tessere del creatore (UX3A-Q2): il "+" resta come icona, non come carattere nel titolo.
5. I tagli verso la 1.1 (§3) e il criterio "niente in arrivo" (D2). **In corso (2026-11-10)**. Già detto dall'autore:
   - **Il builder dei grafici e la pagina Statistiche restano nella v1.0** (chiude **D6**: nella v1.0). Il §3 qui sopra,
     che li mandava alla 1.1, è superato su questo punto.
   - **La v1.0 comprende un APK Android**: l'autore userà l'app per lo più dal telefono. Rivede `PRODUCT.md` ("nella
     v1.0 non c'è l'app per il telefono"). Forma, ordine e ponte fra i dispositivi da definire dopo una sonda. Verificato
     sulla macchina di sviluppo: workload `android` 36.1.69 installato, SDK con `android-36`, `SQLite3MC.PCLRaw.lib`
     porta i nativi `android-arm/arm64/x64/x86`; Core, Data e Presentation sono `net10.0` puri. Nessun dispositivo
     collegato né emulatore.
   - **I ruoli dei due dispositivi** (parole dell'autore, 2026-11-10): dal telefono "praticamente tutto"; dal computer
     guardare i grafici (più spazio) e rivedere o scrivere di più sul diario. Ne segue (lettura mia, da confermare): il
     telefono è la casa dei dati e il prodotto principale; il computer è lo "studio". Tutti e due scrivono, quindi il
     ponte fra i due deve **unire**, non sostituire: spec a sé (le cancellazioni chiedono lapidi, oggi il soft delete
     c'è solo su `HabitNode`).
   - **Sonda Android fatta il 2026-11-10** (`docs/design/2026-11-10-android-probe.md`): sul Galaxy S21 dell'autore il
     database cifrato si apre, lo sblocco costa ≈ 1 s, la Home vera si disegna; il lavoro è la cornice da telefono, i
     servizi di piattaforma e il tempo d'avvio (≈ 6 s oggi). **Sincronizzazione**: l'autore usa Drive con DriveSync; il
     database resta locale, nella cartella sincronizzata ogni dispositivo scrive il proprio file cifrato e unisce
     quello dell'altro.
   - Proposte mie ancora da confermare: dall'Onda 5 restano nella v1.0 la **pausa** e un **"Cerca" unico** (nomi, voci,
     note); si aggiunge **"cambia passphrase"**; promemoria da decidere (sul telefono smettono di essere facoltativi);
     alla 1.1 il riepilogo settimanale, il Calendario, "Cosa dice il gruppo", la palette dei comandi, la cattura rapida,
     l'import CSV, risparmio e voglia resistita, la routine ordinata.
