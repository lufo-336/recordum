# F2c-2 (+ F2c-3) — il restore ferma tutti gli utenti del database

Branch `f2c-data-safety`. Decisioni confermate dall'autore il 2026-10-19.

## Decisioni

1. **`DatabaseGate`** (Data, singleton) è esso stesso un `DbConnectionInterceptor`, registrato in `AddInterceptors`
   accanto a `SqlCipherKeyConnectionInterceptor` in entrambe le composizioni (Presentation e WPF) e iniettato in
   `RestoreService`. Conta le connessioni EF aperte: permesso all'apertura, restituito alla chiusura o al fallimento,
   al più una volta per connessione (`ConditionalWeakTable`).
2. A cancello chiuso: apertura **asincrona aspetta** la riapertura (con il suo `CancellationToken`), apertura
   **sincrona lancia** `DatabaseQuiescedException` (bloccare il thread UI congelerebbe anche lo spegnimento).
3. `QuiesceAsync(timeout)` chiude, aspetta zero connessioni, torna un handle `IDisposable` che riapre al `Dispose`
   salvo `KeepClosed()`. Timeout scaduto → riapre e lancia `TimeoutException`. Timeout del restore: **10 s**.
4. `RestoreService`: anteprima, snapshot, decifra/estrai/**ricifra a cancello aperto**; poi quiesce →
   `ClearAllPools()` → scambio → `Realign` 0600 del nuovo DB (**F2c-3**). Successo: `KeepClosed()` fino all'uscita.
   Errori: timeout → `RestoreException("busy")`; `IOException`/`UnauthorizedAccessException` nello scambio →
   `RestoreException("swap_failed")`. Entrambi con stringa in `.resx` in `RestoreDialogViewModel.Readable`.
5. Fuori perimetro, nel tracker come residuo nuovo: `KeyTransitionService` (rekey) e lo sblocco usano lo stesso schema
   `ClearAllPools()` senza cancello.

## Obblighi di test (ciascuno visto rosso contro il codice senza la correzione)

- Gate: la chiusura aspetta una connessione aperta; un'apertura async a cancello chiuso aspetta e riparte alla
  riapertura; una sync lancia; timeout riapre; un'apertura fallita restituisce il permesso; uso normale torna a zero.
- Restore: una scrittura lanciata a metà restore non arriva nel DB ripristinato; una connessione tenuta aperta →
  `busy` e dati intatti; dopo il restore il DB è 0600 anche se il sorgente non lo era (Unix).
- ViewModel: `busy` e `swap_failed` diventano messaggi leggibili.
- DI: il gate è fra gli interceptor ed è lo stesso singleton iniettato nel restore.

## Trappole

- Una connessione EF chiusa torna nel pool con il file aperto: dopo lo zero serve comunque `ClearAllPools()`.
- `ConnectionClosed` non arriva dopo un'apertura fallita: per questo `ConnectionFailed` restituisce il permesso, e la
  tabella per connessione impedisce il doppio rilascio.
- La ricifratura usa una `SqliteConnection` grezza sul file in chiaro: non passa dal gate, e va fatta prima di chiudere.
