# Recordum — UI delle sezioni dell'Habit Editor (B6c)

- **Data**: 2026-05-29
- **Stato**: design approvato in brainstorming, in attesa di review dello spec
- **Fase**: 4 (Polish/UX) — issue **B6c** (personalizzazione habit assente nella UI)
- **Branch**: `fase-4-polish`

---

## 1. Problema

`EditorPane.xaml` mostra solo l'header L1 (Nome/Descrizione/tipo nodo+habit/colore/icona) + un footer di stato. Il pannello `SectionsHost` è **vuoto** (`<!-- Section views injected in Phase D -->`) e il code-behind non inietta nulla. Le 11 sezioni di personalizzazione (Scheduling, Streak, Timer, Metrics, Scales, Goal, Notifications, Tags, AI sharing, DataSource, ResetTime) esistono come **ViewModel completi e testati** (Milestone 3.4) ma **non hanno alcuna View**. Risultato: l'utente non può configurare metriche, scala, timer, goal, schedule, streak — dipende interamente dai template.

## 2. Decisione

Costruire le View mancanti e comporle in `EditorPane` con un host **data-driven**:
- una **View piccola per sezione** (UserControl) bound al rispettivo VM;
- un `ItemsControl` sulla lista ordinata `Sections` esposta da `HabitEditorViewModel`, ogni item avvolto in un **Expander** collassabile;
- mapping VM→View via **DataTemplate impliciti**.

Nessuna logica nuova nei VM oltre a 3 piccole proprietà di presentazione (vedi §5): autosave per-campo e validazioni restano invariati.

Scope: **tutte le sezioni applicabili**. `DataSource` è un placeholder e viene **escluso** da questo giro.

## 3. Layout (progressive disclosure — Design Brief §9)

- **L1**: header esistente (invariato).
- **L2**: ogni sezione è un **Expander collassato di default**, in ordine fisso.
- **L3**: nelle sezioni con opzioni rare, un toggle **"Advanced"** (flag `ShowAdvanced` già presente) rivela i campi avanzati (es. `streak_tolerance_minutes`, `timer_auto_prompt`, `rest_timer_seconds`).

```
┌ Configura ───────────────────── ✕ ┐
│ [Nome]          [tipo][colore][icona] │   ← L1 (esistente)
│ ▸ Scheduling                          │   ← L2 Expander collassati
│ ▸ Streak                              │
│ ▾ Metrics                             │   ← espanso
│     • Mood  [Integer ▾] [unit ▾]  ✕  │
│     [+ Aggiungi metrica]              │
│ ▸ Scales · Timer · Goal · …           │
│ ───────────────────────────────────  │
│ stato: Salvato                        │   ← footer (esistente)
└───────────────────────────────────────┘
```

## 4. Sezioni e applicabilità

L'ordine e la visibilità seguono `IsApplicable(NodeType, HabitType?)` già definito su ogni VM. Riepilogo (i campi esatti li detta ciascun VM):

| Sezione | Applicabile a | Contenuto (dal VM) |
|---|---|---|
| Scheduling | Habit | `ScheduleType` + campi condizionali (WeeklyTarget, EveryXDays, days/monthly JSON) |
| Streak | Habit | streak mode / recovery / pause (+ L3 tolerance) |
| Timer | Habit (Counter) | timer mode / target (+ L3 auto-prompt, rest timer) |
| Metrics | Habit (Counter/Resistance) | lista righe metrica (Name, Type, Unit) + Add/Remove |
| Scales | Habit (Counter/Resistance) | assegnazione scala a una metrica (override locale vs ScaleTemplate) |
| Goal | Habit (+ Group) | target, period type, metrica collegata |
| Notifications | Habit | lista notifiche (tipo, orario) |
| Tags | Habit + Group | chip tag |
| AI sharing | Habit + Group | toggle condivisione AI |
| Reset time | Habit + Group | reset_time effettivo (cascata) |
| ~~DataSource~~ | — | **escluso (placeholder)** |

La griglia esatta di applicabilità è quella che ogni `IsApplicable` già implementa; non la si reinventa.

## 5. Architettura

- **View per sezione**: `src/Recordum.WPF/Views/HabitEditor/Sections/<Name>SectionView.xaml` (UserControl), una per sezione applicabile. Controlli bound alle proprietà del VM; gli `OnXxxChanged` dei VM fanno già l'autosave.
- **DataTemplate impliciti**: un `ResourceDictionary` (es. `Views/HabitEditor/Sections/SectionTemplates.xaml`) con `<DataTemplate DataType="{x:Type vm:SchedulingSectionViewModel}"><v:SchedulingSectionView/></DataTemplate>` per ogni sezione, mergiato in `EditorPane`.
- **Host**: `EditorPane.SectionsHost` → `ItemsControl ItemsSource="{Binding Sections}"`. `ItemTemplate` = `Expander` con `Header={Binding Title}`, `IsExpanded={Binding IsExpanded, Mode=TwoWay}`, `Visibility` da `IsVisible`, e contenuto `ContentControl Content="{Binding}"` (risolto dai DataTemplate impliciti). Chrome Expander dai brush di tema.
- **`HabitEditorViewModel`**: espone `IReadOnlyList<SectionViewModelBase> Sections` (l'ordine di `EnumerateSections`, escluso DataSource). In `LoadAsync`, dopo il caricamento del nodo, imposta per ogni sezione `IsVisible = IsApplicable(node.NodeType, node.HabitType)`.
- **`SectionViewModelBase`** (aggiunte minime): `abstract string Title { get; }` (stringa localizzata, `.resx`), `[ObservableProperty] bool isVisible`, `[ObservableProperty] bool isExpanded`. Ogni sezione concreta fornisce `Title`. Nessun'altra modifica ai VM.
- **Stringhe**: titoli sezione + label controlli in `Strings.resx` (English-first, IT secondario) — niente hardcoded.

## 6. Posizionamento del pannello

In questo giro le sezioni vivono nell'`EditorPane` **attuale** (pannello separato aperto da "Configura"). La trasformazione di "Configura" in **colonna toggle** è coperta dallo spec del modello di navigazione (`2026-05-29-navigation-model-design.md`) ed è indipendente: le View delle sezioni si riusano identiche lì.

## 7. Testing

- VM: già coperti (M3.4). Aggiungere un test su `HabitEditorViewModel.LoadAsync` che imposta `IsVisible` per sezione coerentemente con `IsApplicable` (es. Action → Metrics/Scales/Timer nascoste; Counter → visibili).
- View: verifica visiva con la skill `run` — sezioni collassabili, campi che salvano (stato "Salvato"), visibilità per tipo, toggle Advanced.

## 8. Fuori scope

- `DataSource` (placeholder).
- Riposizionamento di Configura come colonna (nav-model spec).
- Rifinitura grafica della scala State e comportamento "+" su State (rifiniture R separate).
- Template custom (B6b) e i18n del picker (B9).

## 9. Domande aperte

- Quali sezioni necessitano davvero del livello L3 "Advanced" (da derivare dai campi avanzati di ciascun VM in fase di piano).
- Controllo concreto per `ScheduleDaysJson` / `ScheduleMonthlyDaysJson` (day-picker) — definito in fase di piano.
