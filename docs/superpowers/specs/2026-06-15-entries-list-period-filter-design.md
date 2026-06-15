# Lista inserimenti filtrabile per periodo (Feature B)

> Spec di design — 2026-06-15 · Fase 4 Polish/UX
> Primo di due cicli: **B** (questa) = lista inserimenti per-periodo, fondamento riusabile; **A** (successiva) = tab Note come cronologia unificata pagine+note, riuserà il filtro-periodo qui definito.

## 1. Contesto e problema

Nello HabitScreen (workspace a colonne-toggle: **Cronologia · Dettaglio · Note · Configura**) la colonna **Cronologia** mostra calendario + QuickAdd + timer + DayNote + **"Registrazioni del giorno"**. Quest'ultima (`CompletionListViewModel.LoadDayAsync`, bind `DayCompletions`) è guidata dal calendario: mostra solo i completamenti del **giorno selezionato**. Non esiste alcuna vista che elenchi gli inserimenti **lungo un periodo** — l'unico modo di scorrere la storia è cliccare giorno per giorno sul calendario.

Emerso durante la revisione dell'abitudine "Diario": l'utente cercava lo "storico" degli inserimenti e trovava solo il calendario. (Nota collaterale, fuori scope qui: le pagine di diario sono `Completion.Notes`, le note standalone sono entità `Note`; la loro unificazione è la Feature A successiva.)

## 2. Obiettivo e non-obiettivi

**Obiettivo.** Nella colonna Cronologia, rendere la lista registrazioni commutabile **Giorno | Periodo**. In *Periodo* elenca **tutti** i completamenti dell'abitudine in un intervallo scelto da un filtro (preset + intervallo personalizzato), indipendente dal giorno selezionato sul calendario.

**Non-obiettivi (espliciti):**
- Feature A (tab Note unificata pagine+note) — ciclo separato successivo.
- Filtri diversi dal periodo (per tipo, per testo, per metrica…) — "altri filtri" futuri; il modello del filtro va lasciato **estensibile** ma v1 implementa solo il periodo.
- Persistenza della modalità/periodo tra una visita e l'altra: alla riapertura dello screen si riparte da **Giorno** (default). Nessun nuovo AppSetting, nessuna migrazione.
- Cambiamenti al `ICompletionService` o allo schema: l'overload range esiste già.

## 3. Decisioni di design (confermate)

- **Posizionamento**: nella colonna **Cronologia**, sotto "Registrazioni", come toggle Giorno/Periodo (non in Dettaglio, non in una nuova colonna).
- **Default**: modalità **Giorno** = comportamento attuale invariato (lista guidata dal calendario).
- **Periodo mostra tutti i completamenti** nell'intervallo (ogni riga con i suoi valori + nota inline se presente), riusando le righe attuali (`CompletionRowViewModel`: modifica nota + elimina già supportati).
- **Set periodi v1**: `Settimana · Mese · Anno · Tutto · Personalizzato`.
- **Semantica preset = calendario corrente**:
  - **Settimana** = settimana ISO corrente (lunedì→domenica che contiene oggi).
  - **Mese** = mese di calendario corrente (1→ultimo giorno).
  - **Anno** = anno di calendario corrente (1 gen→31 dic).
  - **Tutto** = nessun limite (from e to illimitati).
  - **Personalizzato** = intervallo `Da`–`A` scelto dall'utente.
  `oggi` deriva **sempre** da `IClock` nel fuso locale (mai `DateTime.Now`).
- **Interazione col calendario in modalità Periodo**: il calendario continua a guidare QuickAdd e DayNote (concern del giorno), ma **non** la lista (resta filtrata per periodo). Tornando a Giorno, la lista riprende il giorno selezionato sul calendario.
- **Default periodo** all'ingresso in modalità Periodo: **Mese**.

## 4. Componenti

Ogni unità ha una responsabilità singola ed è testabile in isolamento.

### 4.1 `EntriesPeriod` (enum) + `EntriesPeriodRange` (resolver puro)

`enum EntriesPeriod { Settimana, Mese, Anno, Tutto, Personalizzato }` (in `Recordum.Core`, accanto agli altri enum di dominio — riusabile da Feature A e da web/MAUI).

`static (DateOnly? From, DateOnly? To) EntriesPeriodRange.Resolve(EntriesPeriod period, DateOnly today, DateOnly? customFrom, DateOnly? customTo)`:
- `Settimana` → lunedì..domenica della settimana ISO contenente `today`.
- `Mese` → primo..ultimo giorno del mese di `today`.
- `Anno` → 1 gen..31 dic dell'anno di `today`.
- `Tutto` → `(null, null)`.
- `Personalizzato` → `(customFrom, customTo)` (se uno è null resta illimitato da quel lato).

Funzione pura, nessuna dipendenza da clock/UI → coperta da unit test diretti. Vive in Core (C# puro).

### 4.2 `CompletionListViewModel.LoadRangeAsync(Guid nodeId, DateOnly? from, DateOnly? to, CancellationToken)`

Carica i completamenti nell'intervallo, riusando `ICompletionService.ListByHabitAsync(nodeId, fromTs, toTs)` (già usato da `LoadDayAsync`). Conversione coerente con `LoadDayAsync`:
- `fromTs = from is {} f ? new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : DateTimeOffset.MinValue`
- `toTs = to is {} t ? new DateTimeOffset(t.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero) : DateTimeOffset.MaxValue`

`LoadDayAsync` viene **rifattorizzato** a wrapper sottile: `LoadRangeAsync(nodeId, date, date, ct)`. Riusa `BuildLookupAsync`/`Populate` esistenti (ordine già `OrderByDescending(CompletedAtMs)`). Nessun `Take` in modalità range (un'abitudine reale non ha migliaia di voci in un periodo; eventuale cap è un "altro filtro" futuro). `LoadAsync` (ultime 20, usato altrove) resta invariato.

### 4.3 `HabitRecordsViewModel` (nuovo)

Incapsula il pannello "Registrazioni" togliendo il concern dalla già-grande `HabitScreenViewModel`. Possiede:
- `EntriesRecordsMode Mode` (enum `{ Giorno, Periodo }`), default `Giorno`.
- `EntriesPeriod SelectedPeriod` (default `Mese`), `DateOnly? CustomFrom`, `DateOnly? CustomTo`.
- `CompletionListViewModel Records` (la lista interna, costruita con gli stessi service).
- `Guid? _nodeId`, `DateOnly? _calendarDay` (ultimo giorno noto dal calendario).

API:
- `SetContext(Guid nodeId)` — memorizza l'abitudine.
- `ShowDayAsync(DateOnly date)` — registra `_calendarDay = date`; se `Mode == Giorno`, `Records.LoadDayAsync(nodeId, date)`. (No-op sulla lista se in Periodo, ma aggiorna `_calendarDay`.)
- `ApplyPeriodAsync()` — risolve il range via `EntriesPeriodRange.Resolve(SelectedPeriod, today, CustomFrom, CustomTo)` e `Records.LoadRangeAsync(nodeId, from, to)`.
- `ReloadAsync()` — rilancia la modalità corrente (Giorno→`_calendarDay`, Periodo→`ApplyPeriodAsync`). Usato dopo un completamento.
- Cambi di `Mode`/`SelectedPeriod`/`CustomFrom`/`CustomTo` (via `[ObservableProperty]` partial-hooks) ricaricano appropriatamente (passando a Giorno → mostra `_calendarDay`; passando a Periodo o cambiando filtro → `ApplyPeriodAsync`).
- `today` ottenuto da `IClock` iniettato (fuso locale), coerente con il resto dello screen.

### 4.4 `HabitScreenViewModel` (modificato)

- Sostituisce la proprietà `DayCompletions` con `Records` (`HabitRecordsViewModel`).
- In load (riga ~393-398): `Records.SetContext(node.Id)`; se c'è un giorno preselezionato → `Records.ShowDayAsync(sel)`.
- `OnCalendarDateSelected` (riga ~559): `await Records.ShowDayAsync(date)` (rimpiazza `DayCompletions.LoadDayAsync`).
- Refresh post-completamento (riga ~447): `Records.ReloadAsync()` (rimpiazza il reload diretto di `DayCompletions`).

### 4.5 XAML — colonna Cronologia (`HabitScreen.xaml`)

Sostituisce il blocco "Registrazioni del giorno" (righe ~859-869) con:
- intestazione "Registrazioni" + toggle segmentato **Giorno | Periodo** (due `RadioButton`/`ToggleButton` legati a `Records.Mode`, stile coerente con i segmenti esistenti del calendario);
- `ComboBox` periodo (`Settimana/Mese/Anno/Tutto/Personalizzato`) legato a `Records.SelectedPeriod`, **visibile solo in Periodo**;
- due `DatePicker` `Da`/`A` legati a `Records.CustomFrom/CustomTo`, **visibili solo quando** `SelectedPeriod == Personalizzato`;
- la lista `ContentControl Content="{Binding Records.Records}"` (riusa il `DataTemplate` di `CompletionListViewModel` già definito a riga ~74) + il placeholder "vuoto" legato a `Records.Records.Rows.Count`.

### 4.6 i18n (`Strings.resx` + `Strings.it.resx`)

Nuove chiavi (English-first, italiano secondario): `HabitScreen_Records` ("Records"/"Registrazioni"), `Records_Mode_Day`, `Records_Mode_Period`, `Period_Week`, `Period_Month`, `Period_Year`, `Period_All`, `Period_Custom`, `Period_From`, `Period_To`. Enum→stringa via converter o `DisplayMember` dedicato (niente `ToString()` dell'enum in UI).

## 5. Data flow

```
Calendario.SelectedDateChanged ─▶ HabitScreenVM.OnCalendarDateSelected ─▶ Records.ShowDayAsync(date)
                                                                              │  (Mode==Giorno) ─▶ Records.LoadDayAsync
Toggle Mode / ComboBox periodo / DatePicker ─▶ Records.<hook> ─▶ ApplyPeriodAsync ─▶ Resolve(period,today) ─▶ Records.LoadRangeAsync
Completamento registrato (EventBus) ─▶ HabitScreenVM refresh ─▶ Records.ReloadAsync ─▶ (rilancia modalità corrente)
```

## 6. Error handling

- Range `Personalizzato` con `From > To` o date mancanti: `Resolve` restituisce il range così com'è; `LoadRangeAsync` con `from > to` produce semplicemente **lista vuota** (nessuna eccezione, nessun crash). Nessun completamento futuro esiste, quindi `To` di default a `null` (illimitato) in Tutto è sicuro.
- `LoadRangeAsync`/`LoadDayAsync` seguono il pattern async esistente; gli errori del service si propagano come oggi (non peggiorati da questa feature).

## 7. Testing

- **`EntriesPeriodRange.Resolve`** (puro, `[Fact]`/`[Theory]`): fissare `today = 2026-06-17` (**mercoledì**, così il bound-lunedì è ≠ today e l'arrotondamento viene davvero esercitato). Aspettative: Settimana→`2026-06-15`..`2026-06-21` (lun..dom ISO); Mese→`2026-06-01`..`2026-06-30`; Anno→`2026-01-01`..`2026-12-31`; Tutto→`(null, null)`; Personalizzato→restituisce `(customFrom, customTo)` invariati; Personalizzato con un lato `null`→illimitato da quel lato.
- **`HabitRecordsViewModel`** (con fake `ICompletionService`/`IClock`): default Giorno mostra il giorno del calendario; `ShowDayAsync` in Periodo non tocca la lista ma aggiorna `_calendarDay`; switch a Periodo carica il range del default (Mese); cambio `SelectedPeriod` ricarica; `Personalizzato` usa `CustomFrom/To`; `ReloadAsync` rilancia la modalità corrente; tornando a Giorno ricompare `_calendarDay`.
- **`CompletionListViewModel.LoadRangeAsync`** (con fake service): filtra per range, ordina desc per `CompletedAtMs`; `from/to` null → illimitato; `LoadDayAsync` continua a funzionare (regressione, ora via wrapper).
- Suite di riferimento da tenere verde: WPF / Data / Core attuali + i nuovi test.

## 8. Rischi e note

- **Fuso orario nei bound**: `LoadDayAsync` usa già `TimeSpan.Zero` (UTC) per i bound del giorno; `LoadRangeAsync` mantiene la stessa convenzione per coerenza. Eventuale disallineamento timezone è **pre-esistente** e non introdotto qui; non lo affrontiamo in questa feature (sarebbe un fix trasversale separato).
- **`HabitScreenViewModel` è già grande**: estrarre `HabitRecordsViewModel` *riduce* il suo peso invece di aumentarlo (sposta fuori il concern records). Coerente con la direzione di decomposizione del file.
- **Estensibilità filtri**: il periodo è modellato come un componente a sé (`EntriesPeriod`+resolver) e il pannello (`HabitRecordsViewModel`) tiene lo stato del filtro separato dalla lista — aggiungere "altri filtri" poi significa aggiungere proprietà al pannello e comporle nel load, senza toccare `CompletionListViewModel`.
