# U9.1 — Orologio vivo d'astinenza + registrazioni periodi in Cronologia

> **Design doc.** Estende U9 (astinenza a stato esplicito, spec `2026-06-28-abstinence-explicit-state-design.md`). Nessun cambio schema. Branch `fase-4-polish`.

## Contesto e problema

La verifica visiva di U9 (2026-07-01) ha rilevato due lacune:

1. **Manca un orologio che mostri il tempo che scorre dall'inizio dell'astinenza.** Il quick-add mostra `DurationFormat.FormatElapsed` (due unità grezze: "appena ora" / "12m" / "21g 6h") che non scorre visibilmente. L'utente vuole un cronometro con i secondi che avanzano.
2. **Mancano le registrazioni delle astinenze nella colonna Cronologia.** La lista "Registrazioni" è costruita sui *completamenti* (`CompletionListViewModel`); l'astinenza non scrive completamenti, quindi per le abitudini `Resistance` è vuota. L'utente vuole vedere durata di ogni astinenza, le ricadute, e poter **eliminare** una registrazione.

Decisioni prese in brainstorming (2026-07-01, con anteprime browser):

- **Orologio**: cronometro vivo `Ng h:mm:ss` con i secondi che scorrono, presente **sia** nel quick-add (colonna Cronologia) **sia** nella pagina Dettaglio (StreakSummary). (Opzione B.)
- **Lista registrazioni**: layout a **righe-scheda** — ogni periodo una scheda con durata pulita in evidenza, sotto l'intervallo e la data di ricaduta, barra colorata a sinistra (blu = in corso, rossa = chiuso da ricaduta), cestino a destra. (Opzione A.)
- **Eliminazione**: si può eliminare **qualsiasi** periodo (chiuso o quello in corso), **con dialog di conferma**. Eliminare quello in corso annulla il tentativo attuale → lo stato torna a `Relapsed` (se esiste un periodo chiuso precedente) o `NotStarted`.

## Principi rispettati

- **Nessun cambio schema.** La tabella `abstinence_period` (v2.12) è già la fonte di verità; si aggiunge solo un'operazione di delete e nuove viste.
- **English-first**: nuove stringhe utente in `Strings.resx` + `Strings.it.resx` + proprietà manuale in `Strings.Designer.cs` (nessun code-gen ResX).
- **Tempo sempre via `IClock`** (`UtcNow`/`UtcNowMs`); mai `DateTime.Now`/`UtcNow` diretti.
- **MVVM + DI** (`CommunityToolkit.Mvvm`); `DispatcherTimer` istanziato solo se `Application.Current is not null` (guardia per i test headless, come già fa `AbstinenceQuickAddViewModel`).
- **Unità piccole e isolate**: un helper cronometro riusabile, una VM riga e una VM lista dedicate, un service di conferma astratto e testabile.

## Componenti

### 1. Pezzi condivisi

**`IAbstinenceService.DeletePeriodAsync(Guid periodId, CancellationToken ct = default)`**
- Nuovo metodo sull'interfaccia (`Recordum.Core`) + implementazione in `AbstinenceService` (`Recordum.Data`): hard-delete della riga `abstinence_period` con quell'`Id`. No-op se l'id non esiste.
- Non serve altra logica di "ricucitura": lo stato è derivato al volo da `GetStateAsync` (periodo aperto → `Abstaining`; altrimenti ultimo chiuso → `Relapsed`; nessuno → `NotStarted`). Eliminando l'aperto, lo stato ricade naturalmente sul precedente.

**`DurationFormat.FormatStopwatch(TimeSpan elapsed)` → string**
- Formato cronometro con secondi visibili:
  - `elapsed >= 1 giorno` → `"{totalDays}g {h}:{mm:00}:{ss:00}"` (es. `"21g 6:14:09"`).
  - altrimenti → `"{h}:{mm:00}:{ss:00}"` (es. `"6:14:09"`, `"0:03:07"`).
- `elapsed < 0` trattato come `TimeSpan.Zero`. `totalDays` = `(int)elapsed.TotalDays`; ore/min/sec dal resto entro il giorno. `CultureInfo.InvariantCulture` (come le altre `DurationFormat`).
- `FormatElapsed` resta per altri usi eventuali; il cronometro vivo usa `FormatStopwatch`.

**`LiveElapsedClock`** (nuovo, `Recordum.WPF`, ObservableObject)
- Helper riusabile per il cronometro vivo. Costruito con `IClock`. Espone `string Display`.
- Metodo `Start(long startedAtMs)` avvia un `DispatcherTimer` da 1s che ricalcola `Display = FormatStopwatch(clock.UtcNow - fromUnixMs(startedAtMs))`; `Stop()` ferma il timer e azzera `Display`.
- `DispatcherTimer` creato solo se `Application.Current is not null`; in ambiente test resta inerte (chi lo usa può chiamare un `Refresh()` interno una volta per ottenere il valore corrente senza tick).
- Riusato da: quick-add, StreakSummary, riga-periodo aperta.

**`IConfirmationDialog { Task<bool> ConfirmAsync(string title, string message) }`** (nuovo, `Recordum.WPF`)
- Astrae la conferma sì/no per rendere testabili le VM che eliminano.
- Impl `MessageBoxConfirmationDialog`: `MessageBox.Show(message, title, YesNo, Question) == Yes`, sul dispatcher UI.
- Fake nei test restituisce un valore preconfigurato (`true`/`false`) e registra le chiamate.
- Registrato in DI (`ServiceRegistrar`).

**Nuove stringhe** (EN / IT):

| chiave | EN | IT |
|---|---|---|
| `Abstinence_InProgress` | `In progress` | `In corso` |
| `Abstinence_CleanDays` | `{0} clean days` | `{0} giorni puliti` |
| `Abstinence_Since` | `since {0}` | `dal {0}` |
| `Abstinence_DeleteConfirm_Title` | `Delete record` | `Elimina registrazione` |
| `Abstinence_DeleteConfirm_Message` | `Delete this abstinence record? This cannot be undone.` | `Eliminare questa registrazione d'astinenza? L'operazione non è reversibile.` |

(`Abstinence_RelapsedOn` "Ricaduta il {0}" esiste già da U9 e viene riusata.)

### 2. Orologio vivo (feature 1 — opzione B)

**Quick-add** (`AbstinenceQuickAddViewModel`, esistente):
- Sostituisce il tick manuale attuale (che usa `FormatElapsed`) con il cronometro `FormatStopwatch`. In pratica: `RefreshElapsed` usa `FormatStopwatch`; oppure delega a `LiveElapsedClock`. Il display "Pulito da {stopwatch}" scorre ogni secondo mentre `Abstaining`; fermo/azzerato negli altri stati (come oggi).

**Dettaglio** (`StreakSummaryViewModel`, esistente):
- Il ramo `Resistance` oggi calcola `CurrentDisplay` una volta via `FormatElapsed`. Diventa vivo: usa `LiveElapsedClock` avviato sullo `CurrentStartedAtMs` quando `Abstaining`, così il valore "streak corrente" scorre al secondo come nel quick-add. Negli stati non-abstaining resta il testo di stato (`Abstinence_NotStarted`), invariato.

### 3. Registrazioni periodi in Cronologia (feature 2 — opzione A)

**`AbstinencePeriodRowViewModel`** (nuovo, `Recordum.WPF`)
- Avvolge un `AbstinencePeriod`. Proprietà:
  - `IsCurrent` = `EndedAtMs is null`.
  - `DurationDisplay`:
    - aperto → cronometro vivo (via `LiveElapsedClock` su `StartedAtMs`), es. `"21g 6:14:09"`.
    - chiuso → `string.Format(Abstinence_CleanDays, giorniPuliti)` dove `giorniPuliti` = numero di giorni `start..end` inclusivi (allineato al motore streak; le pause **non** vengono sottratte qui — coerente col fatto che la colorazione calendario già ignora le pause, noto minore).
  - `SinceDisplay` = `string.Format(Abstinence_Since, startDate)` (per l'aperto) oppure `"{start} → {end}"` (per il chiuso).
  - `RelapsedOnDisplay` = `string.Format(Abstinence_RelapsedOn, endDate)` per i chiusi; vuoto per l'aperto.
  - `DeleteCommand`: chiama `IConfirmationDialog.ConfirmAsync(...)`; se confermato, `IAbstinenceService.DeletePeriodAsync(period.Id)`, poi invoca `onDeleted(this)` (rimozione dalla lista) e solleva `Changed` per far aggiornare orologio/calendario/summary dell'host.
- Dipende da: `IAbstinenceService`, `IConfirmationDialog`, `IClock`.

**`AbstinencePeriodListViewModel`** (nuovo, `Recordum.WPF`)
- `ObservableCollection<AbstinencePeriodRowViewModel> Rows`.
- `LoadRangeAsync(Guid nodeId, DateOnly? from, DateOnly? to, ct)`: chiama `IAbstinenceService.ListPeriodsAsync(nodeId, from, to)`, ordina **newest-first** con il periodo **aperto in cima**, materializza le righe. `LoadDayAsync(nodeId, day)` = `LoadRangeAsync(nodeId, day, day)` (periodi che contengono il giorno).
- Possiede il tick da 1s per la riga aperta (delegato al `LiveElapsedClock` della riga; la lista si limita ad avviarlo/fermarlo).
- Rimozione riga alla cancellazione; espone un evento `Changed` propagato dalle righe verso l'host.
- Dipende da: `IAbstinenceService`, `IConfirmationDialog`, `IClock`.

**`HabitRecordsViewModel`** (esistente) diventa **type-aware**:
- `SetContext` riceve anche l'`HabitType` (o un flag `isResistance`).
- Espone la lista corrente come oggetto commutabile: per `Resistance` un `AbstinencePeriodListViewModel`, altrimenti il `CompletionListViewModel` esistente. La chrome Giorno/Periodo + filtro periodo resta condivisa; `ShowDayAsync`/`ApplyPeriodAsync`/`ReloadAsync` instradano alla lista giusta.
- La logica di cancellazione delle overlapping-reload (CR19, `CancellationTokenSource` per-reload) resta invariata.

**XAML** (`HabitScreen.xaml`, colonna Cronologia):
- Il pannello "Registrazioni" usa un `ContentControl` legato alla lista corrente con due `DataTemplate` per tipo (mirroring del pattern QuickAdd): uno per `CompletionListViewModel` (esistente) e uno nuovo per `AbstinencePeriodListViewModel` che rende le righe-scheda (badge "In corso", durata grande, intervallo + ricaduta, barra sinistra colorata via `Brush.Danger`/colore habit, cestino con `DeleteCommand`).

## Flusso dati

```
Inizio/Ricaduta/Elimina (quick-add o riga) → IAbstinenceService (Start/Lapse/DeletePeriod)
        → Changed → host (HabitScreenViewModel) ricarica: quick-add, StreakSummary, calendario, lista periodi
LiveElapsedClock (DispatcherTimer 1s) → Display (FormatStopwatch) → quick-add / Dettaglio / riga aperta
```

## Gestione errori

- `DeletePeriodAsync` no-op su id inesistente (nessuna eccezione).
- Conferma annullata → nessuna cancellazione, nessuna mutazione.
- `DispatcherTimer` mai creato in headless → i test non dipendono dal dispatcher; il display è calcolabile on-demand.
- Errori inattesi nelle command async → seguono il pattern esistente (try/catch + trace, come CR15) dove applicabile.

## Testing (TDD)

- **Data**: `DeletePeriodAsync` — elimina periodo chiuso (storia si accorcia), elimina periodo aperto (stato → `Relapsed`/`NotStarted`), id inesistente = no-op.
- **DurationFormat.FormatStopwatch** — `< 1h`, `< 1g`, `>= 1g`, negativo → zero; formattazione `mm`/`ss` a due cifre.
- **`AbstinencePeriodRowViewModel`** — aperto ⇒ `IsCurrent`, durata da cronometro; chiuso ⇒ "N giorni puliti" + "Ricaduta il …"; `DeleteCommand` con conferma `true` chiama `DeletePeriodAsync` + `onDeleted` + `Changed`; conferma `false` non elimina.
- **`AbstinencePeriodListViewModel`** — ordina aperto-in-cima/newest-first; `LoadDay` filtra i periodi che contengono il giorno; rimozione riga alla cancellazione.
- **`StreakSummaryViewModel`** / **`AbstinenceQuickAddViewModel`** — usano `FormatStopwatch` (verifica del display su un `FakeClock` fisso, senza dipendere dal tick).
- **`IConfirmationDialog`** — fake configurabile; nessun `MessageBox` reale nei test.

## Fuori scope (YAGNI)

- Modifica delle date di un periodo (solo elimina).
- Sottrazione delle pause sub-giornaliere nel cronometro/durata.
- Backfill dei vecchi completamenti Resistance in periodi.
- Modifica del formato coarse `FormatElapsed` esistente (resta per compatibilità).
