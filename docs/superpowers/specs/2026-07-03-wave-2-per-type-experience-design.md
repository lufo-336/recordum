# Onda 2 — Esperienza per-tipo (design)

**Data**: 2026-07-03 · **Stato**: approvato dall'autore (brainstorm 2026-07-03, con mockup nel visual companion — file in `.superpowers/brainstorm/444-1783081598/content/`)
**Contesto**: piano v1.0 (`2026-07-03-v1-redefinition-plan-design.md`, Onda 2). Chiude **U6** (quadratini Home type-aware), **U10** (landing "qui non c'è niente"), **U8** (serie opzionale + unità tempo + tasto "1"), il pilastro **builder con anteprima** ("perché contatore dovrebbe essere un mero checkbox?"), più i findings smistati qui dall'Onda 0: **F0-11** (Configura con enum grezzi) e **selezione-vs-completato** nel calendario (ex F0-13). Prepara la casa di **U7** (food diary, che consuma le foto dell'Onda 1b). Exit criteria: **E6**, **E7**.

**Principio**: ogni tipo di habit merita la sua faccia. Le 4 superfici (riga Home, fascia "Oggi", registrazione, anteprima builder) condividono gli stessi componenti per-tipo — si costruiscono una volta e si riusano.

## Stato attuale (verificato 2026-07-03)

- Riga Home (`HabitRowTemplate.xaml`): accent bar + icona + nome + label tipo + `HeatmapStrip` bound `CompletionsLast7Days` (identica per tutti i tipi) + "+" generico.
- Colonne aperte: AppSetting **globale** `ui.open_columns` (`HabitScreenViewModel.OpenColumnsSettingKey`) → aprire un'abitudine può mostrare il vuoto (root cause di U10).
- Quick-add: VM per-tipo già esistenti (`ActionQuickAddViewModel`, `CounterSeriesQuickAddViewModel`, `AbstinenceQuickAddViewModel`, …); il selettore serie è un bottone numerato muto (U8).
- Builder (`HabitBuilderView.xaml` + `HabitBuilderViewModel`): mattoni-campo, **nessuna anteprima**.

## Decisioni dell'autore (2026-07-03, via mockup)

1. **Riga Home**: componibile = tutti e tre i design proposti come *pezzi* — striscia specializzata + cifra chiave + cronometro vivo per resistance. **Due manopole per abitudine** (cifra chiave: 2-3 scelte per tipo o nessuna; striscia: nessuna/7/14 giorni). Default per-tipo.
2. **Landing**: **fascia "Oggi" per-tipo sopra le colonne** + colonne di default per-tipo alla prima apertura, poi ricordate per-abitudine.
3. **Registrazione counter**: check esplicito nel builder "Si può registrare più volte al giorno"; non ripetibile → una registrazione al giorno, la seconda **somma**; ripetibile → chip serie **nominate e rinominabili dall'utente**; metriche Tempo con **selettore unità** al momento della registrazione.
4. **Builder**: **anteprima a 3 piani** (riga Home + fascia Oggi + modulo di registrazione), componenti veri con dati d'esempio.

## 1. La matrice per-tipo

| Tipo | Riga Home — cifra chiave (default in grassetto) | Riga Home — striscia (stile fisso) | Fascia "Oggi" | Registrazione |
|---|---|---|---|---|
| **Action** | **streak** (`×12`) · fatto-oggi ✓ · nessuna | quadretti pieni/vuoti | ✓ **Segna come fatto** (toggle) · streak · % ultimi 30g · fascia oraria abituale | invariata |
| **Counter** | **totale oggi** (`22 pg`, con unità) · streak · nessuna | mini-barre altezza-valore (vs max periodo) | **+ Registra** (focus sul modulo) · totale oggi · progresso goal se attivo | §3 |
| **Resistance** | **cronometro vivo** (`20g 4:12`) · giorni puliti · nessuna | banda continua con strappi alle ricadute | **cronometro grande** (`LiveElapsedClock`) · Inizio/Ricaduta · tentativi (90g) | invariata (U9/U9.1) |
| **Journal** | **oggi ✓/—** · voci questa settimana · nessuna | punti (giorno con voce/nota) | gesto primario configurabile: **Scrivi la voce di oggi** *oppure* **+ Nota veloce (con foto)** — il template lo pre-imposta (food diary → nota veloce) **e l'utente lo cambia in Configura** (principio "template = composizione salvata": nessuna capability solo-template) | editor diario / nota veloce (+ foto da Onda 1b) |
| **State** | **valore di oggi** (label scala, es. `Bene`) · media 7g · nessuna | celle colorate dal valore scala (ramp) | slider rapido di oggi · ultimo valore registrato | invariata (slider U3) |

Gruppi: invariati (nessuna fascia/striscia per-tipo; fuori scope).

## 2. Riga Home componibile (U6)

- Zona destra della riga = `[chip cifra chiave] [striscia]`, entrambe opzionali. Il "+" quick action resta.
- Manopole in **Configura → nuova sezione "Riga Home"** (2 ComboBox localizzate; questa sezione è anche il veicolo del fix **F0-11**: la testata del tab Configura si ripulisce — via i ComboBox con enum grezzi "Habit"/"Action", si mostrano tipo e nome come testo localizzato non editabile).
- Il **cronometro in riga** (resistance) si aggiorna con un tick condiviso (un solo `DispatcherTimer` per la Home, non uno per riga; riusare `LiveElapsedClock` con formato compatto senza secondi in riga, es. `20g 4:12`).
- Dati: `HomeTreeRow` va esteso con i campi per-tipo necessari (valore odierno, label scala di oggi, stato periodi resistance, serie 14 giorni quando richiesta) — estensione della query home in `IHabitTreeQueryService`, budget perf invariato (<500ms, `HomeTreeBudgetTests` resta il guardiano; le strisce a 14 giorni si caricano nella stessa query, non per-riga).

## 3. Registrazione (U8)

- **`repeatable`** (bool, per-habit): spento di default; check nel builder ("Si può registrare più volte al giorno") e in Configura. Template seed aggiornati (Sleep → on).
- **Non ripetibile**: modulo = campi metrica + Salva. Una seconda registrazione nello stesso giorno **somma** sul totale del giorno (per tutte le nature di metrica; la scala, che è un valore puntuale, resta "ultima vince" — unica eccezione, da esplicitare nella UI del giorno).
- **Ripetibile**: chip serie in testa al modulo — nomi da `series_labels_json` se presenti (template li seminano: "Notte", "Riposino"), fallback "Serie 1/2/…"; **rinominabili inline** (doppio click/matita) con persistenza per-habit; "+ Aggiungi" crea la serie successiva. Via il bottone numerato muto.
- **Unità tempo**: campi di natura Tempo mostrano il selettore unità (ore/min/sec, default = unità del `MetricDefinition` se impostata) accanto al valore; il valore si normalizza in **secondi** al salvataggio (nessun cambio schema: `value_seconds` già esiste). Le visualizzazioni (records, export) mostrano l'unità scelta sul metric.

## 4. Fascia "Oggi" + colonne per-tipo (U10)

- Nuovo controllo `TodayStrip` (nome indicativo) in testa a `HabitScreen`, sopra i toggle colonna: contenuto per-tipo dalla matrice, sempre visibile (anche a zero colonne aperte — il "qui non c'è niente" diventa impossibile).
- Il cronometro d'astinenza "primario" vive qui (il quick-add in Cronologia resta per il retroattivo/il giorno selezionato).
- **Colonne di default per-tipo** alla prima apertura: journal → Diario; tutti gli altri → Cronologia. Dopo la prima apertura, i toggle si persistono **per-abitudine** (in `ui_prefs_json`, vedi §6); l'AppSetting globale `ui.open_columns` resta come fallback legacy per habit senza prefs.
- **Calendario — selezione vs completato** (ex F0-13): il giorno selezionato si distingue con **anello/bordo**, il completato con **fill**; mai più ambigui quando il colore habit è verde.

## 5. Builder: anteprima a 3 piani

- Pannello destro del builder: **(1)** riga Home, **(2)** fascia "Oggi", **(3)** modulo di registrazione — renderizzati dai **componenti reali** delle superfici (stessi UserControl/VM alimentati da un modello d'esempio), aggiornati a ogni modifica di nome/colore/icona/mattoni/ripetibile.
- Dati d'esempio deterministici (streak ×5, 7 giorni plausibili, valore campione per metrica) generati da un piccolo provider condiviso, NON dal DB.
- Il check "ripetibile" è un mattone visibile del builder (§3).
- Nessun salvataggio finché non si Lancia (comportamento invariato).

## 6. Persistenza — una sola migrazione additiva (schema v2.13)

- **`habit_node.ui_prefs_json TEXT NULL`** — JSON con: cifra chiave scelta, striscia (none/7/14), colonne aperte per-abitudine, gesto primario journal. `NULL` = default del tipo. Presentazione per-nodo (precedente: `pin_scope`); **non** entra in `HabitConfigHistory`; **non** ha provenance (già coperto dal nodo). **Governance** (2026-07-03): ogni chiave del JSON va documentata in `docs/ARCHITECTURE.md` (tabella chiave→significato→onda che l'ha introdotta) — il campo non deve diventare una discarica; le onde successive (5: `companion_counter_id`, routine mode) rispettano la stessa regola.
- **`habit_config.repeatable INTEGER NOT NULL DEFAULT 0`** e **`habit_config.series_labels_json TEXT NULL`** — semantica di registrazione ⇒ **versionate**: colonne mirror in `habit_config_history` + snapshot interceptor aggiornato (NON vanno in `HabitConfigHistoryColumns.Excluded`).
- **Migrazione data-aware**: habit con almeno una `CompletionValue.SeriesIndex > 0` → `repeatable = 1` (nessun cambio di comportamento per dati esistenti). Bump `schema_version` a `2.13` (+ seed `AppSettingSeed` allineato).
- Fluent API esplicita per tutto (indici/CHECK dove servono: `repeatable IN (0,1)`).

## 7. Ordine di esecuzione (plan separati, scritti a ridosso)

1. **2.0 — Migrazione v2.13** + servizi prefs (lettura/scrittura `ui_prefs_json`, `repeatable`, `series_labels_json`).
2. **2.1 — U6 riga Home** (strisce per-tipo, chip cifra, tick condiviso, sezione Configura "Riga Home" + fix F0-11).
3. **2.2 — U10 fascia "Oggi"** + colonne default per-tipo + selezione-vs-completato calendario.
4. **2.3 — U8 registrazione** (repeatable, somma, chip nominate, unità tempo).
5. **2.4 — U7 food diary** (journal note-first: gesto primario nota veloce + foto — richiede Onda 1b eseguita; template "Food diary" riallineato).
6. **2.5 — Builder anteprima a 3 piani + parità piena** (per ultimo: riusa i componenti costruiti in 2.1–2.3). **Aggiunta 2026-07-03 (decisione autore)**: include la promozione di **R-builderCustomUnit** e **R-builderCustomScale** — dal builder si creano inline unità di misura e scale personalizzate, così tutto ciò che un template contiene è ricostruibile da "crea una nuova abitudine" (exit criterion E16; la verifica è la matrice di parità dell'Onda 2.9).

## 8. Test

- Data: migrazione v2.13 (colonne + data-aware repeatable + history mirror + seed 2.13); query home estesa (per-tipo, budget perf verde); somma-nel-giorno per non-repeatable; series labels CRUD.
- WPF: VM riga per-tipo (cifra/striscia/cronometro compatto); `TodayStrip` per i 5 tipi; colonne default per-tipo alla prima apertura + persistenza per-habit; chip serie (nomina/rinomina/aggiungi); selettore unità tempo (normalizzazione secondi); anteprima builder reagisce ai mattoni; calendario anello-vs-fill.
- Verifica visiva finale dell'autore su tutte le superfici, tipo per tipo (E6/E7).

## 9. Fuori scope (esplicito)

Griglia widget libera in riga (bocciata: due manopole bastano); fascia "Oggi" per i **gruppi**; drag-ordering delle serie; conversioni unità oltre ore/min/sec; ridisegno del nav-model a colonne (spec parcheggiato preesistente); Home "type-aware" oltre la zona destra della riga.
