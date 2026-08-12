# Pass visivo unico dell'Onda 2 — checklist (obbligatoria da mappa §1-bis)

**Compilata**: 2026-08-11, dalle spec d'onda (2.1–2.5) + tracker, come richiesto dalla mappa. **La sezione 6 (builder 2.5) va confermata a fine esecuzione 2.5** — il resto è definitivo.
**Contesto**: 2.2, 2.3 e 2.4 sono in `main` **senza essere mai state guardate** (decisione autore 2026-08-08: si verifica tutta l'Onda 2 in una sessione sola). Questo è quel pass.

## Procedura

- Build di `main` (post-merge 2.5), lanciata con `/run`. Sessione con l'autore, un tipo per volta, una superficie per volta.
- Findings → tracker `docs/phase-4-issues.md`, prefisso **`W2V-`**#, triati come sempre (`reported` → …).
- **Prima di segnare un finding**, controllare la lista "Già noti" in fondo: non ri-segnalare ciò che è già a tracker con destinazione decisa.
- Dati di prova: istanziare i template ufficiali (uno per tipo) + una composizione custom dal builder; qualche registrazione retroattiva per popolare strisce/heatmap.

## 1. Riga Home (Onda 2.1 + 2.1-bis)

Per ognuno dei 5 tipi, con un'abitudine popolata:

- [ ] **Chip cifra chiave** col default per-tipo: action → streak `×N`; counter → totale oggi **con unità** (durata: nell'unità della metrica, non secondi grezzi); resistance → **cronometro vivo** che scorre in riga (`20g 4:12`, coi secondi); journal → oggi ✓/—; state → label scala di oggi (es. `Bene`, non l'integer).
- [ ] **Striscia per-tipo** (larghezza fissa 96px, stile per-tipo): quadretti pieni/vuoti (action), mini-barre altezza-valore (counter), banda continua con strappi alle ricadute (resistance), punti (journal), ramp dal valore scala (state).
- [ ] **Zero-state muted** (`×0` non urlato); **nessuna parola-tipo in riga** (è nel tooltip).
- [ ] **Configura → sezione "Riga Home"**: le due manopole (cifra chiave: scelte per-tipo o nessuna; striscia: nessuna/7/14) cambiano la riga al reload; testata Configura senza enum grezzi (F0-11: tipo e nome come testo localizzato).
- [ ] Il tick del cronometro resistance non fa saltare lo scroll né consuma CPU visibile con molte righe.

## 2. Fascia "Oggi" + colonne (Onda 2.2)

Aprendo ogni abitudine (workspace):

- [ ] La fascia è **sempre visibile** sopra le colonne, anche a zero colonne aperte — il "qui non c'è niente" è impossibile.
- [ ] Contenuto per-tipo: action → toggle "Segna come fatto" + streak + % ultimi 30g + fascia oraria; counter → "+ Registra" (porta al modulo) + totale oggi + progresso goal se attivo; resistance → cronometro grande vivo + Inizio/Ricaduta + tentativi (90g); journal → gesto primario configurato ("Scrivi la voce di oggi" *oppure* "+ Nota veloce"); state → slider rapido di oggi + ultimo valore.
- [ ] **Colonne default per-tipo** alla prima apertura: journal → Diario; tutti gli altri → Cronologia. Dopo, i toggle si **ricordano per-abitudine** (riaprire un'altra abitudine non eredita le colonne della prima).
- [ ] **Calendario**: giorno selezionato = **anello/bordo**, giorno completato = **fill** — distinguibili anche con colore habit verde.

## 3. Registrazione (Onda 2.3 + fix W23)

Su un counter ripetibile (Sleep tracker) e uno non ripetibile (Water intake):

- [ ] **Non ripetibile**: seconda registrazione nello stesso giorno **somma** sul totale; la scala fa "ultima vince" (esplicitato in UI).
- [ ] **Ripetibile**: chip serie **nominate** dai template ("Night"/"Nap"), **rinominabili inline**, "+ Aggiungi" crea la successiva; mai "Serie 1/2" mute se i nomi esistono (W23-VF3).
- [ ] **Durata**: selettore unità h/min/sec accanto al campo; salvataggio normalizzato; in Cronologia l'evento singolo si legge **come orologio** (`7:30:00`), i totali **nell'unità della metrica** (90s su metrica minuti → `1.5`, mai `90 minutes`); nessuna unità doppiata accanto al campo (W23-VF2 + fix 60×).
- [ ] **Tap counter**: i numeri del giorno coerenti ovunque (riga Home / Oggi / Cronologia / calendario — W23-VF1 chiuso: stessa cifra nei quattro posti).
- [ ] Layout del modulo **identico** a serie singola e multi-serie (W23-VF6).
- [ ] **Cronometro quick-add**: icona clock↔square, tempo che scorre accanto al campo; il cronometro si ferma/riparte senza perdere il valore; cambiare giorno/abitudine non lascia cronometri fantasma.
- [ ] **Goal**: card goal con actual nell'unità giusta (niente 60×); **export** (un file per tipo): ogni valore nomina la sua unità, il summary stampa il goal.

## 4. Food diary / journal note-first (Onda 2.4)

- [ ] Food diary (template): colonna ✎ Diario mostra il **composer nota veloce**, non l'editor Markdown; **nessun flash/processo browser** all'apertura (C1: WebView2 non deve nemmeno istanziarsi).
- [ ] Composer: nota veloce con **foto** (le foto restano in attesa finché la nota non è salvata); lista voci del giorno; edit inline; elimina **con conferma**.
- [ ] Daily journal (template): editor Markdown pieno, come sempre.
- [ ] **Configura → Registrazione** (journal): il gesto si cambia Diary↔QuickNote e la colonna Diario cambia presentazione al reload; l'identità dell'habit non cambia.
- [ ] Un journal **senza preferenza** (creato a mano pre-2.4) resta sull'editor.
- [ ] Cambio giorno col composer aperto: niente doppie liste/voci duplicate (reentrancy `f15a8bc`).

## 5. Astinenza (regressione U9/U9.1 — già vista, ricontrollo veloce)

- [ ] Quick-add Inizio/Ricaduta + cronometro vivo; registrazioni chiuse con **intervallo di tempo**; elimina con conferma; heatmap Dettaglio period-aware.

## 6. Builder (Onda 2.5) — ✅ confermata a fine esecuzione (2026-08-12)

> Tre voci scritte l'11 agosto dalla spec **non corrispondono a ciò che è stato costruito**, e sono
> corrette qui sotto: il badge "nuova" non esiste, il terzo riquadro dell'anteprima non usa i componenti
> reali, e i micro-form sono inline invece che in un popup. Sono scelte motivate (tracker §"Onda 2.5"),
> non difetti: **non segnalarle come bug**. Le ultime quattro voci sono nuove.

- [ ] **Anteprima a 3 piani** (In Home / Oggi / Registrazione): reagisce **a ogni tocco** (nome, colore, icona, campi, ripetibile, gesto); dati campione plausibili con l'**unità/scala vere** del draft; riquadri inerti (niente click-through); resistance con valore statico. **Componenti reali solo nei primi due piani** — riga Home = `HeatmapStrip` vero con lo stile per-tipo, "Oggi" = il VM di produzione: il terzo è un campione disegnato, non il quick-add vero (scelta motivata: quei template vivono dentro `HabitScreen.xaml`). Giudicare la **fedeltà** del terzo riquadro, non la sua identità.
- [ ] **Mattone Programma** collassato coi default ("Ogni giorno · …"); espanso: giorni specifici / volte-a-settimana / ogni-X-giorni; streak e recupero localizzati.
- [ ] **Mattone Obiettivo** solo su counter; target + periodo; l'unità ereditata dalla metrica.
- [ ] **Mattone Sessione** solo su counter con campi; segmenti Work/Rest default, aggiungi/rimuovi/riposo.
- [ ] **Serie nominate** sotto Ripetibile; chip aggiungi/rinomina/rimuovi.
- [ ] **"+" accanto ai picker Quantità/Scala** (non su Tempo: le unità tempo sono chiuse in Core): apre un micro-form **inline sotto la lista campi** (non un popup); confermando, la voce è selezionata subito sul campo ed è riusabile dagli altri; annullare il builder NON lascia unità/scale nel catalogo (staging). **Nessun badge "nuova"**: l'unico segnale bindabile era `IsOfficial`, che marcherebbe anche le unità create in Configura. Da decidere a schermo se serve davvero un segnale.
- [ ] Journal `Diary` nel builder: **nessun WebView2** nell'anteprima (card statica).
- [ ] Round-trip a mano: composizione ricca → salva template → ristanzia → l'abitudine è identica (spot-check di E16, il guard automatico copre il resto).
- [ ] Ricostruire **Workout log** da zero nel builder (SpecificDays + sessione + 3 metriche, **Weight con la spunta "Decimali"**) e confrontarlo a schermo col template istanziato.
- [ ] **Spunta "Decimali"** su un campo Quantità: compare solo finché il campo non ha un'unità (un'unità implica già le frazioni), e sparisce appena se ne sceglie una.
- [ ] **Larghezza del dialog**: da 720 a **1080px** per la colonna anteprima. Provarlo su schermo piccolo / finestra affiancata — è il cambio più invasivo dell'onda sul layout.
- [ ] **Sfarfallio mentre si scrive**: l'anteprima si ricostruisce **interamente a ogni tasto** (il DataContext del pannello è un record nuovo ogni volta, quindi il riquadro "Oggi" ricrea il proprio sotto-albero visuale). Scrivere un nome lungo e guardare se lampeggia: se sì, la cura è ricostruire il piano "Oggi" solo quando cambia il **tipo** derivato invece che a ogni proprietà. Nessuno l'ha ancora visto girare — è un sospetto da codice, non un difetto osservato.
- [ ] **L'anteprima è inerte per scelta**: verificare che non *sembri* rotta o disabilitata (nessun hover, nessun cursore, niente click).
- [ ] **Colonna Note chiusa** (fix W24-Q1): aprire un journal e controllare che la colonna Note si apra e funzioni normalmente — il suo contenuto ora è null finché è chiusa, e questa è la superficie che quel fix tocca.

## Già noti — NON ri-segnalare

| Item | Destinazione |
|---|---|
| R8 — card Goal senza CTA "apri Configura" | tracker, aperto |
| CM-1…CM-4 — questioni-modello counter (ripetibile inerte sui tap, sonno totale-vs-sessioni, unità durata nel tipo, più cronometri) | adjudicazione col dogfooding |
| A4, A5, A7, A8, A10, W1a-VF1 — estetici/a11y (card bianche in dark, heatmap anno, righe tagliate, sezioni che sbordano, UIA) | Onda 3 |
| W23-VF7 — grafici Dettaglio vuoti (pre-esistente) | Onda 3 |
| W24-R2/R3 — test-debt | giro fix post-2.5 |
| W23-VF4 — cronometro non scopribile dal builder | **ri-triare dopo il pass**: il mattone Sessione ora espone il timer integrato, il finding potrebbe essere già superato |
| F0-6 — default lingua EN + selettore | Onda 4 |

## Esito

- [ ] Findings `W2V-*` trascritti e triati nel tracker.
- [ ] Giro fix concordato (cosa blocca E14/dogfooding, cosa scivola a Onda 3).
- [ ] Evidenze in `docs/verification/<data>-wave-2-pass/` (gitignorata — **ora che origin è attivo valutare se versionarle o copiarle altrove**: la nota "unica copia locale" era il rischio, non una scelta).
- [ ] E6/E7 spuntabili in mappa se il pass regge.
