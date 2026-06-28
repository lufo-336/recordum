# U9 — Astinenza a stato esplicito (Stop smoking) — design

> **Data**: 2026-06-28 · **Branch**: `fase-4-polish` · **Tipo**: feature + evoluzione di componente core (motore streak, quick-add, calendario, schema additivo).
>
> **Fonti autoritative**: `Recordum_ProductSpec_v2.docx` (tipi habit); spec R-multiValue `2026-06-12-multivalue-habits-revision-design.md` (Resistance = astinenza pura, "completare = ricaduta"); spec astinenza precedente `2026-06-14-abstinence-streak-engine-design.md` (modello **continuo**, qui **superato**). Tracker: `docs/phase-4-issues.md` (U9 / B-abstinence). Convenzioni schema/codice: `CLAUDE.md` §6, §8.

## 1. Problema

Il tipo `Resistance` modella l'astinenza ("niente fumo"). Il modello **continuo** attuale (spec 2026-06-14) tratta la streak come "giorni puliti dall'ultima ricaduta", sempre in conteggio dalla creazione: una ricaduta è un `Completion` e la run pulita riparte **automaticamente** il giorno dopo. Funziona, ma:

1. **UX poco chiara** (lamentela dell'autore, U9): registrare una ricaduta passa per il quick-add generico (`MeasuredQuickAddViewModel`) con campi metrica — non c'è un'azione "Ricaduta" evidente né un cronometro d'astinenza esplicito.
2. **Bug latente sulle ricadute pluri-giorno**: nel modello continuo la run pulita riparte il giorno *dopo* la ricaduta. Se la ricaduta dura più giorni (si fuma per 3 giorni), quei giorni risultano "puliti" a meno di registrare una ricaduta **ogni** giorno — irrealistico.

**Decisione**: passare a un **modello a stato esplicito** con due azioni utente — **Inizio** e **Ricaduta** — e un cronometro vivo. Lo stato è persistito come **periodi di astinenza**.

## 2. Decisioni (dal brainstorm 2026-06-28)

- **Modello di stato esplicito.** Un *periodo di astinenza* ha `start` e `end` opzionale. `Inizio` apre un periodo; `Ricaduta` chiude quello aperto. Tra la fine di un periodo e il prossimo `Inizio` c'è un **buco** (ricaduto, non ancora ripreso): il cronometro è fermo, quei giorni **non** contano come puliti. Gestisce le ricadute pluri-giorno in modo naturale.
- **Tre stati derivati**:
  - **NonAvviata** — nessun periodo mai aperto (habit appena creata).
  - **InAstinenza** — esiste un periodo aperto (`end_at_ms == null`).
  - **Ricaduto** — l'ultimo periodo è chiuso, nessuno aperto.
- **Stato iniziale = NonAvviata**: alla creazione il cronometro **non** parte; l'utente preme `Inizio` per cominciare. (Permette di impostare l'abitudine prima di smettere davvero.)
- **Ricaduta = un tap, nessun dato.** Segna il giorno e ferma il cronometro senza chiedere campi. (Eventuali dati-episodio restano agganciabili in futuro, fuori scope — vedi §10.)
- **Superficie doppia**: card cronometro dentro l'abitudine (primaria) + pulsante compatto contestuale sulla riga Home.
- **Persistenza**: nuova tabella `abstinence_period` come **unica fonte di verità** (migrazione additiva v2.12). La completion-on-lapse per le Resistance è **ritirata**.
- **Cronometro vivo a risoluzione 1s** (resta calmo per durate lunghe perché `FormatElapsed` mostra solo le due unità maggiori). **Giorni "buco"** colorati con **danger smorzata**. *(Default proposti e approvati: "mi sembra tutto ok".)*

## 3. Dati — `abstinence_period` (schema v2.12)

Entità dominio `AbstinencePeriod` in **Recordum.Core** (POCO, zero dipendenze UI/EF):

| Colonna | Tipo SQLite | Note |
|---|---|---|
| `id` | TEXT (GUID) PK | |
| `habit_node_id` | TEXT (GUID) FK → `habit_node` | **CASCADE** on delete |
| `started_at_ms` | INTEGER (Unix ms) | apertura periodo (= istante `Inizio`) |
| `ended_at_ms` | INTEGER NULL | chiusura (= istante `Ricaduta`); NULL = periodo aperto/corrente |
| `created_at` / `updated_at` | TEXT ISO 8601 | popolati da `TimestampInterceptor` |
| `updated_at_ms` | INTEGER Unix ms | last-write-wins sync |

- **Niente `created_by_plugin_id`**: è un evento-figlio dell'habit, eredita la provenance dal parent (convenzione §6).
- Indice esplicito `ix_abstinence_period_habit_started` su `(habit_node_id, started_at_ms)`; cascade + indice dichiarati in **Fluent API** (mai default EF), in `Recordum.Data`.
- **Invariante applicativo** (non vincolo SQL): al più **un** periodo aperto (`ended_at_ms IS NULL`) per habit. Garantito dal service (le guardie di §2) + verificato dai test; non un UNIQUE filtrato (SQLite lo supporterebbe, ma teniamo l'invariante nel service per chiarezza e per il caso sync).

### 3.1 Migrazione `AbstinencePeriodV212`

Additiva: crea `abstinence_period` (tabella + indice + FK) e bumpa `AppSetting.schema_version` → **2.12** via `UpdateData`. La migration si genera con `dotnet ef migrations add AbstinencePeriodV212` (rigenera il `ModelSnapshot`); verifica del SQL generato contro questo documento **prima** dell'update (§8 CLAUDE.md). Si coglie l'occasione per **chiudere CR8**: aggiornare il costante `AppSettingSeed.schema_version` da "2.9" a "2.12" *e* rigenerare lo snapshot, così seed (`HasData`/`ModelSnapshot`) e DB vivo tornano allineati (il solo rigenerare lo snapshot non basta — va cambiato anche il valore seed).

### 3.2 Service `IAbstinenceService`

Astrazione in **Recordum.Core.Abstractions.Services**, impl in **Recordum.Data** (dentro uno scope con `IPluginContext.PluginId` = null per azioni user-driven; tempo via `IClock`/`TimeProvider`, mai `DateTime.Now`):

```
record AbstinenceState(AbstinenceStatus Status, long? CurrentStartedAtMs, long? LastEndedAtMs);
enum AbstinenceStatus { NotStarted, Abstaining, Relapsed }

Task<AbstinenceState> GetStateAsync(Guid habitNodeId, CancellationToken ct = default);
Task StartAsync(Guid habitNodeId, CancellationToken ct = default);   // no-op se già Abstaining
Task LapseAsync(Guid habitNodeId, CancellationToken ct = default);   // no-op se non Abstaining
Task<IReadOnlyList<AbstinencePeriod>> ListPeriodsAsync(Guid habitNodeId, DateOnly from, DateOnly to, CancellationToken ct = default);
```

- `StartAsync`: se non c'è periodo aperto, inserisce `{start = clock.now_ms, end = null}`.
- `LapseAsync`: se c'è periodo aperto, ne setta `ended_at_ms = clock.now_ms`.
- Idempotenza/guardie nel service: chiamate spurie (doppio Inizio / Ricaduta a vuoto) sono no-op silenziose.

## 4. Motore streak

`SchedulingAwareStreakAggregator.GetHistoryAsync` — il ramo `HabitType.Resistance` (introdotto dalla spec 2026-06-14) viene **riscritto** per leggere `abstinence_period` invece dei completamenti:

- Ogni **periodo** = una run pulita: `[startDate, endDate]` se chiuso, `[startDate, today]` se aperto (`IsCurrent = true`).
- Il **buco** tra `end` di un periodo e `start` del successivo **non** genera run e non conta come pulito.
- I giorni **prima** del primo `start` non contano (NonAvviata).
- **Pause** ancora onorate: un giorno in pausa dentro un periodo è trasparente (congela, non spezza); coerente con la semantica esistente.
- `StreakRun.Length` = giorni puliti nel periodo (esclusi i giorni in pausa). `StreakBreakReason.Lapse` sulla chiusura (enum già esistente).
- Output: `IReadOnlyList<StreakRun>` (stesso tipo del percorso generico) → `StreakSummaryViewModel`, StreakTimeline chart ed export consumano run coerenti.

Il vecchio calcolo basato su `completions.GetHeatmapAsync` per le Resistance è rimosso.

## 5. Display — `StreakSummaryViewModel`

Ramo Resistance:
- **InAstinenza** → `CurrentDisplay = FormatElapsed(now - currentStart - pausedDays)` (precisione fine, `DurationFormat.FormatElapsed` già esistente). "Pulito da 2a 41g 6h".
- **Ricaduto** / **NonAvviata** → nessuna streak corrente: `CurrentDisplay` mostra uno stato ("Non in astinenza" / "Non avviata", `.resx`). La card invita all'azione (vedi §6).
- "Ricadute (90g)" = numero di **periodi chiusi** (`ended_at_ms` nella finestra) negli ultimi 90 giorni.
- "Più lungo" = periodo (run pulita) più lungo, da §4.

## 6. Interazione

### 6.1 Card nell'abitudine — `AbstinenceQuickAddViewModel`

Nuovo VM che **sostituisce** il routing `HabitType.Resistance => MeasuredQuickAddViewModel` in `HabitScreenViewModel` (oggi riga ~518). Dipendenze: `IAbstinenceService`, `IClock`/`TimeProvider`. Espone:
- `Status` (NotStarted/Abstaining/Relapsed) + testo cronometro `ElapsedDisplay`.
- `StartCommand` (Inizio) / `LapseCommand` (Ricaduta), abilitate per stato.
- Cronometro vivo: un `DispatcherTimer` a **1s** aggiorna `ElapsedDisplay` mentre `Abstaining` (fermo negli altri stati). Restrained per design: `FormatElapsed` mostra due unità → per durate lunghe il testo cambia di rado.
- Layout: **Abstaining** → cronometro grande + pulsante primario "Ricaduta"; **NotStarted** → grande "Inizio"; **Relapsed** → "Ricaduto il _gg/mm_" + "Inizio" per ripartire.
- Dopo un'azione, ricarica lo stato e segnala il refresh (riga Home + summary + calendario si aggiornano — riusa il pattern di refresh post-mutazione già in `HabitScreenViewModel`).

### 6.2 Riga Home — `AbstinenceQuickActionHandler`

Sulla falsariga di `CounterQuickActionHandler` (+1). Per una riga Resistance: pulsante compatto **contestuale** (Inizio quando non in astinenza, Ricaduta quando in astinenza) + tempo trascorso in piccolo accanto al simbolo. Click → `StartAsync`/`LapseAsync` inline + refresh riga. La gestione `magnitudine` Resistance per Home (oggi `mag > 1` in `HabitScreenViewModel` ~668) va aggiornata: la riga mostra lo **stato/tempo d'astinenza**, non un conteggio di completamenti.

Tutte le stringhe nuove in `.resx` (English-first + IT): es. `Abstinence_Start`, `Abstinence_Lapse`, `Abstinence_CleanFor`, `Abstinence_RelapsedOn`, `Abstinence_NotStarted`, `Abstinence_Lapses90d`. Nota build (§7 CLAUDE.md): ogni stringa = 3 edit (`Strings.resx` + `Strings.it.resx` + proprietà manuale in `Strings.Designer.cs`).

## 7. Calendario / heatmap

In `CalendarNavigator` il colore-giorno per le Resistance deriva dai **periodi** (`IAbstinenceService.ListPeriodsAsync` sulla finestra visibile), non dal completion-heatmap:
- **Dentro un periodo** (giorno pulito) → ramp pulito / colore habit (come oggi i giorni "accesi").
- **Giorno di ricaduta** (= `end` del periodo) → nuovo **`Brush.Danger`** (rosso emergenza).
- **Buco** (ricaduto, non ripreso: tra `end` e prossimo `start`) → **danger smorzata** (Danger a bassa opacità / tinta).
- **Prima del primo `start`** → vuoto/neutro (`Brush.HeatmapEmpty`).

Nuovo `Brush.Danger` aggiunto a **Dark.xaml** e **Light.xaml** (manca; esiste solo `Brush.Success`). Colorizer Resistance-specifico isolato, in modo che il percorso non-Resistance del calendario resti invariato.

## 8. Editor / creazione

Nessun cambio strutturale: il mattone "Astinenza" del builder produce già una `Resistance`; l'editor nasconde già Scheduling/Streak/Recovery per le Resistance (spec 2026-06-14 §3.5). La nuova habit nasce **NonAvviata** (nessun periodo) → la card mostra "Inizio".

## 9. Testing

- **`IAbstinenceService`** (Recordum.Data.Tests, `FakeClock`): Inizio da non-astinenza apre un periodo; Inizio già-in-astinenza = no-op; Ricaduta in-astinenza chiude; Ricaduta a vuoto = no-op; cicli multipli Inizio→Ricaduta→Inizio; `GetStateAsync` riporta i 3 stati; `ListPeriodsAsync` clampa alla finestra; cascade delete con l'habit.
- **Aggregator (ramo periodi)**: periodo aperto → run corrente `IsCurrent`; periodi chiusi → run storiche con `Lapse`; **buco** non contato; pausa dentro un periodo congela; più lungo = `LongestLength`.
- **`StreakSummaryViewModel` (Resistance)**: i 3 stati (InAstinenza elapsed dal current start; Ricaduto/NonAvviata "non in astinenza"); "Ricadute 90g" = n° periodi chiusi in finestra; "Più lungo".
- **`AbstinenceQuickAddViewModel`**: transizioni di stato e abilitazione comandi; ticking gestito senza timer reale nei test (inietta il clock; il tick è solo refresh display).
- **Migrazione**: tabella/indice/FK creati; `schema_version` = 2.12.

## 10. Fuori scope / limitazioni note

- **Dati-episodio sulla ricaduta** (sigarette, voglia, nota): non in questa iterazione (Ricaduta = un tap). La tabella periodi non li ospita; un `Completion` opzionale agganciato al giorno di ricaduta resta la via futura, senza accoppiamento col cronometro.
- **Sottrazione pause nel display fine**: come nella spec 2026-06-14, il display corrente sottrae i giorni interi di pausa; granularità sub-giornaliera delle pause fuori scope.
- **Demo seeder `--seed-demo` "Niente fumo"**: va riallineato al modello a periodi (qualche periodo chiuso + uno aperto recente) così la verifica mostra cronometro vivo + marker di ricaduta. Dettaglio in fase di piano.
- **Migrazione dati esistenti**: le eventuali ricadute-come-Completion già nel DB locale non vengono convertite in periodi (dato di sviluppo; per una verifica pulita si rigenera lo stato locale). Nessun backfill in produzione: la feature è pre-1.0.

## 11. Tocchi / file (orientativo, non vincolante per il piano)

- **Core**: `AbstinencePeriod`, `AbstinenceState`/`AbstinenceStatus`, `IAbstinenceService`.
- **Data**: EF entity config + `RecordumDbContext` DbSet + migration `AbstinencePeriodV212` + impl `AbstinenceService` + ramo aggregator riscritto.
- **WPF**: `AbstinenceQuickAddViewModel` + view; `AbstinenceQuickActionHandler` + riga Home; `StreakSummaryViewModel` ramo Resistance; `CalendarNavigator` colorizer; `Brush.Danger` (Dark+Light); stringhe `.resx` ×3; routing in `HabitScreenViewModel`.
- **Test**: come §9.
