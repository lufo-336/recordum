# Recordum — Restyle riga gruppo: cornice contenitore (BC3)

> Follow-up estetico richiesto dall'utente dopo la verifica di gruppi/multi-selezione
> (2026-06-12): il gruppo "non sembra un gruppo", il ToggleButton quadrato è antiestetico,
> i figli non sono indentati. Scelta visiva lockata via visual companion: **BC3 — cornice
> contenitore** (mockup in `.superpowers/brainstorm/292-1781225755/content/group-row-style-v2.html`).

## 1. Aspetto

- **Gruppo chiuso**: card tinta del colore del gruppo (come oggi) ma con **chevron ▸**
  al posto del quadrato, nome in **grassetto**, conteggio figli a destra.
- **Gruppo aperto**: card + figli vivono dentro una **cornice arrotondata** (bordo 1px +
  velo di fondo) nel **colore del gruppo**; chevron **▾**; figli **indentati** dentro la
  cornice. I gruppi annidati ripetono la cornice al loro livello.
- Chevron: geometrie esistenti `Icon.ChevronRight`/rotazione, ToggleButton senza chrome
  (trasparente, hover leggero), bound a `IsExpanded` come oggi.

## 2. Architettura — view gerarchica, logica piatta

La cornice deve racchiudere i figli ⇒ i figli devono stare **dentro il template del
gruppo**. La view passa a rendering ricorsivo; la logica resta sulla lista piatta:

- **`HomeRowViewModel`**: nuova `ObservableCollection<HomeRowViewModel> Children`
  (popolata da `Rebuild`, vuota per habit e per gruppi collassati).
- **`HomeViewModel`**: `Rebuild` costruisce — in parallelo alla flat `Rows` esistente
  (che resta la fonte di verità per selezione/shift-range/drag/SelectById) — la nuova
  `ObservableCollection<HomeRowViewModel> Roots` (righe di livello 0) annidando i figli
  via stack dei livelli. Nelle tab Archiviate/Cestino (gerarchia appiattita a livello 0)
  `Roots` coincide con `Rows`.
- **View**: l'ItemsControl esterno binda `Roots`. `GroupRowTemplate` diventa: Border
  cornice (visibile solo se `IsExpanded` e `ChildCount > 0`) → card gruppo + ItemsControl
  interno su `Children` (indentato), ricorsivo per i sottogruppi.
- **Selettore per chiave**: il selettore attuale richiede riferimenti espliciti ai
  template (autoreferenza impossibile dentro `GroupRowTemplate.xaml`). `RowTemplateSelector`
  risolve invece i template per chiave via `FrameworkElement.FindResource` dal container
  (`"HabitRowTemplate"`/`"GroupRowTemplate"` sono nei dizionari merged di HomeView).
- **Drag-drop/click**: gli handler sono attaccati all'ItemsControl esterno; gli eventi
  routed delle righe interne vi risalgono per bubbling — nessun cambio al behavior.
  L'indentazione nasce dal nesting: niente converter su `IndentLevel`.

## 3. Fuori scope

- Animazioni di apertura/chiusura della cornice.
- Restyle della riga habit (resta com'è).
- Virtualizzazione (liste piccole, ItemsControl semplice come oggi).

## 4. Testing

- `Rebuild` costruisce `Roots`/`Children` coerenti con la flat `Rows` (annidamento,
  gruppo collassato → `Children` vuoti, tab non-Active → tutto in `Roots`).
- Selezione/shift-range/bulk continuano a passare (suite esistente, nessun cambio API).
- Verifica visiva: chevron ruota, cornice nel colore del gruppo, indent figli, gruppi
  annidati, drag-drop dentro/fuori cornice, highlight drop-target, due temi.
