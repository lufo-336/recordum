# Onda 2.4 — U7 Food diary, journal note-first (design)

**Data**: 2026-08-08 · **Stato**: approvato dall'autore (brainstorm 2026-08-08, mockup delle tre alternative in `docs/design/food-diary-composer-preview.html`)
**Contesto**: spec dell'Onda 2 (`2026-07-03-wave-2-per-type-experience-design.md`, §1 riga Journal e §7 punto 5). Chiude **U7**. Richiede l'Onda 1b (allegati foto) e l'Onda 2.0 (`ui_prefs_json`), entrambe in `main`. Exit criteria: **E6**, **E7**.

**Principio**: un diario alimentare si tiene in cinque righe brevi al giorno, non in cinque temi. Il gesto primario deve costare un clic e una frase — e la giornata deve risultare *fatta*.

## Stato attuale (verificato 2026-08-08)

- `JournalTodayContent` legge già `HabitUiPrefs.JournalGesture` (`Diary` | `QuickNote`) ma **si limita ad aprire una colonna**; il suo stesso commento rimanda a quest'onda per il flusso vero.
- `JournalGesture` **non è scrivibile da nessuna parte**: nessuna UI in Configura, e `TemplatePayload` (node/config/metrics/segments/goal) non ha una sezione `ui_prefs`.
- Le foto esistono (Onda 1b): `IAttachmentService`, `AttachmentViewModel`, galleria + lightbox in `NotesTabView`. Si allegano però solo **dopo** aver selezionato una nota già esistente.
- La colonna ✎ Diario ospita `JournalQuickAddViewModel` dentro `JournalDiarioTemplate`: intestazione "Registrazione per: *data*" + `MarkdownEditorControl` (WebView2) + Salva.
- `DiaryService.SaveEntryAsync` crea Nota(`Diario`) + Completion in un solo `SaveChanges`. Una nota `Veloce` non crea nessuna completion.
- Seed: `Food diary` è un habit **Journal** con tre `MetricDefinition` (`Food name` Text, `Grams` g, `Calories` kcal) e la descrizione parla di "multi-series entries". Su un journal quelle metriche non hanno **nessuna** superficie di inserimento.

## Decisioni dell'autore (2026-08-08)

1. **La nota veloce è una voce di diario** (Nota `Diario` + Completion via `IDiaryService`), con un'interfaccia da nota rapida. Non si inventa semantica nuova e non si fa dipendere il modello dati da una preferenza di presentazione: streak, heatmap e cifra chiave funzionano perché esiste la completion.
2. **Il composer vive in cima alla colonna Diario**, con sotto le voci del giorno. La fascia "Oggi" resta un'affordance che porta alla superficie giusta col cursore pronto — non diventa essa stessa un modulo d'inserimento.
3. **Il template Food diary perde le tre metriche fantasma** e diventa un journal onesto: testo + foto. Il conteggio calorie resta possibile come composizione counter ripetibile costruita dall'utente (dall'Onda 2.3 ha serie nominate e somma; dalla 2.5 il builder saprà creare unità e scale inline).
4. **Modifica in riga, editor Markdown mai** per le abitudini a nota veloce. Il gesto *è* la scelta fra breve e lungo, ed è già una manopola esplicita in Configura: non servono due modi di scrivere la stessa cosa.

## 1. Modello e flusso dati

Nessun cambio **strutturale** di schema: nessuna colonna nuova, nessuna tabella nuova. La sola migration è data-only e riscrive due payload seed (§4).

- **Salvataggio**: `IDiaryService.SaveEntryAsync(habitId, day, text)` → Nota(`Diario`) + Completion, atomiche in un solo `SaveChanges`. Il servizio non ha vincoli di unicità per giorno: N voci al giorno funzionano già.
- **Foto**: i percorsi scelti restano **in memoria** finché non si salva; al Save si ottiene il `noteId` e si chiama `IAttachmentService.AddAsync` per ciascuno. L'ordine è obbligato: allegare prima significherebbe creare una nota-bozza che, se abbandoni, resta orfana.
- **Data**: dal `CompletionDate` già esposto dal pannello Diario e pilotato dalla selezione del calendario di Cronologia. Il retroattivo non costa niente e resta coerente con le altre colonne.
- **Eliminazione**: `IDiaryService.DeleteEntryAsync`, che già rimuove nota + completion + allegati + `NoteHistory` in transazione.

## 2. La superficie — composer nella colonna Diario

Nuovo `UserControl` + VM (`JournalQuickEntryViewModel`, nome indicativo), ospitato dalla colonna ✎ Diario.

- **Composer**: campo di testo multi-riga che cresce — `TextBox`, **non** WebView2 — riga foto con miniature degli allegati in attesa e "＋ Foto" (`IOpenFileDialog`, già iniettato in `NotesViewModel`), pulsante Salva. `Ctrl+Invio` salva.
- **Voci del giorno**: sotto il composer, righe-scheda (ora · testo · miniature) per il giorno selezionato. Clic su una riga → **modifica in riga** con lo stesso campo e le stesse foto; Salva ed Elimina, quest'ultimo dietro `IConfirmationDialog` (stesso trattamento delle "Registrazioni" dell'astinenza).
- **Scelta del contenuto della colonna**: gesto `QuickNote` → composer; gesto `Diary` → l'attuale `JournalDiarioTemplate`. Su un'abitudine a nota veloce **il WebView2 non viene istanziato affatto**.
- **Riuso**: `AttachmentViewModel` è già la vista di un allegato — il composer lo riusa invece di duplicarlo. Le stesse foto restano visibili nella galleria + lightbox della colonna Note.
- La riga Home e la cifra chiave dei journal non richiedono lavoro: contano completion, e ora ogni nota veloce ne produce una.

## 3. Dove si sceglie il gesto

- Il gesto va nella sezione **Registrazione** di Configura: `RegistrationSectionViewModel.IsApplicable` passa da Counter-only a Counter *o* Journal; per i journal mostra una ComboBox localizzata (Scrivi la voce di oggi / Nota veloce), per i counter resta il check "ripetibile". La sezione prende `IHabitUiPrefsService` come seconda dipendenza, esattamente come già fa la sezione Riga Home.
- **Lì e non in "Riga Home"** perché il gesto è *come si registra*, non come si presenta la riga.
- `JournalTodayContent` non cambia semantica: continua a chiedere l'apertura di una colonna. Cambia però la destinazione — oggi `OpenQuickNote` mappa su `HabitColumn.Note`, deve mappare su `HabitColumn.Diario`, dove ora vive il composer.

## 4. Template

- **`FoodDiaryPayload`**: via le tre metriche, descrizione riscritta (niente più "multi-series entries"), aggiunta `"ui_prefs":{"journal_gesture":"quick_note"}`.
- **`DailyJournalPayload`**: `"journal_gesture":"diary"` dichiarato esplicitamente anche se è già il default — esplicito > implicito, e i due template journal spediti vogliono gesti diversi: è questo il fatto che impone di leggere il gesto dal payload invece che da un default di tipo.
- **`TemplatePayload`** guadagna una sezione `ui_prefs`; `HabitTemplateService` la applica via `IHabitUiPrefsService` dopo aver creato il nodo.
- **`SaveAsTemplateAsync` deve riportare indietro i `ui_prefs`.** Senza, "template = composizione salvata" si rompe di nuovo: è la stessa classe di bug di R7, un giro salva→istanzia che smarrisce una proprietà.
- **Migration data-only `FoodDiaryQuickNoteV217`** (schema **2.17**) che riscrive i payload dei due template seed. Due gotcha noti: `#pragma warning disable CA1861` va aggiunto a mano; il bump di `schema_version` tocca 8 riferimenti nei test, **nomi di metodo inclusi**.
- **Nessun backfill**: le abitudini già istanziate dal vecchio template conservano metriche fantasma e gesto di default. Sono dati utente, e si sistemano da Configura — stessa scelta fatta per R7 e per l'unità durata.
- `docs/ARCHITECTURE.md`: la tabella delle chiavi `ui_prefs_json` guadagna la riga `journal_gesture` (Onda 2.4).

## 5. Errori e casi limite

- Niente testo **e** niente foto → non si salva, messaggio localizzato sullo stile di `QuickAdd_Error_NotesRequired`.
- Testo vuoto **con** almeno una foto → **si salva**: per un diario alimentare la foto del piatto è la registrazione.
- File illeggibile o sparito → la voce resta salvata, quella foto si segnala, il batch non aborta (comportamento già adottato da `NotesViewModel.AddAttachment`).
- Eliminazione di una voce → sempre dietro conferma.
- Cambio di abitudine mentre un salvataggio è in volo → la voce va comunque all'abitudine per cui era stata scritta, e non compare in una lista che ora rappresenta un'altra abitudine (stessa guardia di `LoadAttachmentsAsync`).

## 6. Test

**Data**
- Payload del template senza metriche e con `ui_prefs`; la materializzazione scrive `ui_prefs_json` sul nodo.
- Round-trip `SaveAsTemplateAsync` → payload che conserva i `ui_prefs`.
- Migration `FoodDiaryQuickNoteV217`: payload riscritti + `schema_version` a 2.17.
- **Test di guardia su tutti i template ufficiali**: nessun template journal dichiara metriche. Gemello di quello scritto per R7 sulle durate senza unità.

**WPF**
- Salvataggio → nota `Diario` + completion; niente salvataggio con campo e foto entrambi vuoti.
- Foto in attesa allegate **dopo** il salvataggio; abbandonare il composer non lascia nota né file.
- Modifica in riga di una voce esistente; eliminazione dietro conferma.
- La colonna Diario sceglie composer o editor Markdown secondo il gesto.
- Sezione Registrazione visibile sui journal e scrittura effettiva del gesto in `ui_prefs_json`.
- La fascia "Oggi" con gesto nota veloce apre **Diario**.

**Verifica visiva**: rimandata al pass unico di fine Onda 2, a valle della 2.5 — decisione dell'autore del 2026-08-08, coerente con §8 della spec dell'Onda 2.

## 7. Fuori scope (esplicito)

Modulo metriche per gli habit journal (chiuderebbe il buco alla radice, ma è una superficie di registrazione nuova che si mangia mezza onda); template counter ufficiale per il conteggio calorie (→ Onda 5, dopo che il builder della 2.5 avrà reso quella composizione costruibile a mano); fotocamera; ritaglio o rotazione delle immagini; drag&drop di file nel composer; qualunque OCR o riconoscimento del piatto.
