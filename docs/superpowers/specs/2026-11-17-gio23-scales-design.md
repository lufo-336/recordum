# GIO-23 — Scale continue e personalizzabili

> Spec del 2026-11-17, branch `gio23-scales`. Nasce da GIO-23 (`docs/phase-4-issues.md`, sezione "Giornata"), chiesta
> dall'autore il 2026-11-16. Decisioni prese in brainstorming con l'autore; le scelte fatte da Claude sono marcate
> **[scelta di Claude]**.

## 1. Perché

Oggi una scala è una fila di gradini: un'etichetta per ogni intero da `MinValue` a `MaxValue`, e il cursore scatta da
uno all'altro. L'autore vuole che **la forma della scala la decida chi la crea**. I tre scenari che ha descritto:

1. **Umore 1–100** con cinque scaglioni messi su valori scelti da lui, ciascuno col suo nome.
2. **Una scala medica 1–12** con dodici scaglioni fissi, da nominare o no.
3. **Una scala 1–25 senza scaglioni**: solo numeri interi.

Più il caso originale di GIO-23: collocarsi **fra** due scaglioni (1,37 fra "Sto malissimo" e "Sto male").

C'è anche un secondo motivo. **Gli scaglioni sono tag**: l'idea dei tag di Recordum è una rete che collega le
abitudini, e "i giorni in cui sono stato male" deve poterla attraversare.

## 2. Obiettivi, non-obiettivi, anti-obiettivi

**Obiettivi**
- Una scala ha estremi interi scelti dall'utente, una precisione (interi / decimali) e scaglioni facoltativi su valori
  scelti dall'utente.
- Ogni scaglione di una scala dell'utente è un **tag** vero (la stessa entità `Tag` di abitudini e note).
- Una registrazione porta il tag della sua zona, **calcolato** dal valore e mai salvato.
- Nessun dato registrato si perde nella migrazione.

**Non-obiettivi** (in questa spec)
- La schermata di ricerca: è il **"Cerca" unico** (ROADMAP, passo 7). Questa spec gli lascia un obbligo (§9).
- Scale oltre ±100: oltre si misura una quantità, e c'è il contatore.
- Stringere una scala, o tornare da decimali a interi.

**Anti-obiettivi** (sarebbe un fallimento anche se funzionasse)
- Una scala 1–100 con cento etichette o cento tacche a schermo.
- Due scaglioni così vicini da sovrapporsi (1 e 1,2).
- Tag congelati sulle registrazioni che diventano falsi dopo una rinomina o uno spostamento.
- L'elenco dei tag dell'utente invaso dalle etichette tradotte delle scale ufficiali.

## 3. Il modello

**Scala** (`ScaleTemplate`)
- **Estremi** `MinValue`/`MaxValue`: interi in **[−100, 100]**, `Min < Max`.
- **Precisione**: `Integer` (il cursore scatta di 1) o `Decimal` (**due decimali** [scelta di Claude], come 1,37).
- **Scaglioni**: zero o più.
- `LabelsJson` sparisce: i nomi stanno negli scaglioni.

**Scaglione** (`ScaleAnchor`, tabella nuova `scale_anchor`)
- `scale_template_id` (FK, cascade dalla scala), `value` **intero** in [Min, Max], `label` (il **nome libero**, testo,
  nullable, max 100), `tag_id` (FK nullable verso `tag`, **SET NULL** alla cancellazione del tag), `updated_at_ms` come
  le altre entità (LWW per la sync futura) [scelta di Claude].
- **Nome e tag** (decisione dell'autore, 2026-11-17): il nome è quello che si vede ovunque ("Sto malissimo"). Il tag
  l'app lo **ricava dal nome** (`#sto-malissimo`), perché un tag segue la regola del diario (`InlineTags.IsValidName`:
  niente spazi né punteggiatura). Regola del tag ricavato: minuscolo; spazi (anche più di uno) → un trattino; via ogni
  carattere fuori da lettere, cifre, `_` e `-`; trattini ripetuti fusi e tolti ai bordi; al più 64 caratteri. Se il
  risultato non passa `InlineTags.IsValidName` (vuoto, solo cifre) lo scaglione ha il nome ma **niente tag**. Un tag con
  quel nome già esistente si riusa (`ix_tag_name` è unico e `NOCASE`), se no si crea.
- Gli scaglioni stanno **solo sugli interi**, anche nelle scale a decimali: così il caso "1 e 1,2" non esiste.
- Unici per valore nella scala. Lo stesso tag può stare su due scaglioni della stessa scala: la lettura funziona
  comunque, e la migrazione non deve perdere etichette ripetute (§8) [scelta di Claude].
- Lo scaglione **senza tag** esiste: nome che non dà un tag valido, o tag cancellato. Tiene il suo nome.
- Lo scaglione **senza nome** è una tacca: la lettura della zona (§4) lo ignora [scelta di Claude].
- **Scaglione con nome** = con un `label` (scale dell'utente) o con un testo di `OfficialTexts` (scale ufficiali). È su
  questi che lavora la lettura del §4.

**Scale ufficiali** (le sette seminate). I loro scaglioni **non hanno tag**: il nome viene da `OfficialTexts` per
(codice della scala, valore), nella lingua dell'interfaccia, come oggi per indice. Un'ufficiale non si modifica: si
**duplica**, e la copia diventa una scala dell'utente: i nomi tradotti (nella lingua del momento) diventano i suoi
`label`, e i tag si ricavano come sopra. Conseguenza voluta: le registrazioni su
una scala ufficiale si leggono per zona ("Male · 37"), ma **non portano tag** e restano fuori dalla rete dei tag e dal
"Cerca" per tag. Chi le vuole nella rete duplica la scala.

**Stato senza scala**: non si crea più. Il creatore lo impedisce già, e il ripiego 0..10 di `StateTodayContent`
diventa un caso morto che la migrazione (§8.6) svuota [scelta di Claude].

**Il valore registrato** è il **valore vero** della scala, sempre in `CompletionValue.ValueDecimal`. La metrica di una
scala diventa `MetricType.Decimal`. In una scala a interi il valore non ha decimali, e lo garantisce chi scrive.
`MetricDefinition.ScaleTemplateId` resta il collegamento.

**La scala è l'unica fonte dei nomi.** `MetricDefinition.LabelsJson` (la copia per abitudine, che oggi vince sulla
scala) sparisce. Conseguenza voluta: modificare una scala la cambia per **tutte** le abitudini che la usano.
L'editor lo dice ("Usata da 3 abitudini") [scelta di Claude].

## 4. Leggere un valore: la zona

Ci sono due letture, prodotte da una sola regola in Core:

- **Breve (a)**: il nome dello scaglione con nome (§3) **più vicino** e il numero, cioè "Male · 37". A parità di distanza
  vince lo scaglione **sotto** [scelta di Claude]. Si usa dove lo spazio è poco: riga della Home, calendario, elenco
  delle registrazioni, anteprime.
- **Estesa (c)**: "37, fra Male e Così così", con i due scaglioni con nome che stanno attorno. Si usa nella pagina
  dell'abitudine: il cursore mentre si registra e il valore di oggi.
- **Casi limite**: sul valore esatto di uno scaglione → solo il nome ("Così così"). Prima del primo scaglione o dopo
  l'ultimo, la (c) diventa la (a). Una scala senza scaglioni con nome → solo il numero, ovunque.
- Il numero si formatta con la cultura corrente: due decimali solo se la scala è a decimali.

**Il tag della registrazione** è il tag dello scaglione della lettura (a), se ne ha uno (sulle ufficiali no, §3). È calcolato e non si salva.

## 5. Tag: la rete

- Lo scaglione di una scala dell'utente porta un tag, ricavato dal suo nome (§3).
- **Rinominare lo scaglione** ricava il tag nuovo e ripunta lo scaglione; il tag vecchio resta (altre cose possono usarlo).
- **Rinominare il tag** da altrove (quando esisterà una gestione dei tag) non cambia il nome dello scaglione: il
  collegamento resta.
- **Cancellare il tag** lascia lo scaglione col suo nome e senza tag (`tag_id` a null). Non si blocca la cancellazione.
- Una registrazione porta il tag della sua zona (§4). Il tag sta sulla **registrazione**, non sull'abitudine: il filtro
  per tag della Home (GIO-22, che filtra abitudini) **non** vede i tag delle zone. Altrimenti "Umore" comparirebbe
  sotto Male, Bene e Così così insieme.
- Lo stesso tag in due scale è lo stesso tag: `#male` collega umore, dolore, sonno. Se per l'utente sono cose diverse,
  usa due tag. Per restringere combina (vedi §9).
- In questa spec il tag di uno scaglione **si vede** nell'editor della scala (sotto il nome). La ricerca che attraversa
  i tag delle zone arriva col "Cerca".

## 6. Modificare una scala

- **Nome, scaglioni, tag**: liberi. Cambiano solo come si *legge* lo storico, mai i numeri salvati.
- **Estremi**: solo **allargare**. Stringere non si può (decisione dell'autore): un valore già registrato resterebbe
  fuori. Chi vuole una scala più stretta ne crea una nuova.
- **Precisione**: da interi a decimali sì, il contrario no.
- Queste regole valgono in ogni punto che scrive una scala (servizio), non solo nell'editor: l'editor le mostra, il
  servizio le impone.
- Le ufficiali non si modificano: "Duplica" (§3). In Configura › Scale, su un'ufficiale l'editor è in sola lettura con
  "Duplica e modifica". La copia ripunta **solo** l'abitudine da cui si è partiti; le altre restano sull'ufficiale
  [scelta di Claude].

## 7. L'editor della scala (impostazione B, scelta dall'autore)

L'elenco prima, la barra sotto come anteprima (mockup `.superpowers/brainstorm/…/scale-editor.html`, opzione B):

```
Nome        [Umore            ]
Da / a      [ 1 ] – [ 100 ]
Precisione  (Interi | Decimali)

[  1 ]  [Malissimo   ]  #malissimo   ×
[ 25 ]  [Male        ]  #male        ×
[ 50 ]  [Così così   ]  #così-così   ×
[ 75 ]  [Bene        ]  #bene        ×
[100 ]  [Benissimo   ]  #benissimo   ×
+ Scaglione     [Un nome per ogni valore]

━●━━━━━━━━━●━━━━━━━━━━●━━━━━━━━━━●━━━━━━━━━━●   ← anteprima, solo i nomi degli scaglioni
```

- Ogni riga: valore (campo numerico intero), nome (testo libero) e, in piccolo, il tag che ne ricava l'app
  (nessun tag mostrato se il nome non ne dà uno). Il tag non si sceglie a mano: segue il nome (decisione dell'autore).
- **"Un nome per ogni valore"**: crea una riga vuota per ogni intero. C'è solo se la scala ha **al più 12 valori**
  [scelta di Claude: copre la scala medica 1–12, il dolore 0–10, lo stress].
- **Tacche** sulla barra (anteprima e cursore di registrazione): una per intero solo se i valori sono al più 12;
  altrimenti solo gli scaglioni.
- Le regole del §6 si vedono qui. Il "Da" non può salire e l'"a" non può scendere sotto lo storico, e i due campi dicono
  perché. "Decimali → Interi" è disabilitato se ci sono registrazioni.
- **Dove vive**: nel creatore (il modulo "nuova scala" di oggi, `NewScaleFormViewModel`) e in Configura › Scale, che
  oggi modifica la copia per abitudine e d'ora in poi modifica la scala [scelta di Claude]. Stesso controllo in tutti
  e due i posti, disegnato una volta per computer e telefono.
- Il cursore di registrazione (Stato, e la cella di scala nel quick-add) va da Min a Max col passo della precisione.
  Accanto c'è un campo numerico per il valore esatto [scelta di Claude: su 1–100 col dito non si centra il 62].

## 8. Migrazione (schema 2.19 → 2.20)

È il punto delicato per i dati dell'autore.

**Il riferimento è la posizione, non Min/Max.** Oggi il cursore si dimensiona sul numero delle etichette
(`StateTodayContent.cs:85-86`), e i modelli possono avere Min/Max che non coincidono con quel numero
(`HabitTemplateService.cs:176-188`). Quindi la posizione salvata *p* (da 1) vale `min + p − 1`. Se il valore cade oltre
`max`, la scala si **allarga** fino a contenerlo: non si tronca e non si interrompe [scelta di Claude]. Lo stesso vale per
le etichette: l'etichetta in posizione *p* va sullo scaglione `min + p − 1`.

1. **Valori**: per ogni `completion_value` di una metrica di scala (vedi 6 per quelle senza scala):
   `value_decimal = min + value_integer − 1`, `value_integer = NULL`. Sul dolore 0–10, il 6 salvato diventa 5. La
   metrica diventa `Decimal`. Le scale esistenti diventano a precisione `Integer`.
2. **Scale ufficiali**: i loro scaglioni si seminano (uno per valore, senza tag). `labels_json` si toglie.
3. **Scale dell'utente**: ogni etichetta diventa uno scaglione con quel `label` e col tag ricavato (§3: riusato se
   c'è, creato se no). Un'etichetta vuota, o uguale al proprio numero ("2", "3"…), non crea lo scaglione. Le etichette
   ripetute danno lo stesso tag su due scaglioni, ed è permesso (§3) [scelte di Claude].
4. **Copie per abitudine** (`metric_definition.labels_json`). Il creatore e Configura le scrivono **sempre**, nella lingua
   dell'interfaccia (`HabitBuilderViewModel.cs:803`, `ScalesSectionViewModel.cs:127`). Una copia è **vera** solo se,
   confrontata come elenco (non come testo JSON), è diversa sia dalle etichette della scala sia da quelle ufficiali di
   **ogni lingua distribuita** (en, it). Le copie non vere si buttano. Per una copia vera si crea una scala dell'utente
   propria, "‹nome della scala› · ‹nome dell'abitudine›", con quelle etichette (come al punto 3), e la metrica si
   ripunta lì. Poi la colonna si toglie. Le traduzioni stanno nei `.resx` di Presentation, che Data non vede: la
   migrazione se ne porta una **copia congelata** (en, it, alla 2.20) [scelta di Claude]. Una migrazione è storia: non
   deve seguire le traduzioni future.
5. **Etichette senza scala**: una metrica con `labels_json` ma senza `scale_template_id` (il "Umore" di
   `DemoDataSeeder.cs:118-124`) diventa una scala dell'utente 1…N con quelle etichette, e i valori si convertono come
   al punto 1 con `min = 1`.
6. **Stato senza scala né etichette** (il ripiego 0..10, posizioni 1..11): diventa una scala dell'utente
   "‹nome dell'abitudine›" 0–10 senza scaglioni, valori `p − 1` [scelta di Claude].
7. Tutto in una transazione; un DB a metà non deve esistere. La migrazione si prova su un DB 2.19 con dati veri di
   ogni caso: ufficiale, ufficiale con copia in italiano (non vera), copia vera, scala dell'utente, etichette ripetute,
   metrica con etichette senza scala, Stato senza scala, scala con minimo 0, valore oltre `max`, valore agli estremi.

Come scrivere la parte che crea GUID e tag (SQL nella migrazione EF o un passo C# a valle con la sua versione) lo
decide il piano. Vincoli: atomica, idempotente, e un backup `.recbak` 2.18 o 2.19 si ripristina passando dalla stessa
strada.

**Archiviazione di `value_decimal`**: resta com'è (TEXT, `HasPrecision(18,6)`). Il "Cerca" leggerà i valori per abitudine
e applicherà la regola del §4 in memoria: è al più un valore al giorno per abitudine, poche migliaia di righe in anni
[scelta di Claude; se un giorno servisse filtrare in SQL, una colonna REAL calcolata si aggiunge senza toccare questa].

## 9. Obblighi verso il "Cerca" (passo 7)

- Il "Cerca" troverà le registrazioni **per tag di zona**, applicando la regola del §4 in memoria sui valori letti (vedi §8, archiviazione), o con un indice
  ricalcolabile, mai con tag salvati sulle registrazioni).
- Filtro a più tag in **E** ("tutti questi"), combinabile con l'abitudine: è il modo con cui l'utente separa "Male"
  dell'umore da "Male" del sonno (decisione dell'autore).

## 10. Cosa tocca

L'elenco esatto lo dà il compilatore quando spariscono `ScaleTemplate.LabelsJson` e `MetricDefinition.LabelsJson`.
Più tutti i punti che leggono o scrivono un `ValueInteger` di scala (quelli il compilatore non li vede: grep +
test). Noti al 2026-11-17:

- **Scrivono**: `StateTodayContent` (cursore Stato), la cella di scala del quick-add (`SeriesCellViewModel` /
  `MetricCellFactory`), modifica e registrazione retroattiva, `DemoDataSeeder`, l'anteprima del creatore
  (`SamplePreviewFactory`), `ComposedHabitService` e `HabitTemplateService` (scale staged / dei modelli).
- **Leggono**: la riga della Home (`HabitTreeQueryService`, striscia dello Stato), il calendario e la heatmap
  (`GetDailyMagnitudeAsync`: i 5 gradini si calcolano su `(v − min) / (max − min)`), elenco delle registrazioni,
  cifre della pagina (`HabitFiguresViewModel`), grafici (asse da Min a Max), export (si esporta il valore vero: per
  CSV/JSON cambia rispetto a oggi, va detto nelle note di versione), `ScalesSectionViewModel`, `OfficialTexts`
  (nome per codice e valore).
- **Modelli** (`HabitTemplate` payload): una scala nel modello porta estremi, precisione e scaglioni (valore + nome del
  tag). Un payload vecchio (`Labels` = elenco) si legge come scaglioni su 1…N. Principio 8: serve una fixture in
  `BuilderTemplateParityTests` con una scala a decimali e scaglioni sparsi.
- **WPF** (congelata): non ha scrittori di scala propri, passa dai ViewModel di Presentation: corretti quelli, WPF
  scrive il valore vero. Deve compilare e **non deve scrivere posizioni** sullo schema nuovo, altrimenti corrompe i
  dati. Il minimo: scrive il valore vero. Che mostri solo il numero va bene [scelta di Claude].
- **Backup/restore**: il ripristino di un `.recbak` 2.18 o 2.19 migra come sopra.

## 11. Approcci considerati

1. **Scaglioni in una tabella loro, tag con FK, valori veri in `value_decimal`, copia per abitudine ritirata** —
   **scelto dall'autore**. Rinomine e cancellazioni dei tag si riflettono da sole; il "Cerca" usa una query normale.
   Costo: la migrazione più grossa.
2. **Scaglioni nel JSON della scala** (`[{value, tagId}]`). Migrazione più piccola, ma il tag non è collegato davvero:
   ogni rinomina o cancellazione costringe a riscrivere i JSON, e il "Cerca" leggerebbe testo. Va contro il motivo
   dei tag. Scartato.
3. **Interi in `value_integer` e decimali in `value_decimal`** secondo la precisione. Due modi di leggere ogni scala, e
   passare a decimali sposterebbe i valori. Scartato.

## 12. Test obbligati

- **Regola della zona** (Core, puri): più vicino, parità → sotto, sul valore esatto, prima del primo e dopo l'ultimo
  scaglione, scaglione senza tag ignorato, scaglione ufficiale letto da `OfficialTexts` (e senza tag), scala senza scaglioni, formattazione con due culture.
- **Migrazione** su un DB 2.19: ogni caso del §8, con i valori letti prima e dopo. Rosso provato contro una
  conversione sbagliata (per esempio senza `− 1`).
- **Regole di modifica** nel servizio: stringere → rifiutato, decimali → interi con storico → rifiutato, scaglione
  fuori estremi o doppio → rifiutato.
- **Tag ricavato**: "Sto malissimo" → `sto-malissimo`, "Così  così!" → `così-così`, "7" → nessun tag, nome di 80
  caratteri → 64; tag esistente riusato (anche con maiuscole diverse). Rinomina dello scaglione → tag nuovo, il vecchio
  resta. Cancellazione del tag → lo scaglione tiene il nome, registrazioni intatte.
- **Cursore**: su una scala a decimali registra 1,37 e lo rilegge 1,37. Su una scala a interi non produce decimali
  (parametro muto: la precisione di default deve rispondere diverso).
- **Parità creatore ↔ modelli**: fixture nuova (Principio 8).
- **Avalonia**: PNG dell'editor (chiaro/scuro, computer/telefono stretto), della pagina dello Stato su 1–100 e 1–12,
  della riga della Home; ciascuno con un'asserzione strutturale.

## 13. Lasciato a chi costruisce

- La forma esatta della migrazione (§8), dentro i vincoli scritti.
- I nomi di tipi, enum e colonne (`ScaleAnchor`, `ScalePrecision`…).
- Come il selettore di tag dell'editor riusa quello esistente.
- Lo stile delle tacche e dell'anteprima, dentro `docs/PRODUCT.md` §"Come si presenta".
