# R9 — Il giorno di appartenenza: da UTC a "locale + `reset_time`"

**Data**: 2026-08-19
**Stato**: spec approvata. **Da eseguire prima di E14** (decisione dell'autore), quindi prima del dogfooding.
**Finding**: R9 (+ R10, che ne è un campione) in `docs/phase-4-issues.md`.
**Consumatore a valle**: `2026-08-19-calendar-lens-design.md` §7 assume questa spec risolta.

---

## 1. Il problema, misurato nel codice

Il tracker diceva "`reset_time` non lo consuma nessuno". È vero, ma sottostima: **il giorno di appartenenza di una registrazione è il giorno UTC**.

Cosa fa il codice oggi:

| dove | cosa |
|---|---|
| scrittura | `CompletedAt = clock.UtcNow.UtcDateTime` (`CounterQuickActionHandler.cs:24`, `ActionTodayContent.cs:112`, `StateTodayContent.cs:132`, …) |
| scrittura retroattiva | il giorno D si salva come **D 00:00 UTC** (`QuickAddViewModelBase.cs:48-57`) |
| lettura / bucketing | `DateOnly.FromDateTime(CompletedAt)` — data **UTC** — in `CompletionService` (135, 162, 205), `TrendQueryService:34`, `ChartQueryService`, `ExportQueryService`, `SchedulingAwareStreakAggregator` (96, 194, 213, 217) |

Il sistema è **coerente in UTC**: è la ragione per cui finora non ha prodotto risultati palesemente assurdi. Ma UTC non è il giorno dell'utente. In Italia d'estate (UTC+2) **una registrazione fatta alle 00:30 del 19 finisce nel 18** — nello streak, nella heatmap, nel calendario, nell'andamento e nell'export. Non è un difetto di resa: è ciò che viene aggregato.

**Lo scisma visibile**: su 55 punti che derivano un giorno da un istante, **14 calcolano "oggi" in UTC e 9 in locale** (`CounterTodayContent`, `StateTodayContent`, `ActionTodayContent`, `CalendarNavigatorViewModel`). Fra mezzanotte e le 02:00 la striscia "Oggi" e l'evidenziazione del calendario indicano il giorno D mentre ciò che si registra atterra su D−1. **R10 non era un caso isolato: era un campione di questa famiglia.**

`reset_time` è la rifinitura configurabile di un confine che non è ancora nemmeno locale. `IResetTimeResolver` è registrato in DI (`ServiceRegistrar.cs:112`) e non ha un solo chiamante; `docs/MILESTONES.md:152` lo dichiara rimandato a una milestone mai arrivata.

## 2. Decisione

**Un solo punto decide a quale giorno appartiene un istante, per una data abitudine: ora locale + cascata `reset_time`.** Tutti i consumatori chiamano quello.

Avendo il risolutore, onorare `reset_time` costa quasi nulla in più rispetto al solo passaggio a locale — motivo per cui le due cose si fanno insieme invece di rimandare la seconda.

## 3. Forma: un valore risolto una volta, poi funzione pura

Scartate due alternative:

- **Servizio asincrono chiamato ovunque** (`GetDayAsync(habitId, istante)`): i siti di bucketing stanno **dentro cicli** e in converter/VM dove `async` non entra pulito — diventa una query per riga.
- **Congelare il giorno alla scrittura** con una colonna `completed_on`: letture banali e indicizzabili, ma cambiare `reset_time` richiederebbe di riscrivere lo storico. Cuoce nei dati una decisione che deve restare interpretabile.

### 3.1 `DayBoundary` (Core, puro)

Un valore leggero, senza dipendenze, con metodi **sincroni e puri**:

- costruito da un `TimeZoneInfo` + un `TimeOnly` (l'orario di reset effettivo);
- `DayOf(DateTimeOffset instant) → DateOnly` — il giorno di appartenenza;
- `Today(IClock) → DateOnly` — l'unica definizione di "oggi" dell'app;
- `RangeOf(DateOnly day) → (DateTimeOffset from, DateTimeOffset toExclusive)` — per le query che filtrano per intervallo invece che raggruppare in memoria.

Vive in `Recordum.Core`: è la sede della logica di dominio senza dipendenze, ed è ciò che rende i casi limite (mezzanotte, reset alle 04:00, cambio d'ora) provabili **senza database**.

### 3.2 `IResetTimeResolver` resta il produttore

Cammina già la cascata habit → gruppi antenati → `AppSetting daily_reset_time` → `00:00`. Gli si aggiunge:

- un metodo che restituisce direttamente un `DayBoundary` per un'abitudine;
- una **versione batch** per più abitudini in una sola passata. Senza questa, le aggregazioni multi-abitudine (albero Home, export libreria, grafici di libreria) tornano a N query e mangiano il budget di 500 ms della Home.

### 3.3 Come lo usano i consumatori

Si risolve **una volta** in cima all'operazione (una query di servizio, il caricamento di una schermata), poi i cicli chiamano la funzione pura. Nessun `await` per riga.

## 4. Portata: l'audit dei siti

I 55 punti `DateOnly.FromDateTime` **non vanno convertiti in blocco**: alcuni sono conversioni di presentazione legittime (converter di binding, ordinamento di note per `CreatedAt`). Il piano deve produrre l'elenco completo con, per ciascuno, una delle tre etichette: **converti**, **lascia (è presentazione)**, **lascia (è già un `DateOnly` di dominio)** — e la ragione. Un sito non classificato è un sito dimenticato.

Aree che sicuramente convertono:

- **Streak**: `SchedulingAwareStreakAggregator` (giorni completati, "oggi", `StreakBrokenAt`, periodi di astinenza).
- **Heatmap e calendario**: `CompletionService.GetDailyMagnitudeAsync` e i due fratelli, `YearHeatmapViewModel`, `CalendarNavigatorViewModel`.
- **Andamento e grafici**: `TrendQueryService`, `ChartQueryService`.
- **Export**: `ExportQueryService` (intervalli e periodi di astinenza).
- **"Oggi"**: le strisce per-tipo, `HomeRowViewModel`, `GoalProgressCardViewModel`, `StreakSummaryViewModel`, `HourlyHistogramViewModel`, `StreakTimelineViewModel`, `ExportDialogViewModel`.
- **Astinenza**: i periodi bucketizzano `StartedAtMs`/`EndedAtMs` in data UTC negli stessi punti.

**Chiude R10** per costruzione: non resta un secondo modo di calcolare "oggi".

## 5. Le registrazioni retroattive

**Scrittura nuova**: una retroattiva per il giorno D si salva a **mezzogiorno locale di D** (convertito in UTC per lo storage). Sta comodamente dentro il giorno per qualunque reset plausibile (00:00–06:00) e regge il cambio d'ora. Oggi si salva a D 00:00 UTC, che sotto il nuovo confine può cadere nel giorno prima.

**Storico esistente** (decisione dell'autore, 2026-08-19): **sì al backfill**. Le sole righe con `IsRetroactive = true` vengono ri-stampigliate al mezzogiorno locale del giorno che già dichiarano. Non cambia il significato: lo **preserva**, perché quel giorno l'aveva scelto l'utente a mano. È l'eccezione motivata alla regola "nessun backfill" applicata a unità durata e metriche fantasma — lì il backfill avrebbe *inventato* un dato mancante, qui impedisce a un dato esplicito di spostarsi.

**Dove gira il backfill**: **non** in una migration SQL. Calcolare "mezzogiorno locale" richiede `TimeZoneInfo` e la gestione storica dell'ora legale; `datetime(…, 'localtime')` di SQLite è inaffidabile sulle date passate. Va fatto come **passo di manutenzione una tantum allo startup** (`ApplicationInitializer`), idempotente e guardato da un `AppSetting` flag, dopo l'applicazione delle migrazioni. Le registrazioni **non** retroattive non si toccano: l'istante è l'istante, e la rilettura le colloca da sé.

**Disciplina dati** (E21): provato su una **copia** del DB reale prima dell'originale, `.recbak` manuale prima di lanciare la build.

## 6. Cosa NON cambia

- **Il formato di storage.** Si continua a salvare istanti (`CompletedAt` UTC + `CompletedAtMs`). Nessuna colonna `completed_on`, nessuna migrazione di schema — `schema_version` resta 2.17.
- **La cascata `reset_time`.** Regole, precedenza e default (`00:00`) restano quelli documentati; cambia solo che qualcuno finalmente li legge.
- **`reset_time` non entra in `HabitConfigHistory`** (resta fra le colonne escluse): è un setting, non uno stato versionato. Conseguenza da accettare e dichiarare: cambiare il reset **ri-interpreta anche lo storico**, non solo il futuro. È il prezzo della strada scelta (§3, alternativa C scartata), ed è coerente col principio "i dati sono istanti, il giorno è un'interpretazione".

## 7. Test

La funzione pura è la sede della maggior parte:

- mezzanotte esatta, un minuto prima, un minuto dopo, con reset `00:00`;
- reset `04:00`: le 02:30 appartengono al giorno **prima**; le 04:00 in punto al giorno **dopo** (il confine è inclusivo in avanti);
- **cambio d'ora**: il giorno di 23 ore e quello di 25 ore, in entrambe le direzioni, con reset dentro e fuori l'ora saltata;
- fusi negativi (UTC−5) oltre a quello locale, così la logica non è tarata sull'Italia.

Sui consumatori:

- una registrazione alle 00:30 locali con reset `00:00` cade su **oggi** in streak, heatmap, calendario, andamento ed export — **lo stesso** giorno in tutti e cinque (è il test che dimostra che il punto di verità è uno solo);
- lo stesso istante con reset `04:00` cade su **ieri** nei medesimi cinque;
- una retroattiva nuova per D resta su D con reset `00:00` e con reset `06:00`;
- il backfill è **idempotente** (girato due volte non sposta nulla) e non tocca le righe non retroattive;
- perf: l'albero Home con N abitudini risolve i confini in **una** passata batch, non N.

**Ogni test di regressione va provato contro il codice rotto** (CLAUDE.md §8): disattivare la conversione, vedere il rosso, ripristinare. È esattamente la trappola in cui sono caduti W24-R2/R3 — test verdi che non sarebbero mai falliti.

## 8. Rischi

1. **Il rischio più grosso è lo storico che si ri-interpreta.** Passando da UTC a locale, ogni registrazione fatta fra mezzanotte e l'offset locale **cambia giorno** — e con essa streak, heatmap ed export dello storico. È corretto (prima erano nel giorno sbagliato), ma va detto, non scoperto: un numero di streak può cambiare al primo avvio dopo l'aggiornamento.
2. **Backfill su dati veri.** Vedi §5: copia + `.recbak`.
3. **Cambio d'ora.** È la classe di bug che questa spec può introdurre, ed è la ragione per cui la logica è pura e testata a parte.
4. **Perf.** Senza la batch, le aggregazioni multi-abitudine regrediscono. Il budget Home <500 ms è un exit criteria (E20).

## 9. Collocazione

Prima di **E14** e quindi prima del dogfooding, per decisione dell'autore del 2026-08-19: i dati delle prossime settimane sono reali, e questo difetto li sporcherebbe in silenzio. La mappa (`2026-07-03-v1-redefinition-plan-design.md`) collocava reset-time nell'Onda 2.9; questa spec lo anticipa e la 2.9 eredita la verifica a schermo nella matrice di parità.
