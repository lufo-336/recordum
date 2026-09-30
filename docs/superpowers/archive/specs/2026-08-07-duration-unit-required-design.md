# L'unità temporale è obbligatoria su una metrica durata

**Data**: 2026-08-07
**Branch**: `wave-2-3-registration`
**Stato**: approvato dall'autore (brainstorming 2026-08-07)
**Chiude**: la domanda aperta lasciata dal fix export `09b3107` — «durata senza unità resa "27000 seconds"
nell'export vs "7:30:00" nelle Registrazioni». Risponde inoltre al punto 3 di
`recordum-counter-repeatability-rethink` (metrica durata + unità separata), che da qui in poi non è
più una domanda di modello aperta.

---

## 1. Il problema

Tre concetti di "unità" convivono attorno a una durata:

| | dove vive | ruolo |
|---|---|---|
| `MetricDefinition.UnitOfMeasureId` | editor / builder | unità **dichiarata** della metrica — nullable, qualsiasi unità |
| `SeriesCellViewModel.DurationUnit` | cella quick-add | unità con cui **digiti** questa registrazione (h/min/sec), normalizzata a secondi al salvataggio |
| `value_seconds` | DB | il dato: **sempre secondi grezzi** |

La prima è oggi facoltativa, e nessun percorso di creazione la valorizza: `FieldBlockViewModel` mappa
`FieldNature.Time → MetricType.Seconds` senza toccare `UnitOfMeasureId`, e il selettore h/min/sec del
builder nasce vuoto. Quindi **la durata senza unità è il caso normale, non l'eccezione**.

Una durata senza unità non ha un'unità in cui rendersi, e ogni consumatore ha dovuto inventarsi un
ripiego diverso:

- Cronologia → `DurationFormat.Format` → `7:30:00`
- totale del giorno / riga Home / striscia / goal → secondi grezzi → `27000`
- export → `27000` etichettato `seconds`

Una durata *con* unità invece legge già coerentemente ovunque (`7,5 ore` a schermo, `7.5 hours`
nell'export). L'incoerenza non è fra i consumatori: è fra le metriche che un'unità ce l'hanno e quelle
che non ce l'hanno.

Aggiunta: la stringa che spiega perché "Crea" è disabilitato **promette già questa regola** e mente.

> `HabitBuilder.LaunchHint` — *"Dai un nome e scegli un'unità/scala per ogni campo Quantità/Scala per lanciare."*

`HabitBuilderViewModel.CanLaunch` controlla soltanto le scale. Questa spec non aggiunge un vincolo
nuovo all'interfaccia: rende vero un vincolo che l'interfaccia già dichiara, spostandolo da Quantità
(dove l'unità resta a ragione opzionale) a Tempo (dove serve).

## 2. La regola

> Una metrica di tipo durata (`MetricType.Seconds`) ha sempre un'unità temporale — **ore, minuti o
> secondi** — e solo una di quelle tre.

Due corollari:

1. su una durata non si può più scegliere un'unità non temporale (oggi "km" su una durata è
   accettato, e il risultato è indistinguibile dal non avere unità: i totali ripiegano su secondi grezzi);
2. su una durata la tendina dell'unità non ha voce vuota.

La regola vale **per costruzione, sulle metriche nuove**. Vedi §4 per i dati esistenti.

## 3. Dove vive l'invariante — Core, applicato ai bordi UI

Fra tre alternative:

| | | esito |
|---|---|---|
| **A** | regola in Core, applicata ai bordi UI | **scelta** |
| B | validazione in `MetricDefinitionService` | scartata |
| C | CHECK a schema (migration v2.16) | scartata |

B e C sono incompatibili con la scelta di §4 (nessun backfill): un invariante imposto dal service o
dal DB non tollera dati che lo violano, quindi ogni `UpdateAsync` su una durata legacy diventerebbe un
errore e non si potrebbe più nemmeno rinominarla. C dovrebbe per giunta esprimere «NULL vietato solo
se `type='Seconds'`» come CHECK condizionale su una colonna che oggi ha righe legali con NULL.

A è anche l'unica che paga un debito già presente: l'elenco dei codici temporali vive duplicato come
array privato in `HabitBuilderViewModel:62`, mentre `SecondsDisplay.IsTimeUnit` in Core deriva la
stessa nozione per un'altra strada (prova a convertire e guarda se il risultato è null).

**Intervento in Core**: l'elenco dei tre codici diventa pubblico accanto alla regola che già lo usa, in
`SecondsDisplay`; `IsTimeUnit` viene riscritto sopra lo stesso elenco. Nessuna definizione duplicata di
"unità temporale" può più divergere. Le due superfici UI consumano da lì.

Nessun cambio di schema, nessuna migration, `metric_definition.unit_of_measure_id` resta nullable.

## 4. Dati esistenti — nessun backfill

Le durate senza unità già in archivio **restano legali e modificabili**. Nessuna migration data-only le
riempie d'ufficio.

Motivo: sono dati first-party dell'autore, pochi e conosciuti, e l'unità giusta dipende dall'abitudine
(una plank va in secondi, il sonno in ore) — una scelta automatica ne sbaglierebbe la maggior parte.
Vengono sistemate a mano da Configura, dove la tendina ora offre esattamente le tre unità giuste.

Conseguenza accettata: **il ramo "senza unità" resta vivo nel codice** e va conservato, non rimosso.
Finché una metrica legacy non viene sistemata, i suoi aggregati continuano a leggersi come oggi (secondi
grezzi) e le sue registrazioni come orologio.

## 5. Superfici

### 5.1 Builder ("Crea nuova abitudine")

Il selettore h/min/sec sul campo Tempo esiste già ed è già filtrato sui tre codici
(`AvailableTimeUnits`). Nasce vuoto e oggi può restare vuoto.

Il gate di lancio guadagna la clausola gemella di quella già presente per le scale:

```
CanLaunch = nome non vuoto
          && ogni campo Scala ha una scala          (esistente)
          && ogni campo Tempo ha un'unità           (nuova)
```

L'unità di un campo **Quantità** resta opzionale, come oggi e come dice il commento sul gate.

`HabitBuilder.LaunchHint` va corretta in entrambe le `.resx` perché nomini Tempo invece di Quantità.
Nessuna stringa nuova (nessun tocco a `Strings.Designer.cs`, quindi nessun attrito con
`guard-generated.js`).

### 5.2 Configura › Metriche

Due innesti su `MetricRowViewModel`:

1. **Tendina dipendente dal tipo della riga.** Per una durata mostra solo ore/minuti/secondi; per ogni
   altro tipo l'elenco completo di oggi.
   **Più una clausola di salvaguardia**: se la riga porta un'unità che non sarebbe nell'elenco (una
   durata legacy con "km"), quell'unità va inclusa comunque nella lista di *quella* riga. Un
   `SelectedValue` che non trova il proprio elemento nell'`ItemsSource` viene coerciato a `null` da WPF
   e la riga, essendo attaccata, lo persisterebbe: sarebbe il data-wipe di `4d01f9c` per un'altra porta.
   L'unità estranea sparisce quando l'utente ne sceglie una valida, non perché la lista l'ha nascosta.
2. **Passare il tipo a `Seconds` assegna minuti nello stesso istante** — ma solo se serve. Regola
   esatta: se la metrica ha già un'unità temporale valida (una `Decimal` in "hours" portata a durata) la
   **conserva**; se non ha unità, o ne ha una non temporale rimasta da quando era una quantità, riceve
   minuti. Sovrascrivere un'unità già giusta sarebbe una perdita silenziosa di una scelta dell'utente.

Perché minuti e non una tendina vuota: Configura autosalva ogni modifica sul momento
(`OnTypeChanged → UpdateAsync`), non ha un pulsante Salva da disabilitare. Un blocco qui non avrebbe
bersaglio; una preselezione garantisce l'invariante senza inventare una modale in una schermata che non
ne ha mai avute. L'utente cambia subito unità dalla tendina accanto se minuti non va bene.

**Trappola nota, da non ricreare.** La sezione ha oggi una `AvailableUnits` **condivisa** fra tutte le
righe, e `MetricsSectionViewModel.LoadAsync:24` porta il commento che ricorda perché: svuotare un
`ItemsSource` bindato fa collassare a `null` il `SelectedValue` di ogni ComboBox viva, e una riga ancora
attaccata persisterebbe quel null — è il data-wipe del commit `4d01f9c`, che azzerò per davvero delle
unità in archivio. Rendendo la lista **per riga** invece che condivisa, il meccanismo non può ripetersi:
nessuna riga può azzerare la lista di un'altra. Il `Detach()` esistente resta comunque al suo posto.

## 6. Resa a schermo

Decisione: **un evento singolo si legge come orologio, un aggregato si legge nella sua unità.**

```
CRONOLOGIA        Sonno · 1 giu
  Durata: 7:30:00              <- evento singolo

TOTALE OGGI       7,5 ore      <- aggregati
GOAL              7,5 / 10 ore
```

Un solo punto del codice cambia comportamento, `CompletionListViewModel.FormatValue:155`:

```csharp
// oggi
DurationUnitDisplay.InUnit(secs, m.Unit) ?? DurationFormat.Format(secs)
// dopo
DurationFormat.Format(secs)
```

Questo **cambia anche le metriche che un'unità ce l'hanno già**: oggi leggono `7,5 ore` in Cronologia,
dopo leggeranno `7:30:00`. È intenzionale ed è il test discriminante di §8.

`DurationUnitDisplay` (WPF) non ha altri chiamanti — era nato per quella riga sola, come fix F1
dell'Onda 2.3. Va **eliminato** insieme al suo file di test. Questa modifica toglie codice.

I cinque siti che aggregano (`HabitTreeQueryService` riga Home, `CounterTodayContent` totale "Oggi",
`HabitScreenViewModel:1047` striscia del giorno, `GoalProgressCardViewModel`,
`ExportQueryService.BuildSummaryAsync`) chiamano già tutti `SecondsDisplay.ConvertTotal` e non si
toccano.

### 6.1 L'eccezione dichiarata: le righe dell'export

Nell'export anche la **riga singola** resta numero + unità (`7.5` / `hours`), non orologio.

Il motivo è che il file esportato è un formato dati, non una lettura a schermo: una colonna `.csv` deve
poter essere sommata in un foglio di calcolo, e `7:30:00` non è un numero. La regola «singolo =
orologio» è una regola di presentazione nell'app.

Effetto collaterale utile: non serve un formatter di durata *invariant* in Core. `DurationFormat` vive
in `Recordum.WPF` e ha suffissi italiani hardcoded (`"g"`, `"a"`, `"appena ora"`) — sarebbe inutilizzabile
da `ExportEngine` e sbagliato in un file invariant. Questa era la ragione tecnica della domanda aperta
che la spec chiude.

## 7. Fuori scope

- **Schema e migration**: nessuno dei due serve.
- **Il selettore h/min/sec della cella quick-add**: è l'unità con cui *digiti* una registrazione, un
  concetto distinto dall'unità della metrica. Continua a preselezionarsi da essa e a ripiegare su minuti
  per le metriche legacy.
- **Template**: un template salvato in passato potrebbe portarsi dietro una durata senza unità, e la
  materializzazione non lo fermerebbe. È esattamente ciò che già accade per le scale, quindi non
  introduciamo un'asimmetria nuova; se il problema si presenterà riguarderà entrambe insieme.
- **Le altre tre questioni-modello del counter** (flag ripetibile, sonno totale-vs-sessioni, più
  cronometri su `active_timer` UNIQUE per habit): restano parcheggiate per il giro con Fable.

## 8. Test

TDD, un test per comportamento.

| dove | cosa dimostra |
|---|---|
| Core | l'elenco dei codici temporali e `IsTimeUnit` non possono divergere |
| Builder | "Crea" bloccato con un campo Tempo senza unità |
| Builder | "Crea" sbloccato appena l'unità è scelta |
| Builder | un campo Quantità senza unità lancia comunque (l'unità lì resta opzionale) |
| Configura | passare il tipo a Tempo assegna minuti quando la metrica non ha unità |
| Configura | passare a Tempo scarta un'unità non temporale preesistente |
| Configura | passare a Tempo **conserva** un'unità temporale già scelta (non la sovrascrive con minuti) |
| Configura | la tendina di una durata offre 3 voci, quella di una quantità l'elenco intero |
| Configura | la tendina di una durata legacy con unità estranea include quell'unità, così non viene coerciata a null |
| Configura | ricaricare la sezione non azzera l'unità di una riga (regressione `4d01f9c`) |
| Cronologia | una durata **con** unità legge `7:30:00` — discriminante: oggi fallirebbe leggendo "7,5 ore" |

Da rimuovere: `tests/Recordum.WPF.Tests/Formatting/DurationUnitDisplayTests.cs`, insieme alla classe che
prova.

## 9. Criteri di uscita

1. Un campo Tempo senza unità tiene disabilitato "Crea abitudine", con il suggerimento che lo spiega.
2. Portando una metrica a Tempo in Configura, l'unità è minuti e la tendina offre solo h/min/sec.
3. Una registrazione di 7h30m legge `7:30:00` in Cronologia qualunque unità abbia la metrica.
4. Totale del giorno, goal ed export leggono nell'unità dichiarata, invariati rispetto a oggi.
5. Una durata legacy senza unità continua a funzionare ovunque, e si sistema dalla tendina di Configura.
6. `DurationUnitDisplay` e i suoi test non esistono più.
7. Suite verdi su tutti i progetti.
