# Template picker — sezioni modelli collassabili (#8)

**Data**: 2026-06-11 · **Branch**: `fase-4-polish` · **Stato**: design approvato (mockup `.superpowers/brainstorm/.../picker-collapsible.html`)

## Problema

La finestra **"Nuova abitudine da modello"** (`HabitTemplatePicker`) elenca in un'unica lista piatta e lunga: due azioni ("Costruisci da zero", "Gruppo vuoto"), tutti i modelli predefiniti e i modelli personalizzati. Con molti modelli la lista è ingombrante e non c'è modo di comprimerla.

## Obiettivo

Rendere **collassabili** i due blocchi di modelli (predefiniti e personalizzati), tenendo sempre visibili le due azioni primarie. Nessun redesign del builder. La voce "Gruppo vuoto" esiste già: il "crea nuovo gruppo" di #8 è quindi già coperto.

## Design

Struttura della finestra, dall'alto:

1. **Azioni (sempre visibili)** — "Costruisci da zero" + "Gruppo vuoto". Fuori da qualsiasi sezione collassabile.
2. **Modelli predefiniti** — `Expander` con i template ufficiali (`IsOfficial`). Intestazione con conteggio (es. "Modelli predefiniti (9)"). **Espansa di default.**
3. **I tuoi modelli** — `Expander` con i template personalizzati (`IsCustom`), con le affordance rename/delete già presenti. Intestazione con conteggio. **Espansa di default.** L'intera sezione resta **nascosta se vuota** (comportamento attuale).

Note:
- Lo stato aperto/chiuso **non** viene persistito (riapre sempre espanso). Scelta esplicita dell'utente.
- Solo presentazione: la logica di pick/instantiate/rename/delete resta invariata.

## Modifiche

### ViewModel — `HabitTemplatePickerViewModel`
Oggi `Options` mischia le 2 azioni con i modelli ufficiali. Separare in due collezioni così la view può intestare/contare i soli modelli:
- `Actions` : `ObservableCollection<PickerOption>` — "Costruisci da zero", "Gruppo vuoto".
- `OfficialTemplates` : `ObservableCollection<PickerOption>` (rinominato da `Options`) — **solo** i modelli ufficiali.
- `CustomOptions` : invariato.

`LoadAsync` popola `Actions` con le due voci blank e smista i template ufficiali in `OfficialTemplates`, i custom in `CustomOptions` (come ora).

### View — `HabitTemplatePicker.xaml`
- Lista azioni in cima (ItemsControl su `Actions`, stesso template-bottone attuale).
- `Expander` "Modelli predefiniti" (Header con conteggio via binding su `OfficialTemplates.Count`), `IsExpanded="True"`, contenuto = ItemsControl su `OfficialTemplates`.
- `Expander` "I tuoi modelli" (Header con conteggio), `IsExpanded="True"`, contenuto = l'attuale blocco custom (rename/delete). La sezione si nasconde se `CustomOptions.Count == 0` (come ora).
- Serve una nuova stringa `.resx` (EN+IT) per l'intestazione "Predefined templates"/"Modelli predefiniti"; "Your templates" esiste già (`HabitTemplatePicker_YourTemplates`). Conteggio mostrato via `StringFormat` sull'header (o un secondo `Run`/`TextBlock`).
- Riusare lo stile `Expander` implicito già presente in `BaseStyles.xaml` (riga ~320, usato da `EditorPane`) — già tematizzato. Nessun nuovo stile necessario.

## Testing
- `HabitTemplatePickerViewModelTests`: `LoadAsync` popola `Actions` con le 2 azioni, `OfficialTemplates` coi soli ufficiali, `CustomOptions` coi soli custom (nessuna azione finita tra i modelli).
- Verifica visiva utente: collasso/espansione, conteggi, sezione custom nascosta se vuota, tema chiaro/scuro.

## Fuori scope
- Persistenza stato espansione.
- Ricerca/filtro modelli, anteprive ricche, redesign builder. (Eventuali follow-up separati.)
