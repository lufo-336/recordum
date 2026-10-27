using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Text;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Calendar;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Journal;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>F2d-2, Task 7: la colonna Diario in modalita' diario (gesto Diary): un solo DiaryEditor per la voce nuova e per
/// quella in modifica, le voci del giorno rese in sola lettura sotto, il menu # condiviso con la nota veloce.</summary>
public sealed class JournalDiaryEditorViewTests(ITestOutputHelper output) : IDisposable
{
    private readonly List<Window> _windows = [];
    public void Dispose() { foreach (var w in _windows) w.Close(); }

    private static readonly ZonedClock Clock = new();
    private static readonly Picker Dialog = new();
    private static readonly Confirm Answer = new();
    private static readonly PrivateDbHost Host = new("recordum-ui-diary", s =>
    {
        s.AddSingleton<Recordum.Core.Abstractions.IClock>(Clock);
        s.AddSingleton<IOpenFileDialog>(Dialog);
        s.AddSingleton<IConfirmationDialog>(Answer);
    });

    /// <summary>La finestra dei file: i percorsi dati, o un'attesa che il test sblocca (o fa fallire).</summary>
    private sealed class Picker : IOpenFileDialog
    {
        public string[] Paths { get; set; } = [];
        public Task<string[]>? Pending { get; set; }
        public Task<string?> PromptAsync(string filter) => Task.FromResult(Paths.FirstOrDefault());
        public Task<string[]> PromptMultipleAsync(string filter) => Pending ?? Task.FromResult(Paths);
    }

    private sealed class Confirm : IConfirmationDialog
    {
        public bool Yes { get; set; }
        public int Asked { get; set; }
        public Task<bool> ConfirmAsync(string title, string message) { Asked++; return Task.FromResult(Yes); }
    }

    private static readonly DateOnly Today = new(2026, 3, 15);
    private static T Svc<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Un diario col gesto di default (Diary) o "nota veloce"; o un'altra abitudine.</summary>
    private static async Task<Guid> CreateAsync(HabitType type, string name, JournalPrimaryGesture? gesture = null)
    {
        using var scope = Host.Provider.CreateScope();
        var id = await Svc<IHabitNodeService>(scope).CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = name, Color = "#7C5CBF",
        });
        if (gesture is { } g) await Svc<IHabitUiPrefsService>(scope).SetAsync(id, new HabitUiPrefs { JournalGesture = g });
        return id;
    }

    // Il DB e' condiviso fra i test della classe: un tag con quel nome puo' esistere gia' (nome unico, NOCASE).
    private static async Task EnsureTagAsync(string name)
    {
        using var scope = Host.Provider.CreateScope();
        var tags = Svc<ITagService>(scope);
        if (!(await tags.ListAsync()).Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            await tags.CreateAsync(new Tag { Name = name });
    }

    /// <summary>Una voce salvata dal servizio; con <paramref name="photo"/> la foto e' importata e il suo riferimento e' nel testo.</summary>
    private static async Task SeedAsync(Guid id, DateOnly day, string text, bool photo = false)
    {
        using var scope = Host.Provider.CreateScope();
        var photos = new List<ImportedPhoto>();
        if (photo)
        {
            var png = JournalFixtures.WritePng(160, 120);
            try { photos.Add(await Svc<IPhotoImporter>(scope).ImportAsync(png, CancellationToken.None)); }
            finally { File.Delete(png); }
            text += "\n" + PhotoRefs.Format(photos[0].Id, photos[0].Filename);
        }
        await Svc<IDiaryService>(scope).SaveEntryAsync(id, day, new DiaryEntryDraft(text, [], photos));
    }

    private async Task<(HabitScreenViewModel Vm, HabitScreenView View, Window Window)> OpenAsync(Guid id, ThemeVariant? variant = null, double height = 900)
    {
        Dialog.Pending = null;
        Answer.Yes = false; Answer.Asked = 0;
        var vm = Host.Provider.CreateScope().ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 900, Height = height, Content = view };
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
        PrivateDbHost.Pump();
        done().Should().BeTrue("la condizione doveva avverarsi");
    }

    /// <summary>L'editor che scrive (l'unico non in sola lettura).</summary>
    private static DiaryEditor Composer(Avalonia.Visual view) =>
        view.GetVisualDescendants().OfType<DiaryEditor>().Single(e => !e.IsReadOnly);

    /// <summary>Gli editor in sola lettura delle voci del giorno.</summary>
    private static List<DiaryEditor> Rendered(Avalonia.Visual view) =>
        [.. Find<ItemsControl>(view, "EntriesList").GetVisualDescendants().OfType<DiaryEditor>()];

    private static string Text(DiaryEditor editor) => editor.Markdown.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void FocusComposer(Avalonia.Visual view)
    {
        Composer(view).GetVisualDescendants().OfType<TextArea>().Single().Focus();
        PrivateDbHost.Pump();
    }

    // Un pulsante con un comando lo esegue; "Inserisci immagine…" ha un gestore Click.
    private static void Click(Button button)
    {
        if (button.Command is { } command) command.Execute(button.CommandParameter);
        else button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        PrivateDbHost.Pump();
    }

    private static void ClickAt(Window w, Control c)
    {
        var p = c.TranslatePoint(new Avalonia.Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        PrivateDbHost.Pump();
    }

    private static void PressEnter(Window window)
    {
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        PrivateDbHost.Pump();
    }

    private static Button EditButton(Avalonia.Visual view) =>
        Find<ItemsControl>(view, "EntriesList").GetVisualDescendants().OfType<Button>()
            .Single(b => Equals(b.Content, Strings.Diario_QuickEntry_Edit));

    private static Button DayCell(Avalonia.Visual view, DateOnly date) =>
        view.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Classes.Contains("day") && ((CalendarDayCellViewModel)b.DataContext!).Date == date);

    // ---- 7.1 ----

    [AvaloniaFact]
    public async Task The_today_button_of_a_diary_opens_the_editor_and_a_quick_note_opens_the_text_box()
    {
        var (vm, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Sera-71"));
        vm.IsDiarioOpen = false;
        PrivateDbHost.Pump();
        Find<Button>(view, "PrimaryButton").Command!.Execute(null);
        PrivateDbHost.Pump();

        Find<Border>(view, "DiarioColumn").IsVisible.Should().BeTrue();
        vm.QuickEntryForDiario!.IsDiaryMode.Should().BeTrue();
        view.GetVisualDescendants().OfType<JournalDiaryView>().Should().ContainSingle();
        Composer(view).IsEffectivelyVisible.Should().BeTrue();
        view.GetVisualDescendants().OfType<JournalQuickEntryView>().Should().BeEmpty("la nota veloce non si costruisce");
        view.GetVisualDescendants().OfType<TextBox>().Should().NotContain(t => t.Name == "DraftBox", "nessuna TextBox del composer");

        var (qvm, quick, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Veloce-71", JournalPrimaryGesture.QuickNote));
        qvm.IsDiarioOpen = false;
        PrivateDbHost.Pump();
        Find<Button>(quick, "PrimaryButton").Command!.Execute(null);
        PrivateDbHost.Pump();

        Find<TextBox>(quick, "DraftBox").IsEffectivelyVisible.Should().BeTrue();
        quick.GetVisualDescendants().OfType<DiaryEditor>().Should().BeEmpty("in nota veloce nessun editor del diario");
        quick.GetVisualDescendants().OfType<JournalDiaryView>().Should().BeEmpty();
    }

    // ---- 7.2 (Review Focus 3) ----

    [AvaloniaFact]
    public async Task Typing_a_hash_in_the_editor_opens_the_menu_and_enter_accepts_without_a_line_break()
    {
        await EnsureTagAsync("pranzo");
        var (vm, view, window) = await OpenAsync(await CreateAsync(HabitType.Journal, "Sera-72"));
        var menu = Find<Popup>(view, "TagMenu");
        FocusComposer(view);

        window.KeyTextInput("#pra");
        PrivateDbHost.Pump();

        menu.IsOpen.Should().BeTrue();
        view.GetVisualDescendants().OfType<JournalDiaryView>().Single().FindControl<ListBox>("InlineList")!.Items.Cast<string>().Should().StartWith("#pranzo");
        menu.PlacementTarget.Should().BeSameAs(Composer(view));
        menu.PlacementRect.Should().NotBeNull("il menu si ancora alla riga del cursore");

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        PrivateDbHost.Pump();

        Text(Composer(view)).Should().Be("#pranzo ");
        vm.QuickEntryForDiario!.Draft.Should().Be("#pranzo ");
        menu.IsOpen.Should().BeFalse();

        // Col menu chiuso Invio torna a essere un a capo.
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        PrivateDbHost.Pump();
        Text(Composer(view)).Should().Be("#pranzo \n");
    }

    [AvaloniaFact]
    public async Task Escape_closes_the_editor_menu_and_the_next_keystroke_reopens_it()
    {
        await EnsureTagAsync("palestra");
        var (_, view, window) = await OpenAsync(await CreateAsync(HabitType.Journal, "Sera-72esc"));
        var menu = Find<Popup>(view, "TagMenu");
        FocusComposer(view);
        window.KeyTextInput("#pa");
        PrivateDbHost.Pump();
        menu.IsOpen.Should().BeTrue();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        PrivateDbHost.Pump();
        menu.IsOpen.Should().BeFalse();

        window.KeyTextInput("l");
        PrivateDbHost.Pump();
        menu.IsOpen.Should().BeTrue();
    }

    // ---- 7.3 ----

    [AvaloniaFact]
    public async Task Insert_image_then_save_puts_the_entry_below_rendered_with_its_image_and_no_duplicate_thumbnail()
    {
        var (vm, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Sera-73"));
        var qe = vm.QuickEntryForDiario!;
        var editor = Composer(view);
        editor.ReplaceText("Pranzo al mare", "Pranzo al mare".Length);
        PrivateDbHost.Pump();
        var png = JournalFixtures.WritePng(160, 120);
        try
        {
            Dialog.Paths = [png];
            Click(Find<Button>(view, "InsertImageButton"));
            await WaitAsync(() => qe.PendingPhotos.Count == 1 && qe.Draft.Contains("photo:", StringComparison.Ordinal));
        }
        finally { File.Delete(png); }

        // Il riferimento sta al cursore, in una riga a se', e il cursore va dopo.
        Text(editor).Should().Be("Pranzo al mare\n" + PhotoRefs.Format(qe.PendingPhotos[0].Id, qe.PendingPhotos[0].Filename) + "\n");
        editor.CaretOffset.Should().Be(editor.Markdown.Length);
        editor.GetVisualDescendants().OfType<Image>().Should().ContainSingle(i => i.Source != null, "l'editor mostra la miniatura in attesa");

        Click(Find<Button>(view, "SaveButton"));
        await WaitAsync(() => qe.Entries.Count == 1 && Rendered(view).Count == 1);

        Text(editor).Should().BeEmpty("dopo Salva l'editor torna vuoto");
        var rendered = Rendered(view).Single();
        rendered.IsReadOnly.Should().BeTrue();
        Text(rendered).Should().StartWith("Pranzo al mare");
        rendered.GetVisualDescendants().OfType<Image>().Should().ContainSingle(i => i.Source != null && i.Bounds.Width > 0, "la voce e' resa con l'immagine");
        Find<ItemsControl>(view, "UnreferencedPhotos").GetVisualDescendants().OfType<Image>()
            .Should().BeEmpty("la foto e' nel testo: nessuna miniatura duplicata nella striscia");
    }

    [AvaloniaFact]
    public async Task A_rendered_entry_sizes_to_its_text_and_does_not_scroll_on_its_own()
    {
        var id = await CreateAsync(HabitType.Journal, "Sera-73h");
        await SeedAsync(id, Today, "una riga");
        Clock.Now = Clock.Now.AddMinutes(1);
        try { await SeedAsync(id, Today, "uno\ndue\ntre\nquattro\ncinque\nsei"); }
        finally { Clock.Now = Clock.Now.AddMinutes(-1); }
        var (_, view, _) = await OpenAsync(id);
        await WaitAsync(() => Rendered(view).Count == 2 && Rendered(view).All(e => e.Bounds.Height > 0));

        var one = Rendered(view).Single(e => e.Markdown == "una riga");
        var six = Rendered(view).Single(e => e.Markdown.StartsWith("uno", StringComparison.Ordinal));
        output.WriteLine($"one={one.Bounds.Height} six={six.Bounds.Height}");
        one.Bounds.Height.Should().BeLessThan(80, "una riga non si allunga a riempire la colonna");
        six.Bounds.Height.Should().BeGreaterThan(one.Bounds.Height * 3, "sei righe, tutte visibili");
        foreach (var e in new[] { one, six })
        {
            var scroll = e.GetVisualDescendants().OfType<ScrollViewer>().Single();
            scroll.Extent.Height.Should().BeLessThanOrEqualTo(scroll.Viewport.Height + 1, "il testo intero e' visibile, niente scorrimento interno");
        }
    }

    [AvaloniaFact]
    public async Task The_wheel_over_a_rendered_entry_scrolls_the_column()
    {
        var id = await CreateAsync(HabitType.Journal, "Sera-73w");
        await SeedAsync(id, Today, string.Join("\n", Enumerable.Range(1, 60).Select(i => $"riga {i}")));
        var (_, view, window) = await OpenAsync(id);
        await WaitAsync(() => Rendered(view).Count == 1 && Rendered(view)[0].Bounds.Height > 600);
        var column = view.GetVisualDescendants().OfType<JournalDiaryView>().Single().FindControl<ScrollViewer>("Scroll")!;
        column.Offset.Y.Should().Be(0);

        var rendered = Rendered(view).Single();
        var visibleTop = Math.Max(0, column.TranslatePoint(new Point(0, 0), rendered)!.Value.Y);
        var p = rendered.TranslatePoint(new Point(40, visibleTop + 20), window)!.Value;
        (window.InputHitTest(p) as Visual)!.FindAncestorOfType<DiaryEditor>(includeSelf: true).Should().BeSameAs(rendered, "la rotella cade sulla voce resa");
        window.MouseWheel(p, new Vector(0, -3));
        PrivateDbHost.Pump();

        column.Offset.Y.Should().BeGreaterThan(0, "la rotella sopra una voce resa scorre la colonna, non l'editor della voce");
    }

    // ---- 7.4 ----

    [AvaloniaTheory]
    [InlineData("en-US")]
    [InlineData("it-IT")]
    public async Task Edit_brings_the_entry_into_the_editor_with_the_strip_and_cancel_brings_back_the_draft(string culture)
    {
        var previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var id = await CreateAsync(HabitType.Journal, $"Sera-74-{culture}");
            await SeedAsync(id, Today, "Cena con Anna");
            var (vm, view, _) = await OpenAsync(id);
            var qe = vm.QuickEntryForDiario!;
            await WaitAsync(() => Rendered(view).Count == 1);
            var editor = Composer(view);
            editor.ReplaceText("bozza nuova", "bozza nuova".Length);
            PrivateDbHost.Pump();
            Find<Border>(view, "EditingStrip").IsVisible.Should().BeFalse();

            var edit = Find<ItemsControl>(view, "EntriesList").GetVisualDescendants().OfType<Button>()
                .Single(b => Equals(b.Content, Strings.Diario_QuickEntry_Edit));
            Click(edit);
            await WaitAsync(() => qe.EditingEntry is not null);

            Find<Border>(view, "EditingStrip").IsVisible.Should().BeTrue();
            Find<TextBlock>(view, "EditingBannerText").Text.Should().Be(
                string.Format(CultureInfo.InvariantCulture, culture == "it-IT" ? "Stai modificando la voce delle {0}" : "Editing the entry from {0}", "13:42"));
            Text(Composer(view)).Should().Be("Cena con Anna");
            Composer(view).Should().BeSameAs(editor, "una superficie sola: la voce in modifica va nello stesso editor");

            Click(Find<Button>(view, "CancelEditButton"));
            PrivateDbHost.Pump();

            Find<Border>(view, "EditingStrip").IsVisible.Should().BeFalse();
            Text(Composer(view)).Should().Be("bozza nuova", "la bozza della voce nuova aspetta e torna");
        }
        finally { Strings.Culture = previous; }
    }

    // ---- 7.5 (Review Focus 5) ----

    [AvaloniaFact]
    public async Task Choosing_yesterday_shows_its_entries_with_images_and_a_new_entry_goes_on_yesterday()
    {
        var yesterday = Today.AddDays(-1);
        var id = await CreateAsync(HabitType.Journal, "Sera-75");
        await SeedAsync(id, yesterday, "Ieri al lago", photo: true);
        await SeedAsync(id, Today, "Oggi in ufficio");
        var (vm, view, window) = await OpenAsync(id);
        var qe = vm.QuickEntryForDiario!;
        await WaitAsync(() => Rendered(view).Count == 1);
        Text(Rendered(view).Single()).Should().Be("Oggi in ufficio");

        vm.IsCronologiaOpen = true;
        PrivateDbHost.Pump();
        var day = view.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Classes.Contains("day") && ((CalendarDayCellViewModel)b.DataContext!).Date == yesterday);
        ClickAt(window, day);
        await WaitAsync(() => Rendered(view).Count == 1 && Rendered(view)[0].Markdown.StartsWith("Ieri al lago", StringComparison.Ordinal)
            && Rendered(view)[0].GetVisualDescendants().OfType<Image>().Any(i => i.Source != null));

        Composer(view).ReplaceText("Scritta oggi per ieri", 0);
        PrivateDbHost.Pump();
        Click(Find<Button>(view, "SaveButton"));
        await WaitAsync(() => qe.Entries.Count == 2);

        using var scope = Host.Provider.CreateScope();
        var completions = await Svc<ICompletionService>(scope).ListByHabitAsync(id);
        completions.Should().HaveCount(3);
        completions.Count(c => DateOnly.FromDateTime(c.CompletedAt) == yesterday)
            .Should().Be(2, "la voce nuova scritta col giorno di ieri scelto va su ieri");
    }

    // ---- 7.6 ----

    [AvaloniaFact]
    public async Task The_diario_chip_is_visible_for_a_diary_and_absent_for_an_action()
    {
        var (vm, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Sera-76"));
        var chip = Find<CheckBox>(view, "ChipDiario");
        chip.IsEffectivelyVisible.Should().BeTrue();
        vm.IsDiarioOpen = false;
        PrivateDbHost.Pump();
        chip.IsChecked.Should().BeFalse();
        chip.IsChecked = true;
        PrivateDbHost.Pump();
        vm.IsDiarioOpen.Should().BeTrue("il chip apre la colonna");

        var (_, action, _) = await OpenAsync(await CreateAsync(HabitType.Action, "Azione-76"));
        Find<CheckBox>(action, "ChipDiario").IsVisible.Should().BeFalse();
    }

    // ---- 7.7 ----

    [AvaloniaFact]
    public async Task Closing_the_column_builds_no_editor_anywhere_in_the_view()
    {
        var id = await CreateAsync(HabitType.Journal, "Sera-77");
        await SeedAsync(id, Today, "una voce resa");
        var (vm, view, _) = await OpenAsync(id);
        await WaitAsync(() => Rendered(view).Count == 1);

        Click(Find<Button>(view, "DiarioCloseButton"));
        vm.IsDiarioOpen.Should().BeFalse();
        view.GetVisualDescendants().OfType<DiaryEditor>().Should().BeEmpty("colonna chiusa: nessun editor nell'intera view");
        view.GetVisualDescendants().OfType<JournalDiaryView>().Should().BeEmpty();
        view.GetVisualDescendants().OfType<JournalQuickEntryView>().Should().BeEmpty();
        view.GetVisualDescendants().OfType<TagEditorView>().Should().BeEmpty();

        vm.IsDiarioOpen = true;
        PrivateDbHost.Pump();
        view.GetVisualDescendants().OfType<JournalDiaryView>().Should().ContainSingle();
        view.GetVisualDescendants().OfType<DiaryEditor>().Count(e => !e.IsReadOnly).Should().Be(1);
    }

    // ---- 7.8 ----

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_column_with_a_rendered_entry_renders_in_both_themes(string variant)
    {
        using var culture = TestCulture.Pin();
        await EnsureTagAsync("lago");
        var id = await CreateAsync(HabitType.Journal, $"Sera-78-{variant}");
        await SeedAsync(id, Today, "## Una giornata al lago\n\nAcqua **fredda**, sole *caldo*. #lago\n- pane\n- frutta\n> da rifare", photo: true);
        var (vm, view, window) = await OpenAsync(id, variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);
        await WaitAsync(() => Rendered(view).Count == 1
            && Rendered(view)[0].GetVisualDescendants().OfType<Image>().Any(i => i.Source != null && i.Bounds.Width > 0));
        Composer(view).ReplaceText("Stasera scrivo del lago", "Stasera scrivo del lago".Length);
        await Task.Delay(100);
        PrivateDbHost.Pump();

        output.WriteLine(ScreenshotProbeTests.Capture(window, $"journal-diary-editor-{variant.ToLowerInvariant()}"));

        vm.QuickEntryForDiario!.IsDiaryMode.Should().BeTrue();
        Composer(view).Bounds.Height.Should().BeGreaterThanOrEqualTo(160);
        var rendered = Rendered(view).Single();
        rendered.Bounds.Height.Should().BeGreaterThan(100, "titolo, testo, elenco, citazione e immagine");
        rendered.GetVisualDescendants().OfType<Image>().Should().ContainSingle(i => i.Source != null && i.Bounds.Width > 0);
        Find<ItemsControl>(view, "EntriesList").GetVisualDescendants().OfType<Border>()
            .Should().ContainSingle(b => b.Classes.Contains("tag-chip") && b.Classes.Contains("inline"));
        Find<Button>(view, "InsertImageButton").Content.Should().Be(Strings.Diario_Editor_InsertImage);
    }

    // ---- Fix round 1 ----

    [AvaloniaFact]
    public async Task Typing_past_the_bottom_scrolls_the_column_to_the_caret()
    {
        var (_, view, window) = await OpenAsync(await CreateAsync(HabitType.Journal, "Sera-I1"), height: 700);
        var scroll = view.GetVisualDescendants().OfType<JournalDiaryView>().Single().FindControl<ScrollViewer>("Scroll")!;
        FocusComposer(view);

        for (var i = 1; i <= 40; i++)
        {
            window.KeyTextInput($"riga {i}");
            PressEnter(window);
        }
        window.KeyTextInput("ultima");
        PrivateDbHost.Pump();

        var editor = Composer(view);
        editor.Bounds.Height.Should().BeGreaterThan(scroll.Viewport.Height, "l'editor e' piu' alto della colonna");
        scroll.Offset.Y.Should().BeGreaterThan(0, "la colonna segue il cursore");
        var caret = editor.CaretBounds()!.Value;
        var top = editor.TranslatePoint(caret.TopLeft, scroll)!.Value.Y;
        top.Should().BeGreaterThanOrEqualTo(0);
        (top + caret.Height).Should().BeLessThanOrEqualTo(scroll.Viewport.Height, "il cursore e' dentro la parte visibile");
    }

    [AvaloniaFact]
    public async Task Choosing_another_day_with_an_open_edit_asks_and_no_keeps_day_and_edit()
    {
        var yesterday = Today.AddDays(-1);
        var id = await CreateAsync(HabitType.Journal, "Sera-I2");
        await SeedAsync(id, yesterday, "Ieri");
        await SeedAsync(id, Today, "Oggi");
        var (vm, view, window) = await OpenAsync(id);
        var qe = vm.QuickEntryForDiario!;
        await WaitAsync(() => Rendered(view).Count == 1);
        Click(EditButton(view));
        await WaitAsync(() => qe.EditingEntry is not null);
        Composer(view).ReplaceText("Oggi, corretto", 0);
        PrivateDbHost.Pump();
        vm.IsCronologiaOpen = true;
        PrivateDbHost.Pump();

        Answer.Yes = false;
        ClickAt(window, DayCell(view, yesterday));
        await WaitAsync(() => Answer.Asked == 1);

        vm.Calendar.SelectedDate.Should().Be(Today, "No: il calendario torna al giorno mostrato");
        DayCell(view, Today).Classes.Should().Contain("selected");
        qe.Day.Should().Be(Today);
        qe.EditingEntry.Should().NotBeNull();
        Text(Composer(view)).Should().Be("Oggi, corretto", "la modifica resta");
        Text(Rendered(view).Single()).Should().Be("Oggi");

        Answer.Yes = true;
        ClickAt(window, DayCell(view, yesterday));
        await WaitAsync(() => Rendered(view).Count == 1 && Rendered(view)[0].Markdown == "Ieri");
        Answer.Asked.Should().Be(2);
        qe.EditingEntry.Should().BeNull("Si': la modifica e' scartata");
        vm.Calendar.SelectedDate.Should().Be(yesterday);
    }

    [AvaloniaFact]
    public async Task Text_from_the_binding_does_not_open_the_menu_and_focus_loss_closes_it()
    {
        await EnsureTagAsync("lago");
        var id = await CreateAsync(HabitType.Journal, "Sera-M2");
        await SeedAsync(id, Today, "Cena al #lago");
        var (vm, view, window) = await OpenAsync(id);
        var qe = vm.QuickEntryForDiario!;
        var menu = Find<Popup>(view, "TagMenu");
        await WaitAsync(() => Rendered(view).Count == 1);

        var edit = EditButton(view);
        edit.Focus();
        Click(edit);
        await WaitAsync(() => qe.EditingEntry is not null);
        Text(Composer(view)).Should().Be("Cena al #lago");
        menu.IsOpen.Should().BeFalse("il testo e' arrivato dal binding, non si sta scrivendo");

        Click(Find<Button>(view, "CancelEditButton"));
        menu.IsOpen.Should().BeFalse();

        FocusComposer(view);
        window.KeyTextInput("#la");
        PrivateDbHost.Pump();
        menu.IsOpen.Should().BeTrue();
        Find<TextBox>(view, "TagBox").Focus();
        PrivateDbHost.Pump();
        menu.IsOpen.Should().BeFalse("fuori fuoco il menu non copre la riga Tag");
    }

    [AvaloniaFact]
    public async Task A_failing_file_chooser_says_so()
    {
        var (vm, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Sera-M3"));
        var gate = new TaskCompletionSource<string[]>();
        Dialog.Pending = gate.Task;
        Click(Find<Button>(view, "InsertImageButton"));
        gate.SetException(new InvalidOperationException("portale non disponibile"));
        await WaitAsync(() => vm.QuickEntryForDiario!.ErrorMessage is not null);

        Find<TextBlock>(view, "ErrorText").Text.Should().Be(Strings.Diario_Editor_InsertImageFailed);
        Find<TextBlock>(view, "ErrorText").IsEffectivelyVisible.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task Text_typed_while_the_image_imports_is_kept_and_the_surface_waits()
    {
        var id = await CreateAsync(HabitType.Journal, "Sera-M4");
        await SeedAsync(id, Today, "Una voce");
        var (vm, view, window) = await OpenAsync(id);
        var qe = vm.QuickEntryForDiario!;
        await WaitAsync(() => Rendered(view).Count == 1);
        FocusComposer(view);
        window.KeyTextInput("Pranzo");
        PrivateDbHost.Pump();

        var gate = new TaskCompletionSource<string[]>();
        Dialog.Pending = gate.Task;
        Click(Find<Button>(view, "InsertImageButton"));
        qe.IsInserting.Should().BeTrue();
        EditButton(view).IsEffectivelyEnabled.Should().BeFalse("Modifica aspetta la fine dell'inserimento");

        FocusComposer(view);
        window.KeyTextInput(" al mare");
        PrivateDbHost.Pump();

        var png = JournalFixtures.WritePng();
        try
        {
            gate.SetResult([png]);
            await WaitAsync(() => qe.PendingPhotos.Count == 1 && qe.Draft.Contains("photo:", StringComparison.Ordinal));
        }
        finally { File.Delete(png); }

        var reference = PhotoRefs.Format(qe.PendingPhotos[0].Id, qe.PendingPhotos[0].Filename);
        Text(Composer(view)).Should().Be("Pranzo al mare\n" + reference + "\n", "il testo scritto durante l'import resta, il riferimento va al cursore");
        qe.EditingEntry.Should().BeNull();
        EditButton(view).IsEffectivelyEnabled.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task Undo_after_Modifica_does_not_carry_the_text_typed_in_the_new_entry()
    {
        // Stesso testo nelle due superfici: PushToEditor esce subito e l'annulla della voce nuova restava vivo (M3).
        var id = await CreateAsync(HabitType.Journal, "Sera-undo-surface");
        await SeedAsync(id, Today, "abc");
        var (vm, view, window) = await OpenAsync(id);
        FocusComposer(view);
        window.KeyTextInput("abc");
        PrivateDbHost.Pump();
        Text(Composer(view)).Should().Be("abc");

        Click(EditButton(view));
        vm.QuickEntryForDiario!.EditingEntry.Should().NotBeNull();
        Text(Composer(view)).Should().Be("abc");
        FocusComposer(view);
        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        PrivateDbHost.Pump();

        Text(Composer(view)).Should().Be("abc", "Ctrl+Z non riporta il testo dell'altra superficie");
        vm.QuickEntryForDiario.EditingEntry!.EditDraft.Should().Be("abc");
    }

    [AvaloniaFact]
    public async Task Modifica_shows_a_refless_photo_under_the_editor_with_a_cross_and_Salva_removes_it()
    {
        var id = await CreateAsync(HabitType.Journal, "Sera-F2d25");
        using (var scope = Host.Provider.CreateScope())
        {
            var png = JournalFixtures.WritePng(160, 120);
            try
            {
                var photo = await Svc<IPhotoImporter>(scope).ImportAsync(png, CancellationToken.None);
                await Svc<IDiaryService>(scope).SaveEntryAsync(id, Today, new DiaryEntryDraft("senza riferimento", [], [photo]));
            }
            finally { File.Delete(png); }
        }
        var (vm, view, _) = await OpenAsync(id);
        var qe = vm.QuickEntryForDiario!;
        await WaitAsync(() => qe.Entries.Count == 1 && qe.Entries[0].Photos.Count == 1);
        var strip = Find<ItemsControl>(view, "EditUnreferencedPhotos");
        strip.GetVisualDescendants().OfType<Button>().Should().BeEmpty("fuori dalla modifica non c'e' niente");

        Click(EditButton(view));
        await WaitAsync(() => strip.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "RemoveEditPhotoButton" && b.IsEffectivelyVisible));
        strip.GetVisualDescendants().OfType<Image>().Should().ContainSingle(i => i.Source != null, "la miniatura della foto senza riferimento");
        Find<ItemsControl>(view, "UnreferencedPhotos").GetVisualDescendants().OfType<Image>()
            .Where(i => i.IsEffectivelyVisible).Should().BeEmpty("la striscia di sola lettura della riga in modifica resta nascosta (niente doppione)");

        Click(strip.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "RemoveEditPhotoButton"));
        Click(Find<Button>(view, "SaveButton"));
        await WaitAsync(() => !qe.Entries[0].IsEditing && qe.Entries[0].Photos.Count == 0);

        Find<ItemsControl>(view, "UnreferencedPhotos").GetVisualDescendants().OfType<Image>().Should().BeEmpty("la foto e' sparita dalla voce");
        strip.GetVisualDescendants().OfType<Button>().Should().BeEmpty();
    }
}
