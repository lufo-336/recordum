# Modular Habit Builder — Design (Spec 1)

> **Fase 4 / Polish-UX → estensione funzionale.** Brainstorming 2026-06-02. Companion visivo: `.superpowers/brainstorm/255-1780415706/`. Autoritativo su questa feature; il modello dominio resta governato da `CLAUDE.md` §5/§6 e dagli schemi.

## 1. Problema

Oggi creare un'abitudine = picker modale (`HabitTemplatePicker`) → "Abitudine vuota" crea **subito** un nodo `Action`, nome di default, colore grigio, già persistito; la configurazione è un passo separato e opzionale nell'editor "Configura" (live/autosave). Risultato: abitudini mezze-vuote, e soprattutto **la UI non esprime la modularità** che è un principio fondante di Recordum (§2.4 "complessità per composizione"). Un'abitudine come "3 serie di flessioni con tempo di riposo" non è componibile in modo visibile.

## 2. Obiettivo

Un **costruttore modulare block-first** per creare un'abitudine da zero: parti da una tela, **assembli mattoni** (campi misurati, serie, modo di registrazione), vedi **un'anteprima dal vivo** della registrazione reale, e quando è pronta la **Lanci**. Il `habit_type` del dominio è una **conseguenza dedotta** dei mattoni, non una scelta a monte. Il costruttore è la superficie che, in futuro (Spec 3), diventerà anche "Configura".

### Principi rispettati
- **Composizione > opzioni gonfie** (brief §2.4): i mattoni compongono, con guardrail che impediscono il nonsense.
- **Profondità senza intimidazione**: essenziali visibili + "Configurazione avanzata" collassata; i **template** restano l'on-ramp facile.
- **English-first** nelle stringhe (`.resx`); spec in italiano.

### Non-obiettivi (decomposizione)
- **Modo sessione + runtime timer** (segmenti cronometrati, auto-avanzamento, loop, beep, recovery) → **Spec 2** (assorbe il vecchio "Piano 3"). Spec 1 copre solo i modi **manuale · cronometro · countdown**.
- **Modifica di abitudini già lanciate e con dati** + unificazione draft↔editor live + regole edit sicuro/distruttivo → **Spec 3**.
- **Catalogo template ufficiali** (contenuto) → lavoro separato. Spec 1 abilita i custom e il consumo, non riempie il catalogo.
- **Gruppi**: invariati. La composizione *fra* abitudini (gruppi) resta il path "Gruppo vuoto" del picker, fuori dal builder.

## 3. Semantica bozza → lancio

- **Bozza pura**: durante la composizione **niente è persistito**. Annullare/chiudere = non resta nulla.
- **Lancia**: persiste la composizione in **una transazione atomica** (nodo + config + metriche). Da quel momento l'abitudine esiste e compare in lista.
- I GUID di nodo/metriche sono assegnati **lato client** nel draft, così i riferimenti (`MetricDefinition.Id`, futuri `CompletionValue.MetricDefinitionId`) sono coerenti già prima del commit.

## 4. Grammatica mattoni → tipo

### 4.1 I mattoni (Spec 1) e il loro mapping al dominio
- **Campo misurato** (`MetricDefinition`), in 4 nature:
  - `conteggio` → `MetricType.Integer`
  - `quantità` → `MetricType.Decimal` + `UnitOfMeasureId`
  - `tempo` → `MetricType.Seconds`
  - `scala` → `MetricType.Integer` + `ScaleTemplateId`/`LabelsJson`
- **Testo libero** (diario) → `MetricType.Text` (il campo definente di un journal).
- **Serie** → multi-serie (resistance, `CompletionValue.SeriesIndex`): toggle + conteggio di default.
- **Modo di registrazione**: `manuale` · `cronometro` · `countdown` (sessione = Spec 2).

### 4.2 Risoluzione del tipo — per priorità (prima regola che matcha)

| # | Condizione sui mattoni | `habit_type` |
|---|---|---|
| 1 | C'è **testo libero** | **journal** |
| 2 | **Serie** attiva | **resistance** |
| 3 | Solo campo/i **scala**, nessun conteggio/quantità/tempo | **state** |
| 4 | ≥1 campo **conteggio/quantità/tempo** | **counter** |
| 5 | Nessun campo misurato | **action** |

### 4.3 Guardrail (coesistenza dei mattoni)

| Mattone | action | counter | resistance | state | journal |
|---|:---:|:---:|:---:|:---:|:---:|
| conteggio / quantità | → counter | ✓ | ✓ | → counter | ✗ |
| tempo | → counter | ✓ | ✓ | → counter | ✓¹ |
| campo scala | → state | ✓ (rating extra) | ✗ | ✓ (primario) | ✗ |
| serie | → resistance | → resistance | ✓ | ✗ | ✗ |
| modo cronometro/countdown | ✗ | ✓ | ✓ | ✗ | ✓¹ |
| testo libero | ✗ | ✗ | ✗ | ✗ | ✓ (definente) |

Legenda: ✓ lecito · ✗ bloccato (disabilitato in palette con tooltip) · "→ X" aggiungerlo trasforma il tipo.
¹ **Rilassamento journal+tempo** (deciso in brainstorming): `journal` può avere **un campo `tempo` + cronometro/countdown** ("tempo dedicato alla scrittura"), ma **non** conteggio/quantità/scala/serie. La risoluzione resta #1 (testo libero → journal anche con un campo tempo).

### 4.4 UX della grammatica
- Badge **"tipo dedotto"** sempre visibile in testata, aggiornato in tempo reale.
- Un mattone che **cambierebbe** il tipo è mostrato come **transizione fluida** ("Serie → diventa Resistance"), non come errore.
- Mattoni **illegali** per il tipo corrente: disabilitati in palette con tooltip che spiega.
- **Testo libero** è il blocco definente del journal: aggiungerlo blocca conteggio/quantità/scala/serie (resta consentito il solo campo tempo + timer).

### 4.5 Esempi di verifica
- Flessioni 3× = *Ripetizioni(conteggio)+Riposo(tempo)* + serie → #2 → **resistance**.
- Bevi 2L = *quantità(L)* → #4 → **counter**.
- Umore = *scala 1–5* → #3 → **state**.
- Scrittura cronometrata = *testo libero* + *tempo* + cronometro → #1 → **journal** (con timer).
- Medita (nessun campo) → #5 → **action**.

## 5. Anatomia del builder (UI)

Superficie **ampia** (schermata/dialog grande — non modale stretta; è la futura "Configura"). Tre zone dall'alto:

1. **Testata** — nome, colore, icona + badge *tipo dedotto* (live).
2. **Composizione** — palette `+ mattone ▾` e i mattoni aggiunti come chip/righe **rimovibili e riordinabili**. Mini-config per campo: *nome* (es. "Ripetizioni") e, secondo natura, *unità* (quantità) o *scala/etichette* (scala). Serie: toggle + conteggio default. Modo: manuale/cronometro/countdown.
3. **Anteprima dal vivo** — rende la **griglia di registrazione reale** (serie × campi) mentre componi, rispecchiando i `QuickAdd*Template` esistenti.

Sotto: toggle **⌄ Configurazione avanzata** che apre le sezioni editor già esistenti (scheduling, streak, goal, notifiche, tag, reset-time) — in Spec 1 in **modalità bozza** (vedi §7). In fondo: **Annulla · Salva come template · Lancia**.

### 5.1 Serie configurate = default, non gabbia
Il conteggio serie in config **pre-riempie** la registrazione; il recording possiede le proprie serie (il dato è già per-serie). Il "+ Aggiungi serie" / ✕ a runtime (già presenti in `ResistanceQuickAddViewModel`) restano e valgono solo per la singola registrazione. Cambiare il default in config cambia solo i giorni futuri.

## 6. Template personalizzati

- Azione **"Salva come template"** nel builder: persiste la composizione corrente come `HabitTemplate` **custom** (`IsOfficial=false`, `CreatedByPluginId=null`), serializzando nodo+config+metriche in `PayloadJson` (stessa macchina di `TemplatePayload` estesa in B6a). Esito **indipendente** dal Lancia (puoi salvare un template senza creare un'abitudine viva).
- Il picker di creazione mostra una sezione **"I tuoi template"** accanto agli ufficiali, con **rinomina** ed **elimina**. I template (ufficiali o custom) si **istanziano subito** (niente builder pre-lancio); l'utente li modifica dopo (Spec 3) o ricostruisce.
- Template che includono il **modo sessione/segmenti** → Spec 2.

## 7. Architettura e data flow

### Componenti
- **`HabitDraft`** (nuovo, in memoria): nodo + lista metriche (con GUID client) + serie-default + modo + valori delle sezioni avanzate. Nessuna persistenza fino al Lancia.
- **`HabitBuilderViewModel`** (nuovo): possiede il `HabitDraft`, espone la palette, applica la **grammatica** (risoluzione tipo + guardrail), calcola il *tipo dedotto* e l'**anteprima**. Riusa le section VM avanzate in **modalità bozza** (vedi nota).
- **Grammatica** (nuovo, in `Recordum.Core` o WPF puro, testabile): funzione pura `ResolveType(blocks) → HabitType` + `IsAllowed(block, currentBlocks)`. Niente dipendenze UI.
- **`CreateComposedHabitAsync(HabitDraft)`** (nuovo metodo di servizio, lato Data): in **una transazione** crea `HabitNode` + `HabitConfig` + `MetricDefinition[]`. Ritorna l'id.
- **Picker**: `HabitTemplatePickerViewModel` esteso — "Da zero" apre il builder; "I tuoi template" (lista custom + rinomina/elimina); template (qualsiasi) → `InstantiateAsync` diretto.
- **`IHabitTemplateService`** esteso: `SaveAsTemplateAsync(draft/composition, name, …)` + `DeleteAsync`/`RenameAsync` per i custom.

> **Nota sezioni avanzate in bozza**: le 11 section VM attuali fanno autosave per-modifica su un nodo esistente. In Spec 1 il builder le usa in **modalità bozza** (buffer in memoria, persistite al Lancia). L'opzione minima è bufferizzare i valori avanzati nel `HabitDraft` e applicarli in `CreateComposedHabitAsync`; la riconciliazione completa draft↔live (riuso pieno delle stesse view in entrambe le modalità) è **Spec 3**. La Spec 1 può limitare l'"avanzata" ai campi config già coperti da `HabitConfig` per restare leggera.

### Flusso
1. Picker → "Da zero" → apre builder con `HabitDraft` vuoto (tipo dedotto = action).
2. Utente aggiunge/rimuove mattoni → grammatica aggiorna tipo + anteprima; guardrail abilita/disabilita la palette.
3. **Lancia** → `CreateComposedHabitAsync` (atomico) → ritorna id → picker/host chiude e seleziona la nuova riga.
4. Oppure **Salva come template** → `SaveAsTemplateAsync` → compare in "I tuoi template".
5. **Annulla** → discard, nessun residuo.

## 8. Error handling / validazione
- **Nome** obbligatorio per Lancia e per Salva-come-template (vincolo già presente nell'editor: `Editor_Error_NameRequired`).
- **Grammatica**: combinazioni illegali impedite a monte (palette), non a valle.
- **Atomicità**: `CreateComposedHabitAsync` in transazione; su errore, rollback completo (nessun nodo orfano, nessuna metrica senza nodo).
- **Quantità/scala**: un campo `quantità` richiede un'unità; un campo `scala` richiede scala/etichette — validati prima del Lancia.

## 9. Testing
- **Grammatica** (puro, no UI): ogni riga di §4.2 (risoluzione tipo) + §4.3 (guardrail: illegali bloccati, transizioni corrette), incluso il rilassamento journal+tempo.
- **Bozza/lancio**: il draft non tocca il DB prima del Lancia; `CreateComposedHabitAsync` crea atomicamente nodo+config+metriche con i GUID del draft; rollback su errore; Annulla senza residui.
- **Builder VM**: aggiungi/rimuovi mattone → tipo dedotto + anteprima aggiornati; anteprima rispecchia serie×campi; palette disabilita gli illegali.
- **Template custom**: Salva-come-template round-trip (compose → save → il payload reistanzia la stessa composizione); picker elenca/rinomina/elimina i custom; istanza custom = istanza diretta.

## 10. Impatti / consumatori esistenti
- I marcatori calendario (Spec 2026-06-02 cell-markers) consumano tipo+metriche+serie via `GetDailyMagnitudeAsync` (type-aware): restano corretti perché il Lancia produce un tipo concreto valido.
- `ResistanceQuickAddViewModel` (recording multi-serie + AddSeries) resta il modello di registrazione che l'anteprima rispecchia.
- Nessuna **migrazione** schema: tutto è già nel modello (HabitNode, HabitConfig, MetricDefinition, HabitTemplate). Schema resta **v2.8**.

## 11. Decomposizione (promemoria)
- **Spec 1 (questa)**: costruttore block-first non-sessione + bozza/lancio atomico + template custom.
- **Spec 2**: modo **sessione** (composizione segmenti nel builder) + **runtime timer**.
- **Spec 3**: builder come **"Configura"** su abitudini esistenti con dati (regole edit sicuro/distruttivo, unificazione draft↔live, protezione dati).
