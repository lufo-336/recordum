# Una direzione visiva per Recordum — design

**Data**: 2026-10-28 · **Stato**: da approvare · **Branch**: `ux-review`
**Brainstorm**: sessione con l'autore del 2026-10-28, con il companion visuale. I mockup approvati sono in
`docs/design/2026-10-28-ux-mockups/` (HTML statici, si aprono nel browser). **Il testo di questo documento vince sui
mockup**: alcuni mockup precedono decisioni prese dopo (ordine dei giorni, dimensioni, blocchi), e lo dicono le sezioni
sotto.
**Fonti**: i finding `UX-1…15`, `UX-P1/P2` in `docs/phase-4-issues.md` (sezione "Review UI/UX prima della checklist"); il
confronto dei font (Source Serif 4 / Source Sans 3 / Inter, misurato su PNG headless); i riferimenti dell'autore, Obsidian
(variabili CSS ufficiali) e Loop Habit Tracker (`iSoron/uhabits`).

---

## 1. Perché

L'autore, con le sue parole: Recordum "non ha il look di un prodotto finito o comunque con una direzione, tipo Obsidian o
Loop". Le finestre interne (colore, creazione da zero, Impostazioni) sono "confusionarie" o "poco professionali"; la
schermata di un'abitudine, a colonne, "può risultare spartana"; Configura è poco chiara.

La review ha trovato la radice comune: **mancano una scala tipografica e un sistema di colore**. Ogni finestra decide da
sé dimensioni, pulsanti e grigi; tutta l'interfaccia è nel serif del diario; i neutri e l'accento sono i default di
Tailwind; la heatmap ha i verdi di GitHub. Una "direzione" è un piccolo insieme di regole riconoscibili applicate ovunque.

Si fa **prima della checklist a schermo di F2d-1**: così il resoconto dell'autore riguarda la versione che userà, non una
che sta per cambiare.

## 2. Obiettivi, non-obiettivi, anti-obiettivi

**Obiettivi**
- Un sistema visivo unico (tipografia, colore, pulsanti, campi, icone) in tutte le viste Avalonia, chiaro e scuro curati uguale.
- Le finestre interne riorganizzate: Impostazioni a categorie, creatore e Configura con gli stessi blocchi, scelta del
  colore, selettore dei modelli, finestre del backup.
- La schermata dell'abitudine e quella del gruppo con una gerarchia chiara: segnare, vedere la mappa, il giorno, la Cronologia.
- Il diario scrivibile a tutta finestra e a schermo intero.
- Chiudere `UX-1…15` (UX-2 nel modo deciso dall'autore, §4.3) e `UX-P2`; verificare `UX-P1`.

**Non-obiettivi** (fuori da questo lavoro)
- Nuovi grafici (F7), il Dettaglio (F3), l'export su Avalonia (F6), i promemoria (dopo il dogfooding), la modalità
  Calendario (spec 2026-08-19, Onda 5).
- **"Cosa dice il gruppo"** (domande e "Esplora", confronti fra abitudini): progetto separato con la sua spec, dopo questo.
  Qui entra solo la vista "le figlie sugli stessi giorni" (§6.3), che non calcola niente.
- Toccare WPF (congelata): solo dove un contratto condiviso lo obbliga, e WPF deve continuare a compilare e passare i test.
- Una scelta del font nelle Impostazioni (WPF l'aveva, `Settings.Font`): non si porta (decisione mia, §12).

**Anti-obiettivi** (cose che renderebbero il lavoro un fallimento anche se "funziona")
- **Perdere una funzione che esiste oggi.** L'inventario dell'Appendice A è una lista di controllo: ogni voce deve avere il
  suo posto nel nuovo disegno. Il redesign riorganizza, non toglie.
- Gamification: niente badge, coriandoli, punteggi.
- Colore che decora: il colore è dell'abitudine e la identifica (barra, tinta della riga, valori, mappa, comando che registra).
- Numeri che sembrano scoperte senza dati dietro.
- Testo inglese in un'interfaccia italiana, o il contrario.

## 3. Vincoli

- Le regole di CLAUDE.md: Presentation resta `net10.0` senza UI (un ViewModel dichiara un'intenzione, mai un pennello; il
  colore dell'abitudine viaggia come hex e le tinte si calcolano nella UI); ogni stringa in `.resx` inglese e italiano;
  `x:DataType` su ogni view; `InitializeComponent()`; `TreatWarningsAsErrors`; versioni esatte dei pacchetti.
- **I ViewModel sono condivisi con WPF**: le modifiche in Presentation sono additive; WPF compila e i suoi test restano verdi.
- Il giorno di un istante si chiede a `DayBoundary`, risolto una volta per operazione (vale per periodi e numeri della §5).
- Prestazioni: Home con 300 righe nel budget di F1-PERF1 (Home < 500 ms); le tinte non si ricalcolano per riga a ogni render.
- Principio 8: ogni cosa che un modello fa si fa nel creatore (`BuilderTemplateParityTests` resta verde).
- Nessuna dipendenza nuova tranne il font Inter (OFL, voce in `THIRD-PARTY-NOTICES.md`).

## 4. Il sistema visivo

### 4.1 Tipografia (decisione dell'autore)

- **Inter** per l'interfaccia. **Source Serif 4** solo per il testo dell'utente (diario, note, anteprime delle voci) e per il
  marchio ("R" e "Recordum").
- Scala unica: **20** titoli di schermata e numeri grandi · **16** titoli di sezione · **14** testo · **13** etichette e
  comandi · **12** note, giorni, didascalie. Pesi: 400, 500 (nomi, comandi), 600 (titoli).
- Diario: **16 px** con interlinea 1,6 nella colonna; **18 px** con interlinea 1,65 a tutta finestra e a schermo intero,
  riga di circa 70 caratteri (≈ 640 px), centrata.
- Cifre tabellari (`tnum`) per tempi, timer e colonne di numeri; il monospace (`Consolas`) di oggi nel timer esce.
- Come entra Inter: lasciato al costruttore (il pacchetto ufficiale `Avalonia.Fonts.Inter` alla stessa versione di
  Avalonia, oppure i TTF in `src/Recordum.UI/Fonts/` come Source Serif); in entrambi i casi la licenza OFL viaggia.

### 4.2 Colore

Neutri leggermente caldi, acromatici quanto basta (Obsidian), un solo colore "d'azione" che è l'inchiostro. Token (nomi
indicativi, il costruttore sceglie le chiavi; i valori sono quelli dei mockup):

| Ruolo | Chiaro | Scuro |
|---|---|---|
| Fondo della finestra | `#FBFBFA` | `#1B1B1A` |
| Pannello / superficie | `#FFFFFF` | `#222221` |
| Barra a sinistra | `#F4F3F1` | `#181817` |
| Riga, bordo leggero | `#ECEBE8` | `#2E2E2C` |
| Bordo dei campi | `#D6D5D0` | `#3A3A37` |
| Selezione / chip | `#ECEBE8` | `#2E2E2C` |
| Testo | `#1F1F1D` | `#E6E5E1` |
| Testo attenuato | `#6B6A65` | `#A3A29C` |
| Testo tenue (segnaposto, "·") | `#A3A29C` | `#6B6A65` |
| Inchiostro (pulsante primario) / sopra | `#1F1F1D` / `#FFFFFF` | `#E6E5E1` / `#1B1B1A` |
| Pericolo | `#B42318` | da tarare |
| Avviso / fondo | `#B45309` / `#FDF6EC` | da tarare |

- Tre livelli di testo (normale, attenuato, tenue): i segnaposto usano il tenue, non grigi ad hoc.
- L'accento di sistema di Fluent (`ColorPaletteResources` in `App.axaml`: caselle, focus, selezioni) passa all'inchiostro.
- Contrasto: testo e testo attenuato ≥ 4,5:1 sul loro fondo; il tenue è solo per segnaposto e separatori.

### 4.3 Il colore dell'abitudine

- **La riga resta tinta** (decisione dell'autore, che rivede UX-2): fondo = colore dell'abitudine mescolato al pannello al
  **9 %** in chiaro e al **16 %** in scuro, più la barra di 4 px. Angoli 6 px.
- **I valori** (numeri, ✓ della striscia, della mappa, delle statistiche) sono nella **variante "testo"** del colore
  dell'abitudine, calcolata nella UI: in chiaro il colore si scurisce, in scuro si schiarisce (come in Loop), **quanto basta
  per 4,5:1 sulla sua tinta**. La palette, le tinte, le barre e la mappa restano nel colore scelto. Misurato dalla
  verifica del costruttore: col colore pieno in chiaro solo 3 colori su 16 arrivano a 4,5:1; la variante vale per
  qualunque colore, anche quelli liberi.
- **Heatmap e mappa a 5 gradini nel colore dell'abitudine**: vuoto = cella neutra; poi 25 / 50 / 75 / 100 % del colore sul
  pannello. Escono i verdi di GitHub (`Brush.Heatmap.Level*`). Vale anche per `YearHeatmapControl`.
- La regola vale per **qualunque** colore (anche quelli liberi e quelli dei modelli di oggi), non solo per la palette.
- Una prova eseguibile (§10) controlla il contrasto della variante "testo" sulla tinta per i 16 colori della palette e per
  un campione di colori liberi, nei due temi.

### 4.4 Pulsanti, campi, icone

- **Colore dell'abitudine**: solo il comando che **registra** (in testa alla schermata dell'abitudine e il log rapido della
  riga in Home): "✓ Fatto oggi", "+1", "Scrivi", "▶ Inizia", "Registra".
- **Inchiostro**: ogni altra azione principale, **una per finestra** ("+ Nuova", "Crea abitudine", "Salva", "Backup ora…").
- **Contorno**: le secondarie ("Salva come modello", "Sfoglia…"). **Solo testo**: "Annulla".
- Nessun pulsante secondario pieno di grigio (oggi sembrano disabilitati).
- Campi con la cornice (bordo dei campi, angoli 6 px), mai solo la riga sotto (UX-15).
- Icone **Lucide** ovunque; via `✎` e ogni glifo Unicode o emoji (UX-13). Nei mockup `⚠`, `🖼`, `❝` sono segnaposto.
- Angoli: 6 px controlli e righe, 8–10 px pannelli e finestrelle. Spaziature a passi di 4.

### 4.5 Date e ore (UX-9)

Sempre dalla cultura: breve "mer 28 ott", lunga "mercoledì 28 ottobre", ore "7:42". Mai ISO (`2026-03-15 12:00`)
nell'interfaccia.

## 5. La cornice e la Home

Mockup: `cornice.html`, `anteprima-home-v2.html`, `integrazioni.html` (parte 1), `correzioni.html`.

**Barra a sinistra** (52 px): in alto "R" in serif; poi **Oggi**, **Diario** (spento, "in arrivo"), **Statistiche**
(spento fino a F7); in basso **Impostazioni**. Le voci spente si vedono, con "in arrivo" al passaggio del mouse (oggi la
barra è spenta del tutto, F1-RAIL1). C'è posto per una voce futura (Calendario, Onda 5).

**Il menu ☰ sparisce** (decisione dell'autore): Home → la barra; Grafici → Statistiche; Crea backup, Ripristina,
Cronologia backup, Esporta libreria → Impostazioni › Dati e backup; Informazioni diagnostiche → Impostazioni ›
Informazioni. Il comando "Mostra/nascondi barra di navigazione" esce con lui.

**La Home**
- Titolo **"Oggi"** con la data accanto (decisione dell'autore; sostituisce "Home" nel titolo e nella barra).
- "+ Nuova ▾" in inchiostro, con il menu Nuova abitudine / Nuovo gruppo.
- Attive · Archiviate · Cestino come segmenti; sotto, **Cerca**, **"fatte"** (= Nascondi completate oggi, interruttore) e
  l'ordinamento (Manuale…).
- **Intestazione dei giorni** sopra le strisce (UX-6), con **oggi a destra** (decisione dell'autore: come oggi, dal più
  vecchio al più nuovo) e la scritta "oggi". Le strisce sono allineate a destra, quindi la colonna di oggi è allineata anche
  fra righe con strisce di lunghezza diversa (7 o 14 giorni, o nessuna); l'intestazione copre la più lunga visibile.
- Riga: tinta (§4.3), nome 14/500, valore chiave attenuato 12, striscia coi valori del tipo (numero, ✓, "·" per vuoto),
  **log rapido** a destra nel colore dell'abitudine (il comando del tipo: +1, ✓, la scala, ▶). La riga dell'abitudine aperta
  nella scheda attiva ha un contorno di 1,5 px nel suo colore.
- **Gruppi** in Home: riga d'intestazione non tinta, con la barra nel colore del gruppo, il chevron, il nome 12/600 e
  "3 di 5" (decisione mia, §12); le figlie rientrate.
- Menu della riga: le voci di oggi (Rinomina, Fissa in alto / nel gruppo / Non fissare, Archivia, Sposta nel cestino; in
  Archivio e Cestino Ripristina ed Elimina definitivamente; Log rapido) con le icone Lucide. Trascinare per riordinare e
  spostare nei gruppi resta com'è.
- Caricamento, errore con Riprova, Home vuota, Archivio vuoto, Cestino vuoto: una frase che dice cosa succede e al più un
  pulsante ("+ Nuova abitudine").
- Corregge F2b-5 se il nuovo layout dell'intestazione lo permette senza lavoro a parte (altrimenti resta aperto).

**Schede delle abitudini aperte**: pallino nel colore, nome, × per chiudere, fermaglio quando la scheda è fissata; a
schede vuote, "Seleziona un'abitudine dalla barra laterale".

**Barra del timer** in basso: tinta all'8 % del colore dell'abitudine che corre, tempo in cifre tabellari, nome,
segmento ("lavoro 2 di 3"), Pausa / Riprendi / Ferma.

**Sblocco** e **errore fatale**: il marchio in serif, una frase su cosa succede, il campo, "Ricorda su questo dispositivo",
un pulsante; al primo avvio la conferma della passphrase; l'avviso onesto sulla passphrase persa.

Su Windows resta la barra del titolo propria (riduci, ingrandisci, chiudi) con i nuovi token; su Linux quella di sistema (D7).

## 6. La schermata dell'abitudine e del gruppo

Mockup: `anteprima-home-v2.html`, `combinazioni.html`, `gruppo.html`, `inventario.html` (periodo), `integrazioni.html`
(parte 2), `correzioni.html`. Nei mockup il nome è a 22 px e i periodi sono solo tre: valgono la §4.1 e questa sezione.

### 6.1 Struttura (uguale per ogni tipo)

1. **Testa** (larga quanto la scheda; filo di 3 px nel colore dell'abitudine in cima):
   - nome a **20/600 nel colore dell'abitudine** (UX-7), sotto il programma e "di solito verso le …";
   - a destra **un solo comando che registra** (§6.2), con sotto "annulla · per un altro giorno". Sostituisce la casella in
     testa e il pulsante sotto (UX-8). "Per un altro giorno" sceglie il giorno; anche un clic su un giorno della mappa lo fa;
   - **quattro numeri** (§6.2) sotto una riga sottile;
   - i **pulsanti delle colonne**: Diario (solo diari), Cronologia, **Note**, Configura; Dettaglio resta nascosto (F3).
     Oggi su Avalonia Note e Dettaglio sono chip nascosti: **la colonna Note si porta in quest'onda** (decisione
     dell'autore, 2026-10-28), sui ViewModel che esistono già in Presentation (`NotesViewModel`, `DayNoteViewModel`,
     `NoteHistoryViewModel`), con WPF come riferimento di comportamento.
2. **Colonne** come oggi (proporzioni salvate, larghezza minima, focus di una colonna con Esc): ognuna ha la sua testa con
   "a tutta finestra" e **×** per chiuderla. Via la didascalia che ripete il nome della scheda (UX-8).

**La colonna Cronologia**
- **Selettore di periodo**, uno solo, che comanda mappa e elenco: Settimana · Mese · 3 mesi · Anno · Tutto; ‹ › per
  spostarsi; "Dal… al…" per un intervallo libero; "Oggi" per tornare al presente.
- **Mappa** che cambia forma col periodo: settimana = 7 caselle grandi; mese = calendario (numeri del giorno a 12 px,
  centrati, leggibili — UX-14; il giorno fuori mese chiaramente diverso da un giorno vuoto; oggi con l'anello; selezione ≠
  completamento, come chiedeva F0-13); 3 mesi e anno = heatmap; tutto = una heatmap per anno. Colori e gradini della §4.3;
  il segno delle note resta sui giorni con note.
- **Il giorno scelto**: le sue registrazioni (con × per eliminarne una) e **la nota dell'abitudine per quel giorno**, in
  serif; se non c'è, un campo tratteggiato che invita a scriverla. È la stessa nota della colonna Note (`DayNoteViewModel`):
  scriverla in un posto la mostra nell'altro.
- **Cronologia**: le registrazioni del periodo, dalla più nuova, con l'ora e l'anteprima della nota; filtro "Tutto / Con
  note". Per i diari le voci si mostrano **rese** (titoli, grassetto, elenchi, foto come "[foto]" o miniatura), mai il
  Markdown grezzo (UX-10). Per l'astinenza, i periodi con "In corso".

### 6.2 Cosa cambia per tipo

Dalla grammatica di `HabitCompositionRules` e dalle viste QuickAdd/Today che esistono. I numeri sono quelli che il
costruttore deve offrire; dove un numero non ha già una fonte nei servizi, si aggiunge una query in Data con i suoi test;
quelli segnati *facoltativo* si tolgono se costano troppo. **Dove il codice ha già una definizione, si riusa** (per
esempio la percentuale "ultimi 30 giorni" di `ActionTodayView` e l'avanzamento di `GoalProgressCardViewModel`), così i
numeri tornano con la striscia e con la mappa. Le percentuali sono sui giorni **previsti dal programma**, non sui giorni di
calendario. "Volume" delle serie = la somma del primo campo quantità del giorno, col suo nome e la sua unità (niente
prodotti fra campi: un contatore a serie può avere campi che non si moltiplicano); senza un campo quantità il numero non
c'è e al suo posto va "serie in tutto".

| Caso | Comando in testa | Quattro numeri | Mappa |
|---|---|---|---|
| Azione | ✓ Fatto oggi (togli) | serie attuale · serie migliore · % 30 giorni · in tutto | fatto / non fatto |
| Contatore a tocco | +1 e − (Annulla ultima), "5/8" se c'è l'obiettivo | oggi · media 30 g · serie dell'obiettivo · in tutto | gradini rispetto all'obiettivo |
| Misurato (un campo, quantità o tempo) | il campo (col cronometro se è tempo: avvia, ferma e compila) + Registra | periodo dell'obiettivo · % obiettivo · media al giorno · in tutto | gradini sul valore |
| Serie (più campi, più volte al giorno) | le righe delle serie (aggiungi, rinomina, togli) + "Registra N serie" | serie oggi · volume oggi · giorni a settimana · sedute | gradini sul volume; le serie del giorno come righe |
| Sessione (timer) | ▶ Inizia e "+ a mano"; mentre corre: tempo grande, Pausa/Riprendi, Segmento successivo, Ferma | oggi · serie · media 30 g · in tutto | gradini sui minuti |
| Stato (scala) | i gradini con le etichette, "ultimo: …" | oggi · media 30 g · andamento *(facoltativo)* · giorni segnati | ogni giorno il suo valore; nessuna "serie" se non è configurata |
| Astinenza | "Ho ceduto…" (Ricaduta) / Inizio | adesso · il periodo più lungo · ricadute in 90 g · giorni liberi in tutto | fascia continua; una ricaduta è un taglio, non un giorno rosso |
| Diario | Scrivi / Nota veloce secondo il gesto principale | voci della settimana · serie · tag del mese *(facoltativo)* · voci | giorni con una voce |

### 6.3 Il gruppo

Oggi su Avalonia un gruppo mostra solo la testa e i chip: la Panoramica era di F3. **Si porta in quest'onda** (decisione
dell'autore, 2026-10-28) partendo da `PanoramicaViewModel` e dal `GoalProgressCardViewModel` che esistono già.

- Testa: nome del gruppo nel suo colore; al posto del comando, **l'avanzamento di oggi** ("3 di 5" e una barra).
- Numeri: giorni completi di fila · % 30 giorni · obiettivo del gruppo · la figlia più saltata *(facoltativo)*.
- **Oggi**: le figlie come righe tinte, **ognuna col log rapido della Home** (lo stesso comando, `QuickActionHandlerSelector`)
  — si segnano da qui (comportamento nuovo; oggi un clic seleziona soltanto). Per i tipi il cui comando è un modulo
  (misurato, serie, sessione, diario) il log rapido apre la figlia, come in Home. Un clic sul nome apre la figlia
  (`ChildSelected`, com'è); i sottogruppi rientrati.
- **Insieme** (vista A, decisione dell'autore per quest'onda): ogni figlia è una riga di celle sugli **stessi giorni**,
  comandata dallo stesso selettore di periodo. Non calcola niente.
- Mappa dell'anno: gradini = quante figlie quel giorno (pieno = tutte).
- Configura del gruppo: Chi è · Per le figlie (inizio della giornata ereditato, obiettivo del gruppo) · Contenuto (abitudini
  e sottogruppi) · Salva come modello.

### 6.4 Configura e il creatore: gli stessi blocchi

Mockup: `builder.html`, `integrazioni.html` (parte 3), `correzioni.html` (parte 3). Il mockup del creatore ha ancora "Come
si registra" separato: vale l'ordine qui.

Ordine unico per **creatore e Configura**:

1. **Chi è** — nome (20/600), icona, colore (§7.1); in Configura anche descrizione e colore del segno delle note.
2. **Cosa registri** — campi/metriche, scale (etichette in un elenco di campi, non più un array JSON da scrivere a mano),
   unità nuove (nome, simbolo), nomi delle serie; poi **come si registra**: gesto principale, più volte al giorno, "un tocco
   = +1", "è un'astinenza", sessione a tempo (timer integrato, modalità, obiettivo). Ogni casella con una riga che spiega.
3. **Quando conta** — programma (giorni, ogni X giorni, volte a settimana), serie (modalità, recupero), obiettivo (nome,
   periodo); in Configura anche **inizio della giornata**, con l'avviso accanto: cambiarlo rilegge tutto lo storico.
4. **Come appare in Home** — valore accanto al nome e striscia, con l'anteprima della riga. Nel creatore è la colonna di
   anteprima a destra (com'è oggi, con le anteprime per tipo: Ricaduta/Inizio, voce di diario, nota veloce con foto, +1).
5. **Avanzate** (chiuso) — tolleranza della serie, obiettivo su giorni mobili, segmenti del timer, riposo, richiesta
   automatica, condivisione con l'AI ("in arrivo" finché l'AI non c'è).

In Configura ogni voce è **una riga col riassunto** ("Ogni giorno ›") che si apre sul posto. Le cose non ancora pronte dicono
"in arrivo" invece di non funzionare.

**Il creatore** in più: la riga "Si comporta come **contatore** · cambia da solo con quello che registri"; le quattro tessere
Quantità · Tempo · Scala · Testo con la riga che dice a cosa servono (spente con la spiegazione quando `CanAddField` lo
vieta); "+ Obiettivo · + Sessione a tempo"; in fondo **Annulla** (testo), **Salva come modello** (contorno, con "Salvato ✓"),
**Crea abitudine** (inchiostro; sostituisce "Lancia"). Escono "Colore (hex)" e la sezione vuota "Dati d'esempio" (UX-12).

## 7. Le finestre interne

### 7.1 Scelta del colore (decisione dell'autore: "un mix")

Mockup: `colore.html` (la B, più "Avanzate").
- In vista: **16 colori** pronti (cerchi da 28 px, 8 per riga), i **Recenti**, l'anteprima **"Come appare"** (la riga della
  Home in chiaro e in scuro).
- **Avanzate ›** apre il selettore libero (quadrato e barra della tonalità), le **sfumature**, i **preferiti** (con
  "+ aggiungi") e l'**esadecimale**.
- Palette (chiaro; lo scuro segue la §4.3): `#C2405B` `#D0603A` `#C98A1E` `#9A9A2A` `#5F9A3C` `#2F9C8C` `#2F8FB0` `#2F7FC1`
  `#4F63C4` `#7A5BC4` `#A352A8` `#C2508A` `#8A6A4F` `#6B7280` `#4B5563` `#9A8F80`. Non si ritoccano per il contrasto: ci pensa la
  variante "testo" (§4.3).

### 7.2 Selettore dei modelli

Mockup: `modelli.html`.
- In alto due tessere: **Da zero**, **Gruppo vuoto**.
- **Modelli** come schede (barra del colore, nome, descrizione di una riga, tipo: "diario · più volte al giorno"), con
  **Cerca un modello…**; sotto, **I tuoi modelli** con Rinomina ed Elimina nel menu della scheda.
- A destra, la scheda scelta: come apparirà in Home, cosa contiene (cosa registri, quante volte, gesto principale).
- **Usa questo modello** (inchiostro) crea l'abitudine così com'è; **Personalizza…** apre il creatore già riempito.
- **I modelli ufficiali si traducono** (UX-11): nome e descrizione da `.resx` attraverso l'Id stabile del modello
  (`00000003-…-00NN`), senza toccare i dati seminati; anche il nome dell'abitudine creata da un modello ufficiale nasce
  nella lingua dell'interfaccia. La traduzione copre **tutto il testo che l'utente vede** nel contenuto del modello: nomi
  delle metriche, unità, nomi delle serie, segmenti, etichette delle scale seminate. Si fissa **alla creazione**, nella
  lingua di quel momento: l'abitudine creata è dell'utente e non cambia se poi cambia lingua. I modelli dell'utente restano
  come li ha scritti. `BuilderTemplateParityTests` copre anche i modelli tradotti.

### 7.3 Impostazioni a categorie

Mockup: `impostazioni.html`, `correzioni.html` (parte 5).
- Finestra con le **categorie a sinistra** (icone Lucide) e a destra righe "cosa fa (con una riga di spiegazione) · comando".
- Categorie: **Generale** (lingua, formato regionale con anteprima e avviso di riavvio, inizio della giornata) · **Aspetto**
  (tema Sistema / Chiaro / Scuro) · **Diario e foto** (le tre scelte D10 con l'avviso sul luogo) · **Dati e backup**
  (Backup automatico: interruttore, ogni quanti giorni, quanti tenerne, cartella con Sfoglia…, passphrase dei backup con
  "Imposta…"; Adesso: Backup ora…, Ripristina…, Esporta libreria ("in arrivo" fino a F6); Cronologia con Apri cartella ed
  Elimina) · **Sicurezza** (Ricorda / Dimentica su questo dispositivo, stato del portachiavi; "cambia passphrase" in arrivo,
  F2c-9) · *In arrivo*, spente: Sincronizzazione, Plugin, Assistente AI · **Informazioni** in fondo (versione, commit,
  cartella dei dati, informazioni diagnostiche).
- **Generale e Aspetto si portano da WPF** (decisione dell'autore): WPF è il riferimento di comportamento; le chiavi
  `Settings.Language`, `Settings.Format*`, `Settings.Culture.*`, `Settings.General.DailyResetTime*`, `Settings.Theme*`
  esistono già.
- **Niente "Salva" in fondo** (decisione dell'autore): ogni scelta vale subito; la passphrase si imposta in una finestrella
  sua con la conferma; gli errori stanno sulla riga a cui si riferiscono. Il vecchio avviso rosso diventa una riga d'avviso
  (§4.2) sotto la passphrase.

### 7.4 Le finestre del backup

Mockup: `integrazioni.html` (parte 4).
- **Fai un backup**: Dove (con Sfoglia…), passphrase, ripetila; Annulla · **Crea backup**.
- **Ripristina un backup**: File, passphrase, **Anteprima** con quello che il backup già dichiara (data di creazione,
  schema); piattaforma e conteggi solo se il manifest li porta già — in quest'onda non si aggiungono campi al formato.
  Annulla · Anteprima · **Ripristina**. La frase dice che i dati di adesso vanno in una copia di sicurezza.

### 7.5 Il diario

Mockup: `diario.html`.
- Tre stati: **nella colonna** (16 px) · **a tutta finestra** (18 px; è il focus della colonna di oggi, esteso a tutta la
  scheda) · **a schermo intero** (F11; solo l'editor).
- In testa: nome dell'abitudine, data, "bozza salvata", interruttore della **barra dell'editor**, schermo intero, ritorno alla
  colonna (Esc), **Salva** (inchiostro).
- La barra contiene **solo ciò che l'editor sa già fare**: titoli, grassetto, corsivo, elenchi, citazione, `#tag`, immagine.
  A schermo intero è nascosta e torna con una scorciatoia o portando il mouse in alto.
- In fondo, il conteggio delle parole. Testo centrato su ~70 caratteri a qualunque larghezza.
- Restano tutte le funzioni di oggi: voce e nota veloce secondo il gesto, foto (aggiungi, togli, "foto non disponibile"),
  tag (+ tag, togli), voci del giorno con Modifica ed Elimina, bozza ripresa dopo un crash.

## 8. Quello che il disegno lascia libero

Posto lasciato, niente costruito: colonna Dettaglio (F3), Statistiche nella barra (F7), export nel menu ⋯ della testa
(F6), voce Calendario nella barra (Onda 5), promemoria "in arrivo", "Cosa dice il gruppo" (sezione del gruppo, spec a parte).

## 9. Le tre onde

Ognuna su un branch suo, chiusa da una review dell'intero branch e dalla CI verde, con i PNG nuovi negli artefatti. L'autore
può provare qualcosa di finito dopo ognuna.

1. **Fondamenta e Home** — Inter e la scala; i token della §4.2 in chiaro e in scuro, l'accento Fluent; la regola delle
   tinte e dei valori (§4.3) e la heatmap; pulsanti, campi, icone, date; la Home (§5); la cornice: barra a sinistra, ☰ tolto
   (i suoi comandi spostati, quelli delle Impostazioni attivi dopo l'onda 3: fino ad allora restano raggiungibili da dove
   sono), schede, barra del timer, sblocco, schermate vuote; `UX-P2`.
2. **Abitudine e gruppo** — §6 intera (testa, colonne, periodo, mappa, giorno, nota, Cronologia resa, gli 8 casi),
   **la colonna Note** e **la schermata del gruppo** (Panoramica) portate da F3, con "Insieme"; Configura in cinque blocchi;
   la scelta del colore (§7.1). È l'onda più grande: il piano può spezzarla in due (abitudine; gruppo e Note).
3. **Le finestre interne** — creatore (§6.4), selettore dei modelli con la traduzione (§7.2), Impostazioni a categorie con
   Generale e Aspetto da WPF (§7.3), finestre del backup (§7.4), diario a tutta finestra e a schermo intero (§7.5).

Poi la checklist a schermo di F2d-1 dell'autore, sulla versione finita; poi la spec di "Cosa dice il gruppo". Il dogfooding
parte dopo (decisione dell'autore: "il dogfooding può aspettare"); F3 perde la Panoramica e le Note, che arrivano qui, e la
ROADMAP va aggiornata quando questa spec è approvata.

## 10. Obblighi di test

- **PNG headless in chiaro e in scuro** per ogni schermata toccata, con un'**asserzione strutturale** ciascuno (un PNG da
  solo non prova niente). Le viste nei test si ospitano **con i token veri**: oggi le schermate del dettaglio escono in sans
  perché la finestra di test non eredita `Font.Default` (`UX-P2`) — si risolve una volta, per esempio con uno stile
  `:is(Window)` in `App.axaml` (provato nel confronto dei font), valido anche in produzione.
- **Prova del contrasto** eseguibile: per i token di testo sui loro fondi e per i valori dei 16 colori sulla loro tinta, nei
  due temi, rossa sotto soglia (provata rossa con un colore che non passa).
- **Inventario**: a fine di ogni onda, la tabella dell'Appendice A si spunta per le voci dell'onda; la review controlla che
  nessuna funzione sia sparita (un comando che non è più legato a niente).
- I numeri nuovi della §6.2 che richiedono query: test in Data, compresi il `DayBoundary` e i giorni senza dati.
- Le stringhe nuove in inglese e italiano; `UX-P1` (inglese in Configura) verificato a schermo e, se è la cultura del test,
  corretto il test.
- Il budget della Home con 300 righe (`Category=Performance`) prima e dopo l'onda 1, in locale.

## 11. Decisioni dell'autore (2026-10-28)

Direzione B · riga tinta mantenuta, dosata per tema · Inter per l'interfaccia e Source Serif 4 per il diario e il marchio ·
Impostazioni a categorie, senza Salva, foto in "Diario e foto", "Dimentica" in "Sicurezza", Generale e Aspetto da WPF ·
colore: 16 pronti + "Avanzate" con selettore libero, sfumature, preferiti, esadecimale · creatore in blocchi · schermata
dell'abitudine come in `anteprima-home-v2` con le colonne apribili, la nota del giorno, la Cronologia · più periodi per mappa
e Cronologia · il gruppo con le figlie segnabili e la vista "Insieme" · "Cosa dice il gruppo" (domande + Esplora) come
progetto separato · diario a tutta finestra e a schermo intero con la barra dell'editor · selettore dei modelli · il ☰ tolto,
i comandi nelle loro sezioni · **oggi a destra** nella striscia · titolo **"Oggi"** · **la colonna Note e la schermata del gruppo si portano adesso
da F3**, il dogfooding aspetta.

## 12. Decisioni mie (da controllare leggendo)

1. Dimensioni: titoli e numeri grandi a 20 (non 22 come nei mockup); diario 16 in colonna, 18 a tutta finestra.
2. La regola dei pulsanti della §4.4 (colore dell'abitudine solo per registrare).
3. L'ordine unico dei cinque blocchi per creatore e Configura, con "come si registra" dentro "Cosa registri".
4. La riga d'intestazione dei gruppi in Home non tinta, con la barra e "3 di 5".
5. Niente scelta del font nelle Impostazioni (Inter è fisso; WPF aveva Sistema/Serif).
6. "A tutta finestra" del diario = il focus della colonna che esiste già; lo schermo intero è nuovo.
7. Le etichette delle scale come elenco di campi invece dell'array JSON.
8. Il "facoltativo" su "andamento", "tag del mese" e "la più saltata".
9. L'anteprima del ripristino mostra solo ciò che il manifest porta già.
10. Valori dei colori "da tarare" in scuro (pericolo, avviso) lasciati al costruttore con la prova di contrasto.
11. Il contrasto dei valori con la variante "testo" calcolata (§4.3), non ricolorando la palette.
12. Nel gruppo, il log rapido della Home per ogni figlia (§6.3); i tipi a modulo aprono la figlia.
13. Le definizioni dei numeri (§6.2): riuso di quelle esistenti, percentuali sui giorni previsti, "volume" = somma del primo
    campo quantità.
14. La traduzione dei modelli ufficiali copre tutto il loro testo e si fissa alla creazione (§7.2).

## Appendice A — Inventario delle funzioni di oggi (lista di controllo)

Estratto dalle viste Avalonia il 2026-10-28; la versione con "dove va" è `docs/design/2026-10-28-ux-mockups/inventario.html`.

- **Home**: cerca; nascondi completate oggi; ordinamento; Attive/Archiviate/Cestino; + Nuova ▾ (abitudine, gruppo); log
  rapido; menu della riga (rinomina, fissa in alto / nel gruppo / non fissare, archivia, cestino, ripristina, ripristina
  dall'archivio, elimina definitivamente); trascina e rilascia; caricamento, errore e Riprova; vuoti di Home, Archivio, Cestino.
- **Cornice**: barra del titolo propria su Windows; Home, Grafici, Backup (crea, ripristina, cronologia), Esporta libreria,
  Informazioni diagnostiche, Impostazioni (oggi nel ☰); schede (chiudi, fissa/sblocca, vuoto); barra del timer (pausa,
  riprendi, ferma).
- **Schermata dell'abitudine**: colonne Diario, Cronologia, Dettaglio, Note, Configura; focus ed Esc; Esporta (nascosto);
  calendario Settimana/Mese con ‹ ›; registrare per un altro giorno; elimina registrazione; azione (segna, %, "di solito
  verso le"); contatore a tocco (+1, annulla ultima); misurato (campi, cronometro); serie (aggiungi, rinomina, togli,
  registra N); stato (scala, "ultimo"); astinenza (inizio, ricaduta, periodi "in corso", tentativi in 90 g); timer (avvia,
  pausa, riprendi, ferma, segmento successivo); segno delle note sul calendario (la colonna Note
  e la Panoramica del gruppo esistono in WPF e nei ViewModel condivisi, non ancora su Avalonia: si portano in quest'onda).
- **Diario**: voce del giorno e nota veloce; foto (aggiungi, togli, non disponibile); tag (+ tag, togli); inserisci immagine;
  voci del giorno con modifica ed elimina; bozza.
- **Configura**: nome, descrizione, colore (con preferiti), icona; riga in Home (valore chiave, striscia); metriche (aggiungi,
  elimina); scale (etichette); registrazione (gesto principale, più volte al giorno); programma (tipo, giorni, ogni X, volte a
  settimana); serie (modalità, recupero, tolleranza); obiettivo (nome, periodo, giorni mobili); timer (integrato, modalità,
  obiettivo, riposo, segmenti, richiesta automatica); orario di reset con avviso; colore del segno delle note; condivisione AI.
- **Creatore**: nome, colore, icona; più volte al giorno; quantità, tempo, scala, testo; contatore a tap; astinenza;
  decimali; unità nuova (nome, simbolo); scala nuova (gradini); nomi delle serie; programma (ogni X giorni, volte a
  settimana); serie e recupero; + obiettivo; + sessione e segmenti; gesto principale; anteprime per tipo; salva come
  template; lancia.
- **Modelli**: da zero; gruppo vuoto; predefiniti; i tuoi (rinomina, elimina).
- **Impostazioni › Dati e backup**: backup automatico (abilitato, frequenza, quanti, cartella, passphrase, conferma, prova,
  salva); crea; ripristina; cronologia (apri cartella, elimina); foto (tre scelte, avviso); dimentica su questo dispositivo.
- **Finestre**: crea backup (destinazione, passphrase, conferma); ripristina (file, passphrase, anteprima con lo schema);
  sblocco (passphrase, conferma al primo avvio, ricorda); errore fatale.
