using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
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
public class HabitTemplatePickerTests
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

    private static Button OptionButton(Window picker, Func<PickerOption, bool> match) =>
        picker.GetVisualDescendants().OfType<Button>()
            .First(b => b.Name == "OptionButton" && b.DataContext is PickerOption o && match(o));

    private static Button FirstOfficial(Window picker)
    {
        var vm = (HabitTemplatePickerViewModel)picker.DataContext!;
        vm.OfficialTemplates.Should().NotBeEmpty("i template ufficiali sono seminati");
        var first = vm.OfficialTemplates[0];
        return OptionButton(picker, o => o == first);
    }

    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
            var option = (PickerOption)FirstOfficial(picker).DataContext!;

            Click(FirstOfficial(picker));
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
            Click(OptionButton(picker!, o => o.Name == Strings.HabitTemplatePicker_FromScratch));

            var id = await task.WaitAsync(TimeSpan.FromSeconds(5));
            Builder.Parents.Should().Equal(group);
            id.Should().Be(Builder.Result);
        }
        finally { picker?.Close(); }
    }

    // ---- 4.3 ----------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task A_double_click_on_a_template_creates_one_habit()
    {
        var (window, vm, launcher) = await OpenAsync();
        try
        {
            var before = vm.Home.Rows.Select(r => r.HabitNodeId).ToHashSet();
            var picker = await PickerOpenedByAsync(launcher, () => vm.Home.OpenNewHabitCommand.ExecuteAsync(null));
            var button = FirstOfficial(picker);

            Click(button);
            Click(button);
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

            Click(FirstOfficial(picker));
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
            Button Delete() => picker.GetVisualDescendants().OfType<Button>()
                .First(b => b.Content as string == Strings.HabitTemplatePicker_Delete
                    && b.DataContext is PickerOption { TemplateId: var t } && t == id);

            Confirm.Yes = false;
            var calls = Confirm.Calls;
            Click(Delete());
            await UntilAsync(() => Confirm.Calls == calls + 1);
            PrivateDbHost.Pump();
            (await ExistsAsync(id)).Should().BeTrue("annullare lascia il template");

            Confirm.Yes = true;
            Click(Delete());
            for (var i = 0; i < 150 && await ExistsAsync(id); i++) { await Task.Delay(20); PrivateDbHost.Pump(); }
            (await ExistsAsync(id)).Should().BeFalse("confermare lo elimina");
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
}
