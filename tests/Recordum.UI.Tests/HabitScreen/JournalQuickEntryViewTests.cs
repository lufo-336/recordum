using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Journal;
using Recordum.UI.Views.HabitScreen.Today;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>F2d-1, Task 7: la nota veloce del diario su Avalonia (composer con menu #, riga Tag, foto, "Oggi" con le voci).</summary>
public sealed class JournalQuickEntryViewTests(ITestOutputHelper output) : IDisposable
{
    private readonly List<Window> _windows = [];
    public void Dispose() { foreach (var w in _windows) w.Close(); }

    private static readonly ZonedClock Clock = new();
    private static readonly FakeOpenFileDialog Dialog = new();
    private static readonly PrivateDbHost Host = new("recordum-ui-journal", s =>
    {
        s.AddSingleton<Recordum.Core.Abstractions.IClock>(Clock);
        s.AddSingleton<IOpenFileDialog>(Dialog);
    });

    private static T Svc<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    private static async Task<Guid> CreateAsync(HabitType type, string name)
    {
        using var scope = Host.Provider.CreateScope();
        return await Svc<IHabitNodeService>(scope).CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = name, Color = "#7C5CBF",
        });
    }

    // Il DB e' condiviso fra i test della classe: un tag con quel nome puo' esistere gia' (nome unico, NOCASE).
    private static async Task EnsureTagAsync(string name)
    {
        using var scope = Host.Provider.CreateScope();
        var tags = Svc<ITagService>(scope);
        if (!(await tags.ListAsync()).Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            await tags.CreateAsync(new Tag { Name = name });
    }

    private async Task<(HabitScreenViewModel Vm, HabitScreenView View, Window Window)> OpenAsync(Guid id, ThemeVariant? variant = null)
    {
        var vm = Host.Provider.CreateScope().ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 900, Height = 900, Content = view };
        if (variant is not null) w.RequestedThemeVariant = variant;
        _windows.Add(w);
        w.Show();
        PrivateDbHost.Pump();
        return (vm, view, w);
    }

    private static T Find<T>(Avalonia.Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static async Task WaitAsync(Func<bool> done)
    {
        for (var i = 0; i < 150 && !done(); i++) { await Task.Delay(20); PrivateDbHost.Pump(); }
        PrivateDbHost.Pump(); // la condizione e' vera ma layout e binding possono essere ancora in coda: svuotarli prima di guardare le viste
        done().Should().BeTrue("la condizione doveva avverarsi");
    }

    [AvaloniaFact]
    public async Task The_today_button_of_a_journal_opens_the_diario_column_with_the_composer()
    {
        var (vm, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-1"));
        vm.IsDiarioOpen = false;
        PrivateDbHost.Pump();
        Find<Border>(view, "DiarioColumn").IsVisible.Should().BeFalse();

        Find<Button>(view, "PrimaryButton").Command!.Execute(null);
        PrivateDbHost.Pump();

        vm.IsDiarioOpen.Should().BeTrue();
        Find<Border>(view, "DiarioColumn").IsVisible.Should().BeTrue();
        view.GetVisualDescendants().OfType<JournalQuickEntryView>().Should().ContainSingle();
        Find<TextBox>(view, "DraftBox").IsEffectivelyVisible.Should().BeTrue();
        Find<CheckBox>(view, "ChipDiario").IsVisible.Should().BeFalse("il chip Diario resta nascosto fino a F2d-2 (spec 5.2): la colonna si apre dal pulsante Oggi");
    }

    [AvaloniaFact]
    public async Task Typing_a_hash_opens_the_menu_and_enter_accepts_without_a_line_break()
    {
        await EnsureTagAsync("pranzo");
        var (vm, view, window) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-menu"));
        var box = Find<TextBox>(view, "DraftBox");
        var menu = Find<Avalonia.Controls.Primitives.Popup>(view, "TagMenu");

        box.Focus();
        box.Text = "#pra";
        box.CaretIndex = 4;
        PrivateDbHost.Pump();

        menu.IsOpen.Should().BeTrue();
        Items(view).Should().Equal("#pranzo", CreateLabel("#pra"));

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        PrivateDbHost.Pump();

        box.Text.Should().Be("#pranzo ");
        vm.QuickEntryForDiario!.Draft.Should().Be("#pranzo ");
        box.Text.Should().NotContain("\n");
        menu.IsOpen.Should().BeFalse();
    }

    private static ListBox List(Avalonia.Visual view) => view.GetVisualDescendants().OfType<JournalQuickEntryView>().Single().FindControl<ListBox>("InlineList")!;
    private static IEnumerable<string> Items(Avalonia.Visual view) => List(view).Items.Cast<string>();
    private static string CreateLabel(string n) => string.Format(System.Globalization.CultureInfo.CurrentCulture, Recordum.Presentation.Resources.Strings.Diario_Tag_Create, n);

    private static void Type(TextBox box, string text)
    {
        box.Focus();
        box.Text = text;
        box.CaretIndex = text.Length;
        PrivateDbHost.Pump();
    }

    [AvaloniaFact]
    public async Task With_no_matching_tag_the_menu_offers_only_create_and_enter_makes_the_tag_without_a_line_break()
    {
        var (_, view, window) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-crea"));
        var box = Find<TextBox>(view, "DraftBox");
        Type(box, "#zupqx");
        Items(view).Should().Equal([CreateLabel("#zupqx")]);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        PrivateDbHost.Pump();
        box.Text.Should().Be("#zupqx ");
        box.Text.Should().NotContain("\n");
    }

    [AvaloniaFact]
    public async Task A_partial_prefix_lists_the_match_then_create()
    {
        await EnsureTagAsync("pranzo");
        var (_, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-crea2"));
        Type(Find<TextBox>(view, "DraftBox"), "#pra");
        Items(view).Should().Equal("#pranzo", CreateLabel("#pra"));
    }

    [AvaloniaFact]
    public async Task Down_then_enter_accepts_the_second_suggestion_and_tab_accepts_too()
    {
        await EnsureTagAsync("zebra1");
        await EnsureTagAsync("zebra2");
        var (_, view, window) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-giu"));
        var box = Find<TextBox>(view, "DraftBox");
        Type(box, "#zebra");
        Items(view).Should().Equal("#zebra1", "#zebra2", CreateLabel("#zebra"));
        window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        PrivateDbHost.Pump();
        box.Text.Should().Be("#zebra2 ");

        Type(box, "#zebra");
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        PrivateDbHost.Pump();
        box.Text.Should().Be("#zebra1 ");
    }

    [AvaloniaFact]
    public async Task The_menu_closes_when_focus_leaves_the_text_box_and_a_tap_on_an_item_still_picks()
    {
        await EnsureTagAsync("pranzo");
        var (_, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-fuoco"));
        var box = Find<TextBox>(view, "DraftBox");
        var menu = Find<Avalonia.Controls.Primitives.Popup>(view, "TagMenu");
        Type(box, "#pra");
        menu.IsOpen.Should().BeTrue();

        Find<TextBox>(view, "TagBox").Focus();
        PrivateDbHost.Pump();
        menu.IsOpen.Should().BeFalse("fuori fuoco il menu non copre la riga Tag");

        Type(box, "#pran");
        menu.IsOpen.Should().BeTrue();
        var item = List(view).GetVisualDescendants().OfType<ListBoxItem>().First();
        List(view).SelectedItem = item.DataContext;
        var release = new Avalonia.Input.PointerReleasedEventArgs(item, new Avalonia.Input.Pointer(0, Avalonia.Input.PointerType.Mouse, true),
            TopLevel.GetTopLevel(item)!, new Avalonia.Point(1, 1), 0,
            new Avalonia.Input.PointerPointProperties(RawInputModifiers.None, Avalonia.Input.PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left);
        item.RaiseEvent(new Avalonia.Input.TappedEventArgs(Avalonia.Input.InputElement.TappedEvent, release));
        PrivateDbHost.Pump();
        box.Text.Should().Be("#pranzo ");
    }

    [AvaloniaFact]
    public async Task The_menu_is_placed_under_the_caret_line()
    {
        await EnsureTagAsync("pranzo");
        var (_, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-cursore"));
        var box = Find<TextBox>(view, "DraftBox");
        var menu = Find<Avalonia.Controls.Primitives.Popup>(view, "TagMenu");
        Type(box, "#pra");
        var y1 = menu.PlacementRect!.Value.Y;
        Type(box, "uno\ndue\ntre #pra");
        var y3 = menu.PlacementRect!.Value.Y;
        y3.Should().BeGreaterThan(y1 + 10, "con il testo sulla terza riga il menu scende con il cursore");
    }

    [AvaloniaFact]
    public async Task The_composer_is_built_only_while_the_diario_column_is_shown()
    {
        var (vm, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-chiuso"));
        vm.IsDiarioOpen = false;
        PrivateDbHost.Pump();
        view.GetVisualDescendants().OfType<JournalQuickEntryView>().Should().BeEmpty("colonna chiusa: nessun composer nell'intera view");
        vm.IsDiarioOpen = true;
        PrivateDbHost.Pump();
        view.GetVisualDescendants().OfType<JournalQuickEntryView>().Should().ContainSingle();
    }

    [AvaloniaFact]
    public async Task Enter_in_the_tag_row_adds_the_exact_known_tag_not_the_first_prefix_match()
    {
        await EnsureTagAsync("cenone");
        var (vm, view, window) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-riga"));
        var qe = vm.QuickEntryForDiario!;
        var tagBox = Find<TextBox>(view, "TagBox");
        tagBox.Focus();
        tagBox.Text = "cenon";
        PrivateDbHost.Pump();
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        PrivateDbHost.Pump();
        qe.PropertyTags.Tags.Should().Equal(["cenon"], "digitato ma non noto: si crea, non si completa");

        tagBox.Text = "CENONE";
        PrivateDbHost.Pump();
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        PrivateDbHost.Pump();
        qe.PropertyTags.Tags.Should().Equal("cenon", "cenone");
    }

    [AvaloniaFact]
    public async Task Escape_closes_the_menu_and_the_next_keystroke_reopens_it()
    {
        await EnsureTagAsync("palestra");
        var (_, view, window) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-esc"));
        var box = Find<TextBox>(view, "DraftBox");
        var menu = Find<Avalonia.Controls.Primitives.Popup>(view, "TagMenu");
        box.Focus();
        box.Text = "#pa";
        box.CaretIndex = 3;
        PrivateDbHost.Pump();
        menu.IsOpen.Should().BeTrue();

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        PrivateDbHost.Pump();
        menu.IsOpen.Should().BeFalse();

        box.Text = "#pal";
        box.CaretIndex = 4;
        PrivateDbHost.Pump();
        menu.IsOpen.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task Saving_with_a_photo_puts_the_entry_in_today_with_thumbnail_chips_and_local_time()
    {
        var (vm, view, window) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-salva"));
        var qe = vm.QuickEntryForDiario!;
        var png = JournalFixtures.WritePng();
        try
        {
            Dialog.Paths = [png];
            Find<Button>(view, "AddPhotoButton").Command!.Execute(null);
            await WaitAsync(() => qe.PendingPhotos.Count == 1 && qe.Draft.Contains("photo:", StringComparison.Ordinal));
            Find<ItemsControl>(view, "PendingPhotos").GetVisualDescendants().OfType<Image>()
                .Should().ContainSingle(i => i.Source != null, "la miniatura in attesa");

            Find<TextBox>(view, "DraftBox").Text = "Zuppa #pranzo\n" + qe.Draft; // AddPhoto ha gia' messo il riferimento in coda: scrivere sopra lo cancellerebbe
            qe.PropertyTags.Add("cena");
            PrivateDbHost.Pump();

            Find<Button>(view, "SaveButton").Command!.Execute(null);
            await WaitAsync(() => qe.Entries.Count == 1);
            PrivateDbHost.Pump();

            var list = Find<ItemsControl>(view, "EntriesList");
            var row = qe.Entries.Single();
            row.TimeDisplay.Should().Be("13:42", "11:42 UTC con l'orologio finto a +02:00");
            list.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Text == "13:42");
            list.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Text != null && t.Text.StartsWith("Zuppa #pranzo", StringComparison.Ordinal));
            list.GetVisualDescendants().OfType<Image>().Should().ContainSingle(i => i.Source != null, "la miniatura della voce");
            var chips = list.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("tag-chip")).ToList();
            chips.Should().ContainSingle(b => b.Classes.Contains("inline"));
            chips.Should().ContainSingle(b => b.Classes.Contains("property"));
            qe.PendingPhotos.Should().BeEmpty();

            output.WriteLine(ScreenshotProbeTests.Capture(window, "journal-quick-entry-light"));
        }
        finally { File.Delete(png); }
    }

    [AvaloniaFact]
    public async Task A_missing_thumbnail_shows_the_image_off_icon_with_a_tooltip()
    {
        var (vm, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-manca"));
        var qe = vm.QuickEntryForDiario!;
        var id = Guid.NewGuid();
        var row = new JournalEntryRowViewModel(
            new Note { Id = id, BodyMd = "x", CreatedAt = Clock.UtcNow.UtcDateTime }, [], Clock);
        row.Photos.Add(new Recordum.Presentation.ViewModels.HabitScreen.Notes.AttachmentViewModel(
            new Attachment { Id = Guid.NewGuid(), NoteId = id, Filename = "a.jpg", MimeType = "image/jpeg", IsMissing = true }, null));
        qe.Entries.Add(row);
        PrivateDbHost.Pump();

        var icon = Find<ItemsControl>(view, "EntriesList").GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>()
            .Single(p => p.Name == "MissingPhoto");
        icon.IsEffectivelyVisible.Should().BeTrue();
        ToolTip.GetTip(icon).Should().Be(Recordum.Presentation.Resources.Strings.Diario_Photo_Missing);
        Find<ItemsControl>(view, "EntriesList").GetVisualDescendants().OfType<Image>().Should().NotContain(i => i.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Editing_a_row_shows_added_and_existing_photos_and_cancel_restores_them()
    {
        var (vm, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-modifica"));
        var qe = vm.QuickEntryForDiario!;
        var png = JournalFixtures.WritePng();
        try
        {
            Dialog.Paths = [png];
            Find<Button>(view, "AddPhotoButton").Command!.Execute(null);
            await WaitAsync(() => qe.PendingPhotos.Count == 1 && qe.Draft.Contains("photo:", StringComparison.Ordinal));
            Find<TextBox>(view, "DraftBox").Text = "Ciao\n" + qe.Draft;
            Find<Button>(view, "SaveButton").Command!.Execute(null);
            await WaitAsync(() => qe.Entries.Count == 1);
            PrivateDbHost.Pump();
            var list = Find<ItemsControl>(view, "EntriesList");

            qe.BeginEditCommand.Execute(qe.Entries.Single());
            PrivateDbHost.Pump();
            list.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "RemoveExistingPhotoButton")
                .Should().ContainSingle(b => b.IsEffectivelyVisible);

            Dialog.Paths = [png];
            Find<Button>(view, "AddPhotoButton").Command!.Execute(null);
            await WaitAsync(() => qe.Entries.Single().AddedPhotos.Count == 1);
            list.GetVisualDescendants().OfType<Image>().Count(i => i.IsEffectivelyVisible && i.Source != null)
                .Should().Be(2, "la foto salvata e quella aggiunta, una accanto all'altra");

            list.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "RemoveExistingPhotoButton" && b.IsEffectivelyVisible)
                .Command!.Execute(list.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "RemoveExistingPhotoButton" && b.IsEffectivelyVisible).CommandParameter);
            PrivateDbHost.Pump();
            qe.Entries.Single().RemovedPhotoIds.Should().HaveCount(1);

            qe.CancelEditCommand.Execute(null);
            PrivateDbHost.Pump();
            qe.Entries.Single().Photos.Should().HaveCount(1, "Annulla riporta la foto tolta");
            qe.Entries.Single().AddedPhotos.Should().BeEmpty();
        }
        finally { File.Delete(png); }
    }

    [AvaloniaFact]
    public async Task Save_is_disabled_while_a_photo_pick_runs_and_add_photo_while_saving()
    {
        var (vm, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Diario-stati"));
        var qe = vm.QuickEntryForDiario!;
        var save = Find<Button>(view, "SaveButton");
        var add = Find<Button>(view, "AddPhotoButton");
        save.IsEnabled.Should().BeTrue();
        add.IsEnabled.Should().BeTrue();

        var gate = new TaskCompletionSource<string[]>();
        var blocking = new BlockingDialog(gate.Task);
        var field = typeof(JournalQuickEntryViewModel).GetField("_dialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var original = field.GetValue(qe);
        field.SetValue(qe, blocking);
        try
        {
            var running = qe.AddPhotoCommand.ExecuteAsync(null);
            PrivateDbHost.Pump();
            save.IsEnabled.Should().BeFalse("Salva e' rifiutato mentre una scelta di foto e' in corso");
            gate.SetResult([]);
            await running;
            PrivateDbHost.Pump();
            save.IsEnabled.Should().BeTrue();
        }
        finally { field.SetValue(qe, original); }

        qe.IsSaving = true;
        PrivateDbHost.Pump();
        add.IsEnabled.Should().BeFalse("Aggiungi foto e' rifiutato durante un salvataggio");
        qe.IsSaving = false;
    }

    private sealed class BlockingDialog(Task<string[]> gate) : IOpenFileDialog
    {
        public Task<string?> PromptAsync(string filter) => Task.FromResult<string?>(null);
        public Task<string[]> PromptMultipleAsync(string filter) => gate;
    }

    [AvaloniaFact]
    public async Task The_diario_column_builds_no_composer_for_a_habit_that_is_not_a_journal()
    {
        var (vm, view, _) = await OpenAsync(await CreateAsync(HabitType.Action, "Azione-no-diario"));
        vm.QuickEntryForDiario.Should().BeNull();
        view.GetVisualDescendants().OfType<JournalQuickEntryView>().Should().BeEmpty("nell'intera view");
        view.GetVisualDescendants().OfType<TagEditorView>().Should().BeEmpty();
        Find<CheckBox>(view, "ChipDiario").IsVisible.Should().BeFalse();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_screen_with_an_entry_and_a_photo_renders_in_both_themes(string variant)
    {
        using var culture = TestCulture.Pin();
        var (vm, view, window) = await OpenAsync(await CreateAsync(HabitType.Journal, $"Diario-{variant}"),
            variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);
        var qe = vm.QuickEntryForDiario!;
        var png = JournalFixtures.WritePng(160, 120);
        try
        {
            Dialog.Paths = [png];
            Find<Button>(view, "AddPhotoButton").Command!.Execute(null);
            await WaitAsync(() => qe.PendingPhotos.Count == 1 && qe.Draft.Contains("photo:", StringComparison.Ordinal));
            Find<TextBox>(view, "DraftBox").Text = "Insalata e un caffe #pranzo\n" + qe.Draft;
            qe.PropertyTags.Add("salute");
            Find<Button>(view, "SaveButton").Command!.Execute(null);
            await WaitAsync(() => qe.Entries.Count == 1);
            await Task.Delay(100);
            PrivateDbHost.Pump();

            output.WriteLine(ScreenshotProbeTests.Capture(window, $"journal-entry-photo-{variant.ToLowerInvariant()}"));

            var list = Find<ItemsControl>(view, "EntriesList");
            list.GetVisualDescendants().OfType<Image>().Should().ContainSingle(i => i.Source != null && i.Bounds.Width > 0);
            list.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("tag-chip")).Should().Be(2);
            Find<TextBox>(view, "DraftBox").Bounds.Height.Should().BeGreaterThan(40);
        }
        finally { File.Delete(png); }
    }
}
