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

    private static readonly RecordingBuilder Builder = new();
    private static readonly PrivateDbHost Host = new("recordum-ui-picker", s => s.AddSingleton<IHabitBuilderLauncher>(Builder));

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
}
