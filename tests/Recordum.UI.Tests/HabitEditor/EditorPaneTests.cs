using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.HabitEditor;
using Recordum.UI.Tests.HabitScreen;
using Recordum.UI.Views.HabitScreen;
using Xunit;

namespace Recordum.UI.Tests.HabitEditor;

/// <summary>F2b Task 6: la testata di Configura, l'host delle sezioni, lo stato di salvataggio. DB privato.</summary>
public class EditorPaneTests
{
    internal static readonly PrivateDbHost Host = new("recordum-ui-editorpane");

    internal sealed record Opened(MainWindow Window, MainWindowViewModel Vm, Guid Id);

    internal static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 150 && !done(); i++) { await Task.Delay(20); PrivateDbHost.Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    internal static async Task<Guid> CreateAsync(string name, NodeType node = NodeType.Habit, HabitType type = HabitType.Action)
    {
        using var scope = Host.Provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = node, HabitType = node == NodeType.Group ? null : type, Name = name, Color = "#E11D48",
        });
    }

    internal static async Task<HabitNode> ReadAsync(Guid id)
    {
        using var scope = Host.Provider.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IHabitNodeService>().GetAsync(id))!;
    }

    internal static async Task<Opened> OpenAsync(Guid? selectId = null, string name = "Cfg")
    {
        using (var scope = Host.Provider.CreateScope()) await HabitScreenColumnsTests.ResetColumnStateAsync(scope);
        var id = selectId ?? await CreateAsync($"{name}-{Guid.NewGuid():N}");
        using (var scope = Host.Provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAppSettingService>()
                .SetAsync("ui.workspace", new Recordum.Presentation.ViewModels.Workspace.WorkspaceState().ToJson());
        var (window, vm) = await ShellFixture.OpenMainWindowAsync(Host.Provider.GetRequiredService<MainWindowViewModel>());
        vm.Home.SelectedRow = vm.Home.Rows.Single(r => r.HabitNodeId == id);
        await UntilAsync(() => window.GetVisualDescendants().OfType<HabitScreenView>().Any());
        return new Opened(window, vm, id);
    }

    internal static async Task<EditorPane> OpenConfiguraAsync(Opened o)
    {
        o.Vm.Workspace.ActiveSubjectTab!.Screen.IsConfiguraOpen = true;
        await UntilAsync(() => o.Window.GetVisualDescendants().OfType<EditorPane>().Any()
            && o.Vm.Workspace.ActiveSubjectTab!.Screen.Editor.Node is not null);
        PrivateDbHost.Pump();
        return o.Window.GetVisualDescendants().OfType<EditorPane>().Single();
    }

    internal static T Named<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    internal static void TypeName(Opened o, EditorPane pane, string text)
    {
        var box = Named<TextBox>(pane, "NameBox");
        box.Focus();
        PrivateDbHost.Pump();
        box.SelectAll();
        o.Window.KeyTextInput(text);
        PrivateDbHost.Pump();
    }

    internal static void Blur(Opened o, EditorPane pane)
    {
        Named<TextBox>(pane, "DescriptionBox").Focus();
        PrivateDbHost.Pump();
    }

    [AvaloniaFact]
    public async Task Name_colour_and_icon_save_and_the_home_row_and_screen_header_follow()
    {
        var o = await OpenAsync();
        try
        {
            var pane = await OpenConfiguraAsync(o);
            TypeName(o, pane, "Rinominata");
            Blur(o, pane);
            Named<ColorSwatchPicker>(pane, "ColorPicker").Color = "#112233";
            Named<IconPicker>(pane, "IconPickerControl").Icon = "Icon.Heart";

            await UntilAsync(() => ReadAsync(o.Id).GetAwaiter().GetResult() is { Name: "Rinominata", Color: "#112233", Icon: "Icon.Heart" });
            var row = () => o.Vm.Home.Rows.Single(r => r.HabitNodeId == o.Id);
            await UntilAsync(() => row().Name == "Rinominata" && row().ColorHex == "#112233" && row().Icon == "Icon.Heart");
            var header = o.Window.GetVisualDescendants().OfType<HabitScreenView>().Single().GetVisualDescendants()
                .OfType<TextBlock>().Single(t => t.Name == "HabitName");
            await UntilAsync(() => header.Text == "Rinominata");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task The_save_status_stays_visible_outside_the_scroll_even_with_tall_open_sections()
    {
        var o = await OpenAsync();
        try
        {
            var pane = await OpenConfiguraAsync(o);
            var scroll = Named<ScrollViewer>(pane, "SectionsScroll");
            Named<StackPanel>(pane, "SectionsStack").Children.Add(new Border { Height = 3000 });   // sezioni aperte, tutte alte
            PrivateDbHost.Pump();
            scroll.ScrollToEnd();
            PrivateDbHost.Pump();
            scroll.Offset.Y.Should().BeGreaterThan(0, "le sezioni sono scorse in fondo");

            TypeName(o, pane, "Salvami");
            Blur(o, pane);
            var status = Named<TextBlock>(pane, "StatusText");
            await UntilAsync(() => status.Text == Strings.Editor_Status_Saved);

            status.GetVisualAncestors().Should().NotContain(scroll, "A12: lo stato e' fuori dallo scorrimento");
            var bottom = status.TranslatePoint(new Avalonia.Point(0, status.Bounds.Height), pane)!.Value.Y;
            bottom.Should().BeLessThanOrEqualTo(pane.Bounds.Height + 0.5).And.BeGreaterThan(pane.Bounds.Height - 40, "in fondo alla colonna");
            status.IsEffectivelyVisible.Should().BeTrue();
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_group_created_from_home_opens_with_configura_and_is_renamed_there()
    {
        var o = await OpenAsync();
        try
        {
            await o.Vm.Home.CreateGroupCommand.ExecuteAsync(null);
            await UntilAsync(() => o.Vm.Workspace.ActiveSubjectTab is { Screen.IsConfiguraOpen: true } t
                && t.Screen.Editor.Node?.NodeType == NodeType.Group
                && o.Window.GetVisualDescendants().OfType<EditorPane>().Any());
            var pane = o.Window.GetVisualDescendants().OfType<EditorPane>().Single();
            var id = o.Vm.Workspace.ActiveSubjectTab!.SubjectId;
            Named<TextBox>(pane, "NameBox").Text.Should().Be(Strings.Editor_Default_NewGroup);
            Named<TextBlock>(pane, "TypeLabel").Text.Should().Be(Strings.Editor_NodeType_Group);

            TypeName(o, pane, "Gruppo mio");
            Blur(o, pane);
            await UntilAsync(() => ReadAsync(id).GetAwaiter().GetResult().Name == "Gruppo mio");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Auto_opening_configura_for_a_new_group_does_not_seed_the_columns_of_new_habits()
    {
        var o = await OpenAsync();
        try
        {
            await o.Vm.Home.CreateGroupCommand.ExecuteAsync(null);
            await UntilAsync(() => o.Vm.Workspace.ActiveSubjectTab is { Screen.IsConfiguraOpen: true }
                && o.Window.GetVisualDescendants().OfType<EditorPane>().Any());

            var fresh = await CreateAsync($"Nuova-{Guid.NewGuid():N}");
            await o.Vm.Home.LoadCommand.ExecuteAsync(null);
            o.Vm.Home.SelectedRow = o.Vm.Home.Rows.Single(r => r.HabitNodeId == fresh);
            await UntilAsync(() => o.Vm.Workspace.ActiveSubjectTab is { } t && t.SubjectId == fresh && t.Screen.Node?.Id == fresh);
            var screen = o.Vm.Workspace.ActiveSubjectTab!.Screen;
            await UntilAsync(() => screen.IsCronologiaOpen || screen.IsConfiguraOpen);
            screen.IsConfiguraOpen.Should().BeFalse("un'abitudine nuova parte dal default del suo tipo");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_name_typed_and_the_tab_closed_at_once_is_still_saved()
    {
        var o = await OpenAsync();
        try
        {
            var pane = await OpenConfiguraAsync(o);
            TypeName(o, pane, "Chiusa subito");   // il fuoco resta nella casella: niente LostFocus
            var tab = o.Vm.Workspace.ActiveSubjectTab!;
            o.Vm.Workspace.CloseTab(tab);
            PrivateDbHost.Pump();
            await UntilAsync(() => ReadAsync(o.Id).GetAwaiter().GetResult().Name == "Chiusa subito");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_name_typed_and_another_habit_selected_at_once_is_still_saved()
    {
        var other = await CreateAsync($"Altra-{Guid.NewGuid():N}");
        var o = await OpenAsync();
        try
        {
            var pane = await OpenConfiguraAsync(o);
            TypeName(o, pane, "Prima di cambiare");
            o.Vm.Home.SelectedRow = o.Vm.Home.Rows.Single(r => r.HabitNodeId == other);
            PrivateDbHost.Pump();
            await UntilAsync(() => ReadAsync(o.Id).GetAwaiter().GetResult().Name == "Prima di cambiare");
            (await ReadAsync(other)).Name.Should().StartWith("Altra-", "l'altra abitudine non prende il nome");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task The_notifications_section_is_never_built()
    {
        var o = await OpenAsync();
        try
        {
            var pane = await OpenConfiguraAsync(o);
            var editor = o.Vm.Workspace.ActiveSubjectTab!.Screen.Editor;
            editor.Sections.Should().Contain(editor.Notifications, "la sezione esiste nel ViewModel");
            editor.Sections.Should().NotContain(s => s is DataSourceSectionViewModel, "R2: DataSource non e' in Sections e non ha view");
            new SectionTemplate().Build(editor.Notifications).Should().BeNull();
            pane.GetVisualDescendants().OfType<Expander>()
                .Should().NotContain(e => Equals(e.Header, editor.Notifications.Title));
        }
        finally { o.Window.Close(); }
    }
}
