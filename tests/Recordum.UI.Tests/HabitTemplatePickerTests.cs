using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Presentation.Services;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.Workspace;
using Recordum.UI.Services;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.HabitScreen;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>Il selettore dei template e le porte d'ingresso della creazione (F2b task 4). DB PRIVATO.</summary>
public class HabitTemplatePickerTests(ITestOutputHelper output)
{
    private sealed class RecordingBuilder : IHabitBuilderLauncher
    {
        public List<Guid?> Parents { get; } = [];
        public Guid Result { get; } = Guid.NewGuid();
        public Task<Guid?> LaunchAsync(Guid? parentId = null, CancellationToken ct = default)
        {
            Parents.Add(parentId);
            return Task.FromResult<Guid?>(Result);
        }

        public List<(Guid TemplateId, Guid? ParentId)> FromTemplate { get; } = [];
        public Task<Guid?> LaunchFromTemplateAsync(Guid templateId, Guid? parentId = null, CancellationToken ct = default)
        {
            FromTemplate.Add((templateId, parentId));
            return Task.FromResult<Guid?>(Result);
        }
    }

    private sealed class Answer : IConfirmationDialog
    {
        public bool Yes { get; set; } = true;
        public int Calls { get; private set; }
        public Task<bool> ConfirmAsync(string title, string message) { Calls++; return Task.FromResult(Yes); }
    }

    /// <summary>Il servizio dei template di ogni scope, registrato: prova che ogni apertura ha il suo e che lo
    /// smaltisce alla chiusura.</summary>
    private sealed class SpyTemplates(IHabitTemplateService inner) : IHabitTemplateService, IDisposable
    {
        public static List<SpyTemplates> All { get; } = [];
        public bool Disposed { get; private set; }
        public static SpyTemplates Create(IHabitTemplateService inner) { var s = new SpyTemplates(inner); All.Add(s); return s; }
        public void Dispose() => Disposed = true;
        public ValueTask<Guid> CreateAsync(HabitTemplate draft, CancellationToken ct = default) => inner.CreateAsync(draft, ct);
        public ValueTask UpdateAsync(Guid id, Action<HabitTemplate> mutate, CancellationToken ct = default) => inner.UpdateAsync(id, mutate, ct);
        public ValueTask DeleteAsync(Guid id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
        public ValueTask<HabitTemplate?> GetAsync(Guid id, CancellationToken ct = default) => inner.GetAsync(id, ct);
        public ValueTask<IReadOnlyList<HabitTemplate>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
        public ValueTask<Guid> InstantiateAsync(Guid templateId, Guid? parentId, CancellationToken ct = default) => inner.InstantiateAsync(templateId, parentId, ct);
    }

    private static readonly RecordingBuilder Builder = new();
    private static readonly Answer Confirm = new();
    private static readonly PrivateDbHost Host = new("recordum-ui-picker", s =>
    {
        s.AddSingleton<IHabitBuilderLauncher>(Builder);
        s.AddSingleton<IConfirmationDialog>(Confirm);
        s.AddScoped<IHabitTemplateService>(sp =>
            SpyTemplates.Create(ActivatorUtilities.CreateInstance<HabitTemplateService>(sp)));
    });

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 150 && !done(); i++) { await Task.Delay(20); PrivateDbHost.Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static async Task<(MainWindow Window, MainWindowViewModel Vm, AvaloniaHabitTemplatePickerLauncher Launcher)> OpenAsync()
    {
        var sp = Host.Provider;
        using (var scope = sp.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAppSettingService>()
                .SetAsync("ui.workspace", new WorkspaceState().ToJson());
        var (window, vm) = await ShellFixture.OpenMainWindowAsync(sp.GetRequiredService<MainWindowViewModel>());
        return (window, vm, (AvaloniaHabitTemplatePickerLauncher)sp.GetRequiredService<IHabitTemplatePickerLauncher>());
    }

    private static async Task<Window> PickerOpenedByAsync(AvaloniaHabitTemplatePickerLauncher launcher, Func<Task> open)
    {
        Window? picker = null;
        launcher.WindowCreatedForTests += w => picker = w;
        _ = open();
        await UntilAsync(() => picker is { IsVisible: true });
        PrivateDbHost.Pump();
        return picker!;
    }

    private static HabitTemplatePickerViewModel Vm(Window picker) => (HabitTemplatePickerViewModel)picker.DataContext!;

    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    /// <summary>La scheda (il contenitore della lista) del modello <paramref name="id"/>, o la prima ufficiale.</summary>
    private static ListBoxItem Card(Window picker, Guid? id = null)
    {
        var vm = Vm(picker);
        vm.VisibleOfficial.Should().NotBeEmpty("i template ufficiali sono seminati");
        var option = vm.VisibleOfficial.Concat(vm.VisibleCustom).First(o => id is null || o.TemplateId == id);
        return picker.GetVisualDescendants().OfType<ListBoxItem>().Single(i => Equals(i.DataContext, option));
    }

    /// <summary>Un clic vero (giu' e su del mouse) al centro del controllo: passa dal comando, come l'utente.</summary>
    private static void Click(Window w, Control c)
    {
        c.BringIntoView();
        PrivateDbHost.Pump();
        var p = c.TranslatePoint(new Avalonia.Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        PrivateDbHost.Pump();
    }

    private static void Enter(Control target) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = target });

    /// <summary>Sceglie la scheda e preme "Usa questo modello".</summary>
    private static void Use(Window picker, Guid? id = null)
    {
        Click(picker, Card(picker, id));
        Click(picker, Find<Button>(picker, "UseButton"));
    }

    private static async Task<Guid> CreateGroupAsync()
    {
        using var scope = Host.Provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Group, Name = $"G-{Guid.NewGuid():N}", Color = "#3B82F6",
        });
    }

    // ---- 4.1 ----------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task Picking_a_template_creates_the_habit_selects_it_and_opens_its_tab()
    {
        var (window, vm, launcher) = await OpenAsync();
        try
        {
            var before = vm.Home.Rows.Select(r => r.HabitNodeId).ToHashSet();
            var picker = await PickerOpenedByAsync(launcher, () => vm.Home.OpenNewHabitCommand.ExecuteAsync(null));
            var option = (PickerOption)Card(picker).DataContext!;

            Use(picker);
            await UntilAsync(() => vm.Home.SelectedRow is not null && !before.Contains(vm.Home.SelectedRow.HabitNodeId));

            var created = vm.Home.SelectedRow!;
            created.Name.Should().Be(option.Name);
            vm.Home.Rows.Select(r => r.HabitNodeId).Except(before).Should().ContainSingle();
            await UntilAsync(() => window.GetVisualDescendants().OfType<HabitScreenView>().Any());
            picker.IsVisible.Should().BeFalse("il selettore si chiude da solo");
        }
        finally { window.Close(); }
    }

    // ---- 4.2 ----------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task Free_composition_opens_the_builder_with_the_pickers_parent_and_returns_its_result()
    {
        var group = await CreateGroupAsync();
        var launcher = (AvaloniaHabitTemplatePickerLauncher)Host.Provider.GetRequiredService<IHabitTemplatePickerLauncher>();
        Builder.Parents.Clear();
        Window? picker = null;
        launcher.WindowCreatedForTests += w => picker = w;
        var task = launcher.PickAsync(group);
        try
        {
            await UntilAsync(() => picker is { IsVisible: true });
            PrivateDbHost.Pump();
            Click(picker!, Find<Button>(picker!, "ScratchTile"));

            var id = await task.WaitAsync(TimeSpan.FromSeconds(5));
            Builder.Parents.Should().Equal(group);
            id.Should().Be(Builder.Result);
        }
        finally { picker?.Close(); }
    }

    // ---- 4.3 ----------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task Enter_on_a_card_uses_it_and_a_repeated_Enter_creates_one_habit()
    {
        var (window, vm, launcher) = await OpenAsync();
        try
        {
            var before = vm.Home.Rows.Select(r => r.HabitNodeId).ToHashSet();
            var picker = await PickerOpenedByAsync(launcher, () => vm.Home.OpenNewHabitCommand.ExecuteAsync(null));
            var card = Card(picker);
            Click(picker, card);
            card.IsFocused.Should().BeTrue("la scheda scelta ha il fuoco: Invio le arriva");

            // Invio ripetuto prima che il primo abbia finito (nessun giro del dispatcher in mezzo: KeyPress lo farebbe, e la
            // finestra sarebbe gia' chiusa): ExecuteAsync non consulta CanExecute, la guardia e' nel ViewModel.
            Enter(card);
            Enter(card);
            await UntilAsync(() => vm.Home.SelectedRow is not null && !before.Contains(vm.Home.SelectedRow.HabitNodeId));
            await Task.Delay(200);
            await vm.Home.LoadCommand.ExecuteAsync(null);   // rilegge dal DB: un secondo nodo, se c'e', compare qui
            PrivateDbHost.Pump();

            vm.Home.Rows.Select(r => r.HabitNodeId).Except(before).Should().ContainSingle();
        }
        finally { window.Close(); }
    }

    // ---- 4.4 ----------------------------------------------------------------------------------------------

    private sealed class RecordingPicker : IHabitTemplatePickerLauncher
    {
        public int Calls;
        public Task<Guid?> PickAsync(Guid? parentId = null, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult<Guid?>(null);
        }
    }

    [AvaloniaFact]
    public void The_new_menu_habit_item_and_the_empty_state_button_open_the_picker()
    {
        var picker = new RecordingPicker();
        var query = new FakeHabitTreeQueryService();
        var vm = new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService(), launcher: picker);
        var view = new HomeView { DataContext = vm };
        var w = new Window { Width = 900, Height = 600, Content = view };
        try
        {
            w.Show();
            vm.LoadCommand.Execute(null);
            PrivateDbHost.Pump();
            vm.State.Should().Be(HomeState.Empty);

            var empty = view.GetVisualDescendants().OfType<EmptyState>().Single();
            var cta = empty.GetVisualDescendants().OfType<Button>().Single();
            cta.IsVisible.Should().BeTrue();
            cta.Command!.Execute(null);

            var button = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NewMenuButton");
            button.Flyout!.ShowAt(button);   // il comando si lega quando il menu entra nell'albero
            PrivateDbHost.Pump();
            var item = ((MenuFlyout)button.Flyout!).Items.OfType<MenuItem>().Single(i => (string?)i.Header == Strings.Home_NewMenu_Habit);
            item.IsVisible.Should().BeTrue();
            item.Command!.Execute(null);

            picker.Calls.Should().Be(2);
        }
        finally { w.Close(); }
    }

    // ---- 4.5 ----------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task With_a_group_selected_the_new_habit_is_born_inside_it()
    {
        var group = await CreateGroupAsync();
        var (window, vm, launcher) = await OpenAsync();
        try
        {
            vm.Home.SelectedRow = vm.Home.Rows.Single(r => r.HabitNodeId == group);
            var before = vm.Home.Rows.Select(r => r.HabitNodeId).ToHashSet();
            var picker = await PickerOpenedByAsync(launcher, () => vm.Home.OpenNewHabitCommand.ExecuteAsync(null));

            Use(picker);
            await UntilAsync(() => vm.Home.SelectedRow is not null && !before.Contains(vm.Home.SelectedRow.HabitNodeId));

            vm.Home.SelectedRow!.ParentId.Should().Be(group);
        }
        finally { window.Close(); }
    }

    // ---- fix round 1 --------------------------------------------------------------------------------------

    private static async Task<Guid> CreateCustomTemplateAsync(string name)
    {
        using var scope = Host.Provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHabitTemplateService>()
            .CreateAsync(new HabitTemplate { Name = name, IsOfficial = false });
    }

    private static async Task<bool> ExistsAsync(Guid id)
    {
        using var scope = Host.Provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHabitTemplateService>().GetAsync(id) is not null;
    }

    [AvaloniaFact]
    public async Task Deleting_a_custom_template_asks_first_cancel_keeps_it_confirm_removes_it()
    {
        var id = await CreateCustomTemplateAsync($"doomed-{Guid.NewGuid():N}");
        var launcher = (AvaloniaHabitTemplatePickerLauncher)Host.Provider.GetRequiredService<IHabitTemplatePickerLauncher>();
        var picker = await PickerOpenedByAsync(launcher, () => launcher.PickAsync());
        try
        {
            MenuItem Delete()
            {
                var menu = Card(picker, id).GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CardMenuButton");
                menu.Flyout!.ShowAt(menu);   // il comando si lega quando il menu entra nell'albero
                PrivateDbHost.Pump();
                return ((MenuFlyout)menu.Flyout!).Items.OfType<MenuItem>().Single(i => i.Name == "DeleteItem");
            }

            Confirm.Yes = false;
            var calls = Confirm.Calls;
            Delete().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await UntilAsync(() => Confirm.Calls == calls + 1);
            PrivateDbHost.Pump();
            (await ExistsAsync(id)).Should().BeTrue("annullare lascia il template");

            Confirm.Yes = true;
            Delete().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            for (var i = 0; i < 150 && await ExistsAsync(id); i++) { await Task.Delay(20); PrivateDbHost.Pump(); }
            (await ExistsAsync(id)).Should().BeFalse("confermare lo elimina");
        }
        finally { picker.Close(); }
    }

    // I tasti premuti sul ⋮ di una scheda passano in tunnel per la lista (il bottone sta dentro la scheda): Invio deve aprire il
    // menu, non usare la scheda scelta (un'altra: creerebbe un Sonno), e le frecce non spostano la scelta. Le voci del menu,
    // invece, stanno in un popup il cui Popup non ha genitore visuale: il loro percorso finisce li' e non tocca la lista
    // (misurato su Avalonia 12.1.2 in headless, che usa OverlayPopupHost; il PopupRoot del desktop qui non si crea). Per loro
    // il test prova solo che Invio su Elimina arriva al menu e chiede la conferma; la guardia della lista le copre comunque.
    [AvaloniaFact]
    public async Task Keys_on_the_card_menu_stay_with_the_menu()
    {
        var id = await CreateCustomTemplateAsync($"menu-{Guid.NewGuid():N}");
        var picker = await OpenPickerAsync();
        try
        {
            var vm = Vm(picker);
            Click(picker, Card(picker, SleepId));
            var chosen = vm.Selected;
            var menu = Card(picker, id).GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CardMenuButton");
            menu.Focus();

            menu.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down, Source = menu });
            PrivateDbHost.Pump();
            vm.Selected.Should().Be(chosen, "una freccia sul ⋮ non sposta la scheda scelta");

            Enter(menu);
            PrivateDbHost.Pump();
            vm.CreatedHabitNodeId.Should().BeNull("Invio sul ⋮ non usa la scheda scelta");
            menu.Flyout!.IsOpen.Should().BeTrue("Invio sul ⋮ apre il suo menu");

            var delete = ((MenuFlyout)menu.Flyout!).Items.OfType<MenuItem>().Single(i => i.Name == "DeleteItem");
            delete.Focus();
            Confirm.Yes = false;
            var calls = Confirm.Calls;
            Enter(delete);
            await UntilAsync(() => Confirm.Calls == calls + 1);
            await Task.Delay(100);
            PrivateDbHost.Pump();

            vm.CreatedHabitNodeId.Should().BeNull("Invio su Elimina non usa la scheda scelta");
            vm.CustomizeTemplateRequested.Should().BeNull();
            picker.IsVisible.Should().BeTrue();
            (await ExistsAsync(id)).Should().BeTrue("annullato: il modello resta");
        }
        finally { picker.Close(); }
    }

    [AvaloniaFact]
    public async Task Each_picker_open_has_its_own_scope_that_ends_with_the_window()
    {
        var launcher = (AvaloniaHabitTemplatePickerLauncher)Host.Provider.GetRequiredService<IHabitTemplatePickerLauncher>();
        var known = SpyTemplates.All.Count;

        var first = await PickerOpenedByAsync(launcher, () => launcher.PickAsync());
        first.Close();
        var added = $"added-{Guid.NewGuid():N}";
        await CreateCustomTemplateAsync(added);
        var second = await PickerOpenedByAsync(launcher, () => launcher.PickAsync());
        try
        {
            ((HabitTemplatePickerViewModel)second.DataContext!).CustomOptions.Select(o => o.Name).Should().Contain(added);
            var spies = SpyTemplates.All.Skip(known).ToList();
            spies.Should().HaveCount(3, "primo picker, scope del test, secondo picker: mai un servizio di radice condiviso");
            spies[0].Disposed.Should().BeTrue("lo scope finisce con la finestra");
            spies[2].Disposed.Should().BeFalse("la finestra e' ancora aperta");
        }
        finally { second.Close(); }
    }

    // ---- 3a, task 6: il selettore di spec §7.2 ------------------------------------------------------------

    private static readonly Guid SleepId = Guid.Parse("00000003-0000-0000-0000-000000000005");

    private static async Task<Window> OpenPickerAsync(Guid? parent = null, double width = 980, string variant = "Light")
    {
        var launcher = (AvaloniaHabitTemplatePickerLauncher)Host.Provider.GetRequiredService<IHabitTemplatePickerLauncher>();
        Window? picker = null;
        launcher.WindowCreatedForTests += w =>
        {
            picker = w;
            w.Width = width;
            w.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        };
        _ = launcher.PickAsync(parent);
        await UntilAsync(() => picker is { IsVisible: true });
        PrivateDbHost.Pump();
        return picker!;
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static List<ListBoxItem> Cards(Window picker, string list) =>
        Find<ListBox>(picker, list).GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.IsEffectivelyVisible).ToList();

    [AvaloniaFact]
    public async Task Tiles_cards_and_detail_are_laid_out()
    {
        var picker = await OpenPickerAsync();
        try
        {
            Click(picker, Card(picker, SleepId));
            await UntilAsync(() => Vm(picker).Detail is not null);
            Render();

            Find<Button>(picker, "ScratchTile").IsEffectivelyVisible.Should().BeTrue();
            Find<Button>(picker, "ScratchTile").Bounds.Height.Should().Be(Find<Button>(picker, "GroupTile").Bounds.Height,
                "le due tessere hanno la stessa altezza");
            var groupTile = Find<Button>(picker, "GroupTile");
            groupTile.IsEffectivelyVisible.Should().BeTrue();
            // La descrizione va a capo dentro la tessera (in uno StackPanel orizzontale usciva tagliata a destra).
            var description = Find<TextBlock>(picker, "GroupTileDescription");
            description.TranslatePoint(new Point(description.Bounds.Width, 0), groupTile)!.Value.X
                .Should().BeLessThanOrEqualTo(groupTile.Bounds.Width, "la descrizione non esce dalla tessera");
            var cards = Cards(picker, "OfficialList");
            cards.Should().HaveCount(8, "gli otto modelli ufficiali");
            cards.Should().OnlyContain(c => c.Bounds.Height > 40 && c.Bounds.Width > 150, "ogni scheda e' disegnata");
            cards[0].Bounds.Y.Should().Be(cards[1].Bounds.Y, "due schede per riga");

            var detail = Find<Border>(picker, "DetailHost");
            var list = Find<ListBox>(picker, "OfficialList");
            detail.TranslatePoint(default, picker)!.Value.X.Should().BeGreaterThan(
                list.TranslatePoint(new Avalonia.Point(list.Bounds.Width, 0), picker)!.Value.X - 1, "il dettaglio sta a destra");
            Find<StackPanel>(picker, "DetailPanel").IsEffectivelyVisible.Should().BeTrue();
            Find<TextBlock>(picker, "DetailName").Text.Should().Be(Card(picker, SleepId).DataContext is PickerOption o ? o.Name : null);
            Find<ItemsControl>(picker, "DetailContents").ItemCount.Should().BeGreaterThanOrEqualTo(2);
            Find<Recordum.UI.Controls.RowPreview>(picker, "DetailRow").Bounds.Height.Should().BeGreaterThan(20, "la riga di Home e' disegnata");

            picker.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("primary") && b.IsEffectivelyVisible)
                .Should().Be(1, "un solo inchiostro per finestra: Usa questo modello");
        }
        finally { picker.Close(); }
    }

    [AvaloniaFact]
    public async Task Use_is_disabled_until_a_card_is_selected()
    {
        var picker = await OpenPickerAsync();
        try
        {
            var use = Find<Button>(picker, "UseButton");
            var customize = Find<Button>(picker, "CustomizeButton");
            use.IsEffectivelyEnabled.Should().BeFalse();
            customize.IsEffectivelyEnabled.Should().BeFalse();
            Find<TextBlock>(picker, "DetailEmpty").IsEffectivelyVisible.Should().BeTrue("senza scheda il dettaglio dice cosa fare");

            Click(picker, Card(picker));

            use.IsEffectivelyEnabled.Should().BeTrue();
            customize.IsEffectivelyEnabled.Should().BeTrue();
        }
        finally { picker.Close(); }
    }

    [AvaloniaFact]
    public async Task Arrows_move_between_cards()
    {
        var picker = await OpenPickerAsync();
        try
        {
            var vm = Vm(picker);
            Click(picker, Card(picker, vm.VisibleOfficial[0].TemplateId));

            picker.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            PrivateDbHost.Pump();

            vm.Selected.Should().NotBe(vm.VisibleOfficial[0], "la freccia sposta la scelta sulla scheda successiva");
            vm.Selected.Should().NotBeNull();
        }
        finally { picker.Close(); }
    }

    [AvaloniaFact]
    public async Task Customize_opens_the_creator_filled_with_the_template_and_the_pickers_parent()
    {
        var group = await CreateGroupAsync();
        var launcher = (AvaloniaHabitTemplatePickerLauncher)Host.Provider.GetRequiredService<IHabitTemplatePickerLauncher>();
        Builder.FromTemplate.Clear();
        Window? picker = null;
        launcher.WindowCreatedForTests += w => picker = w;
        var task = launcher.PickAsync(group);
        try
        {
            await UntilAsync(() => picker is { IsVisible: true });
            PrivateDbHost.Pump();
            Click(picker!, Card(picker!, SleepId));
            Click(picker!, Find<Button>(picker!, "CustomizeButton"));

            var id = await task.WaitAsync(TimeSpan.FromSeconds(5));
            Builder.FromTemplate.Should().Equal((SleepId, (Guid?)group));
            id.Should().Be(Builder.Result);
            picker!.IsVisible.Should().BeFalse("il selettore si chiude prima del creatore");
        }
        finally { picker?.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Templates_png_with_a_card_selected(string variant)
    {
        using var culture = TestCulture.Pin();
        var mine = await CreateCustomTemplateAsync("Mattina");
        var picker = await OpenPickerAsync(variant: variant);
        try
        {
            Card(picker, mine).GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CardMenuButton")
                .IsEffectivelyVisible.Should().BeTrue("i tuoi modelli hanno il loro menu");
            var dots = Card(picker, mine).GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p => p.Name == "CardMenuIcon");
            Render();
            // Misurato: con Stretch="Uniform" su un Path di 14 px la geometria disegnata era alta 0 (un punto solo).
            dots.RenderedGeometry!.Bounds.TransformToAABB(dots.TransformToVisual(picker)!.Value).Height
                .Should().BeGreaterThan(6, "i tre punti stanno in colonna, non schiacciati in uno");
            Click(picker, Card(picker, Guid.Parse("00000003-0000-0000-0000-000000000008")));
            await UntilAsync(() => Vm(picker).Detail is not null);
            Render();
            output.WriteLine(ScreenshotProbeTests.Capture(picker, $"templates-{variant.ToLowerInvariant()}"));

            Find<TextBlock>(picker, "DetailName").Text.Should().Be("Diario del cibo", "il modello ufficiale si legge in italiano");
            Find<TextBlock>(picker, "DetailType").Text.Should().Be("journal · più volte al giorno");
            Card(picker, Guid.Parse("00000003-0000-0000-0000-000000000008")).IsSelected.Should().BeTrue();
        }
        finally
        {
            picker.Close();
            using var scope = Host.Provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IHabitTemplateService>().DeleteAsync(mine);
        }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Templates_search_empty_png(string variant)
    {
        using var culture = TestCulture.Pin();
        var picker = await OpenPickerAsync(variant: variant);
        try
        {
            Find<TextBox>(picker, "SearchBox").Text = "zzzz";
            Render();
            output.WriteLine(ScreenshotProbeTests.Capture(picker, $"templates-search-empty-{variant.ToLowerInvariant()}"));

            Find<TextBlock>(picker, "NoResultsText").IsEffectivelyVisible.Should().BeTrue();
            Cards(picker, "OfficialList").Should().BeEmpty();
            Find<StackPanel>(picker, "YoursSection").IsVisible.Should().BeFalse("nessun tuo modello corrisponde");
        }
        finally { picker.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Templates_narrow_png_puts_the_detail_under_the_cards(string variant)
    {
        using var culture = TestCulture.Pin();
        var picker = await OpenPickerAsync(width: 720, variant: variant);
        try
        {
            Click(picker, Card(picker, SleepId));
            await UntilAsync(() => Vm(picker).Detail is not null);
            Render();
            output.WriteLine(ScreenshotProbeTests.Capture(picker, $"templates-narrow-{variant.ToLowerInvariant()}"));

            var detail = Find<Border>(picker, "DetailHost");
            detail.Parent.Should().Be(Find<StackPanel>(picker, "MainStack"), "stretto: il dettaglio scende sotto le schede");
            Find<Grid>(picker, "LayoutGrid").ColumnDefinitions[1].Width.Value.Should().Be(0);

            picker.Width = 980;
            Render();
            detail.Parent.Should().Be(Find<Grid>(picker, "LayoutGrid"), "largo di nuovo: torna a destra");
        }
        finally { picker.Close(); }
    }
}
