# Creation pickers — colore + icona (design)

> **Stato**: approvato 2026-06-09. Fase 4 Polish/UX, branch `fase-4-polish`.
> **Tracker**: chiude `R-builderColorPicker` + `R-builderIconPicker` (e migliora gli stessi controlli in Configura). Vedi `docs/phase-4-issues.md`, sezione "Stato al 2026-06-09".

## 1. Problema

Il builder "Costruisci da zero" (`HabitBuilderView`) raccoglie colore e icona con **TextBox grezze**: il colore si inserisce solo come esadecimale (`#808080`) e il quadratino accanto **non è cliccabile**; l'icona è una casella di testo dove si dovrebbe digitare una chiave. L'utente non conosce gli hex e non sa quali chiavi icona esistano → la creazione è ostile.

Esistono già due controlli condivisi (`Views/ColorSwatchPicker`, `Views/IconPicker`) usati nell'editor (`EditorPane`), ma:
- **il builder non li usa affatto** (da qui le TextBox grezze);
- sono **basici**: il colore offre 12 swatch fissi + hex (niente sfumature, niente "recenti"); l'icona mostra solo l'anteprima della geometria e una casella dove **digitare la chiave** — la griglia selezionabile è esplicitamente deferita, e `Resources/Icons.xaml` contiene **solo 7 icone**.

## 2. Obiettivo

Due picker **condivisi** builder + Configura, gradevoli e on-brand:
- **Colore**: palette curata multi-tonalità/sfumatura + colori recenti + hex, tutto cliccabile, in un popup.
- **Icona**: set Lucide curato (~40) in una griglia cliccabile con ricerca.

On-brand = design brief (`Recordum_Prototype_DesignBrief.md`): "colore = identificatore, mai decorazione"; riferimenti Linear/Things (palette curate, non ruote HSV); icone Lucide line-style.

### Non-obiettivi (YAGNI)
- Ruota colore HSV libera (fuori brief, più codice).
- Colori/scale/unità personalizzati creati dall'utente (sono `R-builderCustomUnit/CustomScale`, feature a sé).
- "Recenti" per le icone (non richiesto).
- Catalogo Lucide completo (migliaia di icone): si parte da ~40 curate; l'espansione è incrementale e additiva.

## 3. Approccio di fondo

**Potenziare i due controlli esistenti in-place**, non crearne di nuovi. L'editor li usa già (`EditorPane.xaml`), quindi eredita il miglioramento senza modifiche; il builder li adotta sostituendo le TextBox. Un solo punto di manutenzione, zero divergenza.

I controlli restano **VM-agnostici**: espongono Dependency Property (`Color`, `Icon`, più la nuova `RecentColors`) e non conoscono i view-model. La persistenza dei recenti vive nel VM host.

## 4. Color picker — `ColorSwatchPicker` potenziato

### 4.1 Interazione
- Un **trigger** compatto (Border della dimensione attuale ~28px) che mostra il colore corrente come sfondo. Click → apre un **Popup** (`StaysOpen=False`, `Placement=Bottom`).
- Nel popup, dall'alto:
  1. **Griglia palette**: rampe curate. ~8 tonalità (colonne) × 3 sfumature (righe) + una riga neutri (grigi). Ogni cella è un Border cliccabile ~20px. Click → imposta `Color` + chiude il popup.
  2. **Riga "Recenti"** (visibile solo se `RecentColors` non vuota): fino a 8 swatch dai colori usati di recente.
  3. **Casella hex** + anteprima del colore corrente. Edit con commit su `LostFocus`/Invio (come oggi). Hex non valido → ignorato (nessun crash; il valore resta l'ultimo valido).

### 4.2 Palette
Definita come costante statica nel controllo (estende l'attuale `Palette`). Rampe stile Tailwind (le 12 attuali sono già i 500-level): per ogni tonalità si aggiungono un livello più chiaro e uno più scuro. Esempio per "blue": `#93c5fd` / `#3b82f6` / `#1d4ed8`. Totale ~24-28 swatch + 4 neutri. Valori esatti fissati in implementazione (palette curata, non generata).

### 4.3 Recenti — `IRecentColorStore`
Nuovo servizio in `Recordum.WPF` (è UI-state, non dominio):
```
public interface IRecentColorStore
{
    ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken ct = default);
    ValueTask AddAsync(string hex, CancellationToken ct = default);
}
```
- Implementazione su `IAppSettingService`, chiave `ui.recent_colors` (CSV di hex `#RRGGBB`).
- `AddAsync`: normalizza l'hex (lowercase, valida `#RRGGBB`), lo mette in testa, **dedup** (rimuove duplicati esistenti), **cap a 8**, persiste. Hex non valido → no-op.
- Registrazione **al commit reale**, non a ogni cambio: builder su **Lancia** (dopo `CreateComposedHabitAsync` riuscito), editor su **Applica/save**.
- Il VM host carica i recenti in init (`InitializeAsync`/`LoadAsync`) in una collezione esposta; il controllo li mostra via la DP `RecentColors`.

### 4.4 Nuova DP
`RecentColors` (`IEnumerable<string>`, default vuoto). Il controllo la renderizza nella riga "Recenti". `Color` resta come oggi (two-way, default `#808080`).

## 5. Icon picker — `IconPicker` potenziato

### 5.1 Set Lucide curato
Importare **~40 icone Lucide** rilevanti per le abitudini in `Resources/Icons.xaml` come `Geometry` (resource key = nome icona Lucide, es. `dumbbell`, `book-open`, `heart`, `moon`, `droplet`, `footprints`, `brain`, `pill`, `cigarette`, `utensils`…). Categorie: sport/movimento, salute/medico, studio/lavoro, casa/cura, cibo/idratazione, sonno/mente, astinenza.

**Sorgente**: path data dalle SVG ufficiali Lucide (MIT), viewBox `0 0 24 24`, stroke-based — coerenti col rendering attuale (`Path` con `Stroke`, `StrokeThickness=1.5`, line-cap round). **Non** ricostruite a memoria: prese dalle SVG ufficiali in fase di implementazione. Le 7 esistenti restano.

### 5.2 Interazione
- Il box anteprima esistente (28px, mostra la geometria corrente) diventa il **trigger** → apre un **Popup**.
- Nel popup:
  1. Casella **ricerca** (filtra per nome icona, case-insensitive, substring).
  2. **Griglia** (`WrapPanel`/`ItemsControl`) delle icone filtrate: ogni cella è un bottone con il `Path` dell'icona. Click → imposta `Icon` (la chiave) + chiude.
- Catalogo + filtro in un'unità testabile (vedi §7), non logica nel code-behind.

### 5.3 Catalogo icone
Piccolo tipo che enumera le chiavi disponibili e filtra per nome:
```
public sealed record IconCatalogEntry(string Key);   // Key = resource key = nome Lucide
public static class IconCatalog
{
    public static IReadOnlyList<string> All { get; }          // i nomi curati
    public static IReadOnlyList<string> Search(string? query); // substring case-insensitive; vuoto → All
}
```
La lista `All` è la singola fonte di verità dei nomi; deve restare allineata alle key in `Icons.xaml` (un test lo verifica, §7).

## 6. Adozione

- **Builder** (`HabitBuilderView.xaml`): sostituire la TextBox "Colore (hex)" + quadratino con `<v:ColorSwatchPicker Color="{Binding Color}" RecentColors="{Binding RecentColors}"/>`; la TextBox "Icona" con `<v:IconPicker Icon="{Binding Icon}"/>`. `HabitBuilderViewModel`: nuova collezione `RecentColors` caricata in `InitializeAsync` da `IRecentColorStore`; chiamata `AddAsync(Color)` dopo `LaunchAsync` riuscito.
- **Editor** (`EditorPane.xaml`): usa già i controlli — serve solo **una riga**: aggiungere `RecentColors="{Binding RecentColors}"` al `ColorSwatchPicker` esistente. `HabitEditorViewModel`: espone `RecentColors` (caricata da `IRecentColorStore` in `LoadAsync`) e chiama `AddAsync(Color)` al salvataggio (`ApplyAsync`).
- DI: registrare `IRecentColorStore` in `ServiceRegistrar`.

## 7. Testing

Logica in unità isolate, testate (WPF test project):
- **`RecentColorStore`** con fake `IAppSettingService`: aggiunge in testa; dedup (ri-aggiungere un colore lo porta in cima senza duplicati); cap a 8 (il 9° espelle il più vecchio); hex non valido → no-op; round-trip CSV.
- **`IconCatalog.Search`**: vuoto/null → tutti; substring case-insensitive; nessun match → vuota.
- **Allineamento catalogo↔risorse**: test che ogni chiave di `IconCatalog.All` risolva a una `Geometry` in `Icons.xaml` (via `Application` resources o caricando il ResourceDictionary) → previene nomi orfani.
- VM: `HabitBuilderViewModel.InitializeAsync` popola `RecentColors` dallo store; `LaunchAsync` riuscito chiama `store.AddAsync(Color)`.

I controlli XAML (popup, griglie, ricerca) → **verifica visiva utente** al relaunch (skill `run`).

## 8. File toccati (indicativo)

- `src/Recordum.WPF/Views/ColorSwatchPicker.xaml(.cs)` — popup, griglia palette, riga recenti, DP `RecentColors`.
- `src/Recordum.WPF/Views/IconPicker.xaml(.cs)` — popup, griglia, ricerca.
- `src/Recordum.WPF/Resources/Icons.xaml` — +~40 geometrie Lucide.
- `src/Recordum.WPF/Services/RecentColorStore.cs` (+ interfaccia) — nuovo.
- `src/Recordum.WPF/Services/IconCatalog.cs` — nuovo.
- `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs` — `RecentColors` + AddAsync su Launch.
- `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs` — `RecentColors` + AddAsync su Apply.
- `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml` — adozione controlli.
- `src/Recordum.WPF/Views/EditorPane.xaml` — bind `RecentColors`.
- `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` — DI `IRecentColorStore`.
- `tests/Recordum.WPF.Tests/...` — RecentColorStore, IconCatalog, allineamento risorse, VM.

## 9. Sequenza (per il piano)

1. `RecentColorStore` + DI + test.
2. `ColorSwatchPicker` potenziato (palette + recenti + hex popup) + DP `RecentColors`.
3. Adozione colore in builder + editor (carica/registra recenti) + test VM.
4. `IconCatalog` + import ~40 geometrie Lucide in `Icons.xaml` + test allineamento.
5. `IconPicker` potenziato (popup + griglia + ricerca).
6. Adozione icona in builder (l'editor la usa già).

Colore prima (alto valore, basso rischio); icone dopo (l'import geometrie è la parte corposa).
