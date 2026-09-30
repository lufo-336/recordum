# Onda 2.5 — Builder: superficie di composizione completa (design)

**Data**: 2026-08-11 · **Stato**: approvato dall'autore (brainstorm 2026-08-11, sezione per sezione)
**Contesto**: mappa v1.0 §1-bis punto 4 (`2026-07-03-v1-redefinition-plan-design.md`), spec Onda 2 §5/§7.6 (`2026-07-03-wave-2-per-type-experience-design.md`). Ultimo pezzo dell'Onda 2, prima del pass visivo unico. Exit criteria: **E7** (anteprima viva coerente col tipo) + **E16** parte meccanica (guard round-trip template↔builder, gemello di R7). Include la promozione di **R-builderCustomUnit** e **R-builderCustomScale**.

## Decisioni dell'autore (2026-08-11, brainstorm)

1. **Perimetro pieno**: anche schedule/streak/recovery diventano mattoni del builder — la parità coi template ufficiali si esprime **dentro il solo builder**, non "builder + Configura". Mattoni nuovi: Programma, Obiettivo, Sessione (+segmenti), serie nominate, unità/scale custom inline.
2. **Unità/scale custom staged**: create inline nel builder, restano nel draft in memoria e si materializzano **solo** al Lancia o al Salva template. Niente reference data orfana da builder abbandonato.
3. **Modello mentale**: *comporre e riconfigurare sono la stessa cosa in due momenti*. La superficie di composizione è una; oggi è ospitata nel dialog di creazione, la direzione di lungo periodo è aprirla anche su un habit esistente al posto delle sezioni di Configura. La 2.5 si progetta con questa direzione in mente (mattoni host-agnostici), senza eseguirla.
4. **Via la ComboBox Modo**: il modo sessione si promuove a mattone ("Sessione"); il cronometro semplice resta implicito nelle metriche Tempo. La ComboBox attuale non scrive nulla nella config (mapping "future work" mai fatto) — la 2.5 chiude quel filo.

## 1. Principio direttivo

**La composizione è un modello di prima classe, completo.** `HabitComposition` in Core (oggi: campi/contatore/astinenza/modo, serve alla grammatica del tipo) si estende — o si affianca con un `CompositionDraft` più ricco — finché non copre l'intera superficie di parità: programma (schedule/streak/recovery), goal, sessione+segmenti, ripetibile+nomi serie, gesto journal, metriche con unità/scale **incluse le definizioni staged** di quelle nuove.

- **I mattoni-VM sono viste sul draft, non figli del dialog.** Un brick-VM riceve il draft e lo muta; non chiama servizi, non assume che l'habit non esista. L'unico componente che sa di essere pre-persistenza è l'host (il dialog col suo Lancia atomico). Quando Configura aprirà la stessa superficie su un habit vivo, cambierà l'host (caricamento del draft da un habit + strategia di salvataggio), non i mattoni.
- **Lancia = un'unica richiesta atomica.** `ComposedHabitRequest` si estende a portare tutto (config completa, goal, segmenti, serie, ui_prefs, definizioni unità/scale staged) e `CreateComposedHabitAsync` materializza in **una transazione** (precedente CR7). Effetto collaterale: **W24-R1 si dissolve** — il gesto journal non si scrive più dopo la chiusura del dialog, viaggia dentro la richiesta.
- La firma della grammatica in Core perde `RecordingMode` (decisione 4); le derivazioni di tipo restano invariate a parità di mattoni.

## 2. I mattoni nuovi

Il canvas attuale resta com'è (palette campi Quantità/Tempo/Scala/Testo + contatore nudo + astinenza + ripetibile + gesto journal). I mattoni 1–3 vivono in una zona "composizione avanzata" sotto il canvas, coerente col Level 2 dell'editor (progressive disclosure). I valori esatti degli enum li prende il piano dal codice: sono quelli che Configura già offre.

1. **Programma** *(sempre presente, collassato)* — ogni abitudine ha un programma, quindi non si "aggiunge": riga compatta che collassata mostra il riassunto localizzato dei default ("Ogni giorno · recupero grace day"); espansa offre tipo di programma (con day-picker per i giorni specifici), modo streak, recupero. Default `Daily/Daily/GraceDay` (la forma dei seed). Chi non la tocca non la vede mai aperta.
2. **Obiettivo** *(opzionale)* — chip "+ Obiettivo" disponibile quando la composizione ha una metrica numerica o il contatore nudo. Campi: target numerico + periodo (`Open`/periodici). L'unità la eredita dalla metrica — non si sceglie due volte.
3. **Sessione** *(opzionale, assorbe il modo)* — aggiungerlo imposta `has_integrated_timer` + `timer_mode = Session` e apre l'editor segmenti: righe nome + interruttore lavoro/riposo, ordine = posizione in lista, default Work/Rest (come Workout log). Toglierlo azzera `has_integrated_timer`/`timer_mode` (niente timer integrato).
4. **Serie nominate** *(dentro Ripetibile)* — con "si può registrare più volte al giorno" acceso appare l'editor dei nomi serie: chip aggiungibili/rinominabili/rimovibili ("Notte", "Riposino"…). Vuoto = fallback "Serie 1/2/…" com'è oggi. Persistite in `series_labels_json`.
5. **Unità e scale nuove** *(nei picker esistenti)* — voce "+ Nuova unità…"/"+ Nuova scala…" in coda ai picker apre un micro-form inline: unità = nome + simbolo; scala = nome + etichette dei gradini (min 2). La definizione resta staged nel draft (decisione 2): compare nel picker marcata come nuova, è riusabile dagli altri campi della stessa composizione, si materializza al Lancia/Salva. Il gating `CanLaunch` si estende: scala nuova con etichette incomplete blocca come oggi blocca una scala non scelta.

## 3. Anteprima a 3 piani (E7)

**Struttura.** Pannello destro del dialog, tre riquadri impilati con etichetta: **"In Home"**, **"Oggi"**, **"Registrazione"**. Ogni riquadro renderizza il **componente reale** della superficie (2.1–2.3): template della riga Home (accent bar, chip cifra chiave, `HeatmapStrip` per-tipo), contenuto per-tipo della fascia `TodayStrip`, modulo di registrazione per-tipo. L'attuale `RecordingPreviewViewModel` (mock a celle) viene **sostituito** dal terzo riquadro. Riquadri **inerti** (niente click-through), ricostruiti a ogni `Recompute`.

**Dati d'esempio.** Un `SamplePreviewFactory` (WPF, niente DB) traduce il draft nei modelli che i componenti già consumano: streak ×5, serie di 7/14 giorni plausibili e **deterministiche** (stessa composizione → stessa anteprima), valore odierno campione formattato con l'unità vera della metrica (anche staged), label della scala vera (anche staged). Cifra chiave/striscia = default per-tipo della matrice Onda 2 §1: l'anteprima mostra ciò che l'utente vedrà davvero alla prima apertura.

**Vincoli fissati in spec** (lezioni già pagate):

1. **Niente WebView2 nell'anteprima, mai.** Journal con gesto `Diary` → rappresentazione statica dell'editor (card con titolo e righe fantasma), non un `MarkdownEditorControl`; `QuickNote` → il composer vero (XAML puro). Regola C1: `ContentControl` + selector, mai il solo binding null.
2. **Seam sui VM delle superfici.** I VM di produzione di fascia/quick-add si caricano via servizi DB-backed: l'anteprima non li istanzia via DI — la factory costruisce **istanze leggere alimentate a mano** (i controlli renderizzano da proprietà bindate). Dove un controllo è troppo accoppiato al suo VM di produzione, si estrae l'interfaccia visiva minima (è il lavoro "mattoni riusabili" del §1 applicato alle superfici).
3. **Resistance**: cronometro come valore campione **statico** ("20g 4:12"), niente tick — determinismo prima della scenografia.

## 4. Payload template v2 + round-trip E16

**Scrittore (builder).** `BuildTemplatePayloadJson` si estende finché non copre tutto il draft: `config` completa (programma, streak, recovery, `repeatable`, `series_labels`, `has_integrated_timer`, `timer_mode`), `goal`, `segments`, `ui_prefs`, metriche in ordine. Sezione nuova: **definizioni delle unità/scale staged** (`units`/`scales` con codice, nome, etichette) — un template che riferisce una scala creata inline deve portarla con sé, perché al salvataggio quella definizione non esiste nel DB. Le unità/scale di catalogo restano riferite per solo codice. Il codice di un'unità/scala custom è uno slug generato dal nome con suffisso anti-collisione — chiave di join, mai mostrato all'utente.

**Lettore (`InstantiateAsync`).** Già legge config/goal/segmenti/metriche. Si aggiunge **risolvi-o-crea**: unità/scala risolta per codice; se assente e il payload porta la definizione, si crea dentro la **stessa transazione** dell'instanziazione (CR7). Payload legacy senza definizioni = comportamento attuale, intatto (pattern I5: compat all'indietro nel lettore, mai backfill).

**Round-trip E16 — guard gemello di R7**, non eludibile aggiungendo template:

1. **Copertura**: percorre `HabitTemplateSeed.All`; per ogni template ufficiale esiste una fixture che costruisce la stessa composizione **via `HabitBuilderViewModel`** (mattoni programmatici). Template senza fixture → rosso. Chiave di payload sconosciuta al confronto → rosso. Una capacità aggiunta domani ai template senza il mattone corrispondente non può passare.
2. **Parità di scrittura**: il payload della fixture si confronta **semanticamente** (JSON parsato, non byte) col payload seed, campo per campo.
3. **Round-trip pieno**: composizione → salva template → `InstantiateAsync` su DB di test → rilettura di nodo/config/metriche/goal/segmenti/ui_prefs/etichette scala → confronto col draft. Copre scrittore+lettore come coppia.
4. **Composizione sintetica "tutto insieme"**: ogni mattone attivo, inclusa un'unità e una scala custom (nessun seed le usa) — unico punto che esercita risolvi-o-crea end-to-end.

Con questo, la matrice di parità dell'Onda 2.9 si riduce a: verificare la copertura del guard + spot-check a schermo (§1-bis punto 4).

## 5. Test per strato

- **Core**: grammatica aggiornata (via `RecordingMode`; sessione/segmenti nel draft) — derivazioni di tipo invariate a parità di mattoni; validazione draft (scala custom ≥2 etichette, slug anti-collisione).
- **WPF**: ogni brick-VM muta il draft e solo il draft; gating `CanLaunch` esteso (matrice dei casi bloccanti, guard **dentro** i metodi — `AsyncRelayCommand.ExecuteAsync` non consulta `CanExecute`, lezione R7); `SamplePreviewFactory` deterministico (5 tipi × gesto journal); anteprima ricostruita su ogni mutazione.
- **Data**: risolvi-o-crea in transazione unica (rollback se l'instanziazione fallisce a metà); payload legacy pre-2.5 instanziati identici a oggi; `CreateComposedHabitAsync` esteso atomico, gesto journal incluso (chiude W24-R1 by design).
- **Anti-WebView2**: la view del builder con composizione journal `Diary` non contiene **nessun** `MarkdownEditorControl`/WebView2 (`FindDescendants` sull'intera view). **Nello stesso giro si adjudica W24-Q1**: scan allargato all'intera `HabitScreen` col pannello Note chiuso, in `HabitScreenDiarioColumnViewTests` — decide se la promessa "niente WebView2 per QuickNote" è vera o se il secondo editor `Collapsed` di `NotesTabView` la falsifica.
- Round-trip E16: §4.

## 6. Fuori scope (esplicito)

Aprire la superficie di composizione su un habit esistente (direzione dichiarata al §1, non lavoro 2.5 — i vincoli dei dati accumulati, il cambio di tipo derivato, autosave-vs-atomicità e `HabitConfigHistory` sono un progetto a sé, candidato naturale post-dogfooding); smontare/migrare le sezioni di Configura; questioni-modello CM-1…CM-4 (parcheggiate al dogfooding — il builder replica la semantica attuale del ripetibile senza adjudicarla); W24-R2/R3 (test-debt, giro fix post-2.5); E14 notifiche; pass visivo (unico, a valle della 2.5, con checklist scritta obbligatoria come da mappa §1-bis).

## 7. Note per gli esecutori

Da mappa §1-bis (nota esecutori) + CLAUDE.md §8, i vincoli che toccano questa onda da vicino:

- **Mai `Clear()` su una collection che è `ItemsSource` bindata** senza sopprimere la persistenza per tutta l'operazione e ri-affermare il valore dopo (data-wipe `4d01f9c`, ricomparso in `1d96967`) — i picker unità/scala con voci staged toccano esattamente quel filo.
- **`ContentControl` con `ContentTemplate` statico sempre accoppiato a un selector o guard** (C1, Onda 2.4) — l'anteprima Registrazione per-tipo è un uso da manuale.
- Stringhe nuove = 3 edit (`Strings.resx` + `Strings.it.resx` + `Strings.Designer.cs` **via shell**).
- Run WPF serializzato se la suite si appende sulla VM (W24-E1: `-- xUnit.ParallelizeTestCollections=false`).
- Nessuna migrazione schema prevista: la 2.5 non tocca colonne (tutto ciò che persiste esiste già: config v2.14/2.15, `ui_prefs_json`, reference data).
