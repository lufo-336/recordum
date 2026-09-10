# Sonda Live Preview — l'editor può somigliare a Obsidian?

**Verdetto: raggiungibile, e raggiunto.** Su AvaloniaEdit 12 funzionano tutte e quattro le
condizioni chieste: grassetto, corsivo e titoli si rendono mentre si scrive, i marcatori
(`**`, `*`/`_`, `#`) spariscono, e **sulla riga del cursore la sintassi grezza riappare**, per poi
ri-formattarsi quando il cursore se ne va. Costo dell'implementazione: **358 righe** in cinque file,
commenti inclusi.

> Contesto: questa è la quarta sonda della valutazione Avalonia, e chiude la condizione posta
> dall'autore — *"se l'editor markdown può offrire un'esperienza come quella di Obsidian, non ho
> problemi"*. Le precedenti: `2026-09-09-avalonia-theme-probe.md`,
> `2026-09-09-avalonia-controls-probe.md`, `2026-09-09-avalonia-editor-probe.md`.

## La distinzione che rende la domanda rispondibile

Obsidian ha tre modalità, e "l'esperienza Obsidian" ne indica quasi sempre una sola:

| Modalità | Cos'è | Stato |
|---|---|---|
| **Reading view** | tutto reso, sola lettura | fuori scope, l'autore la valuterà dopo |
| **Source mode** | markdown grezzo con evidenziazione | **già costruita** dalla sonda editor precedente |
| **Live Preview** | reso mentre scrivi, grezzo solo sulla riga del cursore | **oggetto di questa sonda** |

La sonda precedente si era fermata alla Source mode, e da lì veniva la sensazione di perdita
rispetto a Toast UI (che è configurato in WYSIWYG). Questa colma la differenza.

## Come funziona

Due hook distinti di AvaloniaEdit, che fanno due mestieri diversi:

- **`DocumentColorizingTransformer`** (`MarkdownLivePreviewColorizer`, 75 righe) — dipinge:
  peso, corsivo e dimensione sugli intervalli riconosciuti. Aggiunto a `TextView.LineTransformers`.
- **`VisualLineElementGenerator`** (`MarkdownLivePreviewMarkerGenerator`, 58 righe) — **nasconde**:
  sostituisce l'intervallo del marcatore con un `FormattedTextElement` di testo vuoto ma
  `documentLength` pari alla lunghezza del marcatore. Zero pixel a schermo, **zero offset persi nel
  documento**: selezione, undo/redo e posizioni del caret restano intatte. È questa la parte che
  decide la sonda.

Il riconoscimento sta in `MarkdownLineScanner` (101 righe), a regex non sovrapposte: titoli `#{1,3}`,
grassetto `**…**`, corsivo `*…*` e `_…_` con lookaround per non confondersi col grassetto.

**L'eccezione della riga del cursore** è una riga sola dentro il generatore — se la riga è quella del
caret, non c'è nulla da nascondere — più il ridisegno delle due righe coinvolte su
`Caret.PositionChanged`. È la parte che sembrava difficile e non lo è.

## La prova, e perché non è lo screenshot

Gli screenshot ci sono (`livepreview-light.png`, `livepreview-dark.png`) e mostrano in un solo
fotogramma il titolo reso col `#` sparito, la riga del cursore grezza coi `**` visibili, e una terza
riga con grassetto e corsivo **resi** e marcatori spariti.

Ma la prova vera è strutturale, in `LivePreviewStructureTests`: se un marcatore è nascosto, la riga
**visuale** è più corta della riga del **documento**; sulla riga del cursore le due lunghezze
coincidono. Un PNG non dice *quanti* caratteri sono stati nascosti; questo sì.

**Provata contro il codice rotto**, come chiede CLAUDE.md §8:

| Rottura | Esito |
|---|---|
| generatore dei marcatori rimosso | rosso: *"expected VisualLength to be less than 23, but found 23"* |
| eccezione della riga del cursore rimossa | rosso: entrambi i test |
| ripristino | **12/12 verdi** |

## Cosa è peggio di Obsidian, senza sconti

- **Copertura**: quattro costruzioni (titoli h1-h3, grassetto, corsivo). Obsidian rende anche liste,
  citazioni, link, codice inline, blocchi di codice, task list, tabelle. Ognuna è un altro caso nello
  scanner più il suo stile: il costo cresce **linearmente**, senza nuovi meccanismi — il difficile
  (nascondere senza perdere offset, e l'eccezione del caret) è già risolto e non si ripaga per ogni
  costruzione.
- **Granularità dell'eccezione**: qui è la **riga**; Obsidian lavora per blocco e per selezione, con
  comportamenti più fini (una lista resta lista mentre ci scrivi dentro).
- **Niente di ciò che Obsidian ha oltre l'editing**: link interni, backlink, autocompletamento,
  tabelle interattive. Fuori scope, ma va detto per non promettere un'app che non è questa.
- **Nessuna toolbar**: Toast UI ne ha 13 comandi. Chi scrive markdown a mano non la usa; chi non lo
  scrive la vuole.

## Due scoperte d'ambiente che valgono oltre questa sonda

**1. In headless, il primo `AvaloniaEdit` della sessione non fa il proprio passaggio di layout.**
Il `TextView` resta `0x0`, `VisualLines` lancia `VisualLinesInvalidException`, e lo screenshot esce
**con la cornice e senza testo**. Dal secondo in poi si dispone regolarmente. La prima stesura di
questa sonda ci si è arenata: il test **passava** — l'assert guarda il peso del file — mostrando una
schermata vuota. È il test peggiore che esista, verde e cieco, ed è esattamente la trappola che
CLAUDE.md §8 registra da W24-R2/R3.

Rimedio adottato: mostrare una vista *diversa* con un editor prima di quella vera
(`MarkdownEditorProbeView` come "priming"), poi `ForceRenderTimerTick()` + `RunJobs()`. Curiosamente
**non basta primare con la stessa vista**: provato, resta vuota. Chi scriverà i test del porting deve
saperlo, o produrrà una galleria di PNG vuoti tutti verdi.

**2. Il progetto `Recordum.Avalonia` fa ombra al namespace radice `Avalonia`.** Dentro qualunque file
del namespace `Recordum.*`, `Avalonia.Threading.Dispatcher` non risolve: serve
`global::Avalonia.Threading.Dispatcher`. Su un porting di sessanta view è un attrito costante.
Vale la pena valutare un nome diverso (`Recordum.UI.Avalonia`, o `Recordum.Shell.Avalonia`) **prima**
di cominciare, perché rinominarlo dopo tocca ogni file.

## File della sonda

| File | Righe |
|---|---:|
| `Controls/LivePreviewEditorControl.axaml` + `.axaml.cs` | 32 + 92 |
| `Controls/MarkdownLivePreviewColorizer.cs` | 75 |
| `Controls/MarkdownLivePreviewMarkerGenerator.cs` | 58 |
| `Controls/MarkdownLineScanner.cs` | 101 |
| **Totale** | **358** |

Più `Views/LivePreviewProbeView.axaml(.cs)` e due classi di test (struttura + screenshot), che sono
impalcatura della sonda e non prodotto.

## Conseguenza per la migrazione

La condizione dell'autore è soddisfatta: l'esperienza di scrittura può essere quella di Obsidian, non
la sola Source mode. Restano da estendere le costruzioni markdown mancanti — lavoro lineare — e da
decidere se e quando aggiungere una toolbar.

E cade il motivo per tenersi `Markdown.Avalonia`: la Live Preview **non ha un pannello di anteprima**,
quindi la dipendenza alpha con segnali di stallo (v. sonda editor, §"La sorpresa tecnica") esce dal
percorso critico. Se un domani servirà la Reading view, la strada consigliata resta **Markdig** più un
renderer nostro.
