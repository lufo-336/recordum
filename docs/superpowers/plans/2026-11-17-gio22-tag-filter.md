# GIO-22 — filtro per tag nella Home (brief per Antigravity)
- [x] Fatto 2026-11-17 (agy): file `src/Recordum.Presentation/ViewModels/Home/HomeTagOption.cs`, `src/Recordum.Presentation/ViewModels/HomeViewModel.cs`, `src/Recordum.UI/Themes/Icons.axaml`, `src/Recordum.UI/Views/HomeView.axaml` — verifica: non eseguita (nessuna shell in sessione; build e test demandati al processo esterno)

> Leggi SOLO dentro il repo. Non leggere la memoria di Claude. Non usare la shell. Non fare commit.
> Se qualcosa non è chiaro, lascialo ed elencalo alla fine in "NON FATTO".

## Cosa

I tag si assegnano in Configura › Tag e `HomeViewModel.TagFilter` già filtra le righe, ma la Home Avalonia non ha
un comando per sceglierli. Si aggiungono:

1. una voce **"Tag"** nel menu ⋯ della testa della Home (sotto Attive/Archiviate/Cestino, dopo un separatore), con un
   sottomenu: "Tutti i tag" e poi un tag per riga, a scelta singola (radio);
2. una **pastiglia** nella testa, accanto a "fatte", visibile solo col filtro acceso: icona tag + nome del tag + ×;
   un clic toglie il filtro;
3. la **ricerca** trova un'abitudine anche dal nome di un suo tag.

## Test rossi già scritti (devono diventare verdi, non modificarli)

- `tests/Recordum.Presentation.Tests/ViewModels/HomeTagFilterTests.cs` (6 test)
- `tests/Recordum.UI.Tests/HomeTagFilterViewTests.cs` (2 test)

Le stringhe esistono già (non toccare `.resx` né `Strings.Designer.cs`): `Strings.Home_TagMenu` ("Tag"),
`Strings.Home_TagMenu_All` ("Tutti i tag"), `Strings.Home_TagFilter_Clear` (tooltip/nome accessibile della pastiglia).

## ViewModel — `src/Recordum.Presentation/ViewModels/HomeViewModel.cs`

- Costruttore: nuovo parametro opzionale **in fondo** `ITagService? tags = null` (dopo `HomeRowsPreference? rows`),
  salvato in un campo. Il DI (`AddTransient<HomeViewModel>()`) lo risolve da solo; nessun'altra registrazione.
- Nuovo record in `src/Recordum.Presentation/ViewModels/Home/HomeTagOption.cs`, namespace
  `Recordum.Presentation.ViewModels.Home`: `public sealed record HomeTagOption(Guid? Id, string Name, bool IsSelected);`
  Solo dati: il comando resta sul ViewModel della Home (vedi sotto).
- Proprietà (CommunityToolkit `[ObservableProperty]` o calcolate con notifica):
  - `IReadOnlyList<HomeTagOption> TagOptions` — prima voce `new(null, Strings.Home_TagMenu_All, TagFilter.IsEmpty)`,
    poi i tag di `ITagService.ListAsync()` **nell'ordine del servizio**, tenendo solo quelli che compaiono nei `TagIds`
    di almeno una riga di `_lastFetched` (tutta la lista caricata, senza guardare scheda e altri filtri);
    `IsSelected = TagFilter.Contains(id)`.
  - `bool HasTagOptions` — true se c'è almeno un tag oltre a "Tutti i tag".
  - `string? ActiveTagName` — il nome del tag filtrato, null senza filtro.
  - `bool IsTagFilterOn` — `!TagFilter.IsEmpty`.
- In `LoadAsync`, dopo il fetch delle righe e prima di `Rebuild()`: carica i tag (`_tags?.ListAsync()`), in un
  `try/catch (Exception)` suo: un servizio dei tag che fallisce lascia la Home caricata e senza menu (come
  `ResolveAppDayAsync`, review M-6). Tieni una mappa id → nome per la ricerca. Se `TagFilter` contiene un id che non è
  più fra le opzioni, svuotalo **senza** un Rebuild in più (usa `_suppressRebuild` come `SetDaysBackQuietly`).
- `[RelayCommand] private void FilterByTag(Guid? id)` — `TagFilter = id is { } g ? ImmutableHashSet.Create(g) : Empty`.
- Quando cambia `TagFilter` (c'è già `OnTagFilterChanged`): ricalcola `TagOptions`, `ActiveTagName`, `IsTagFilterOn`
  (notifica), poi il Rebuild com'è ora. Anche `ResetFilters` (cambio scheda) svuota `TagFilter`: le proprietà devono
  seguire.
- Ricerca, in `Rebuild` (dove c'è `r.Name.Contains(needle, …)`): vale anche se uno dei `r.TagIds` ha un nome che
  contiene `needle` (OrdinalIgnoreCase).

## Vista — `src/Recordum.UI/Views/HomeView.axaml`

- Menu ⋯ (`MoreButton`, `MenuFlyout` intorno alla riga 143): dopo la voce Cestino aggiungi
  `<Separator IsVisible="{CompiledBinding HasTagOptions}"/>` e
  `<MenuItem Header="{x:Static res:Strings.Home_TagMenu}" IsVisible="{CompiledBinding HasTagOptions}" ItemsSource="{CompiledBinding TagOptions}">`
  con gli elementi generati come `MenuItem` radio: `Header` = `Name`, `ToggleType="Radio"`, `IsChecked` = `IsSelected`
  (OneWay), `Command` = `FilterByTagCommand` del ViewModel della Home (via `$parent[...]` o `#MoreButton.DataContext`,
  binding compilato con il cast al tipo), `CommandParameter` = `Id`. Usa `MenuItem.ItemContainerTheme` o
  `MenuItem.Styles` con `Style Selector="MenuItem"`; `x:DataType="vmhome:HomeTagOption"` dove serve. Attenzione: il
  prefisso `home` nel file è `Recordum.UI.Views.Home`; per il record aggiungi
  `xmlns:vmhome="using:Recordum.Presentation.ViewModels.Home"` se non c'è già un prefisso per quel namespace.
  Il test conta `ItemCount == 2` sul MenuItem "Tag" dopo `flyout.ShowAt(more)`.
- Pastiglia: nello `StackPanel` della testa (riga ~100), subito **dopo** `DoneToggle`:
  `ToggleButton x:Name="TagChip" Classes="pill" Padding="8,5"`, `IsVisible="{CompiledBinding IsTagFilterOn}"`,
  `IsChecked="{CompiledBinding IsTagFilterOn, Mode=OneWay}"`, `Command="{CompiledBinding FilterByTagCommand}"` senza
  parametro (null = tutti), `ToolTip.Tip` e `AutomationProperties.Name` = `Strings.Home_TagFilter_Clear`. Contenuto
  come `DoneToggle`: `StackPanel Orientation="Horizontal" Spacing="5"` con `Path` icona tag (14×14, stesso tratto di
  `DoneToggle`), `TextBlock Text="{CompiledBinding ActiveTagName}"` (FontSize `FontSize.Label`), `Path` della ×
  (`{StaticResource Icon.X}`, 10×10).
- Icona tag: aggiungi a `src/Recordum.UI/Themes/Icons.axaml`, accanto alle altre, la Lucide "tag":
  `<StreamGeometry x:Key="Icon.Tag">M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z M7 7.5a.5.5 0 1 0 1 0a.5.5 0 1 0-1 0</StreamGeometry>`

## Non fare

- Non toccare `.resx`, `Strings.Designer.cs`, i test elencati sopra, WPF (`src/Recordum.WPF`), Data, Core.
- Non cambiare `GroupFrames.cs`, `RowCardMarginConverter.cs`, `HabitRow.axaml`, `RowCardStyles.axaml`,
  `HeatmapStrip.axaml.cs`, `HabitBuilderView.axaml` (ci sono modifiche in corso di Claude).
- Niente emoji nell'interfaccia; `InitializeComponent()`, mai `AvaloniaXamlLoader.Load`; binding compilati.
- Niente nuovi pacchetti.

## Alla fine

Elenca i file toccati e, sotto "NON FATTO", ciò che hai lasciato o di cui non sei sicuro.
