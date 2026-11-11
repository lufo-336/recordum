using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Notes;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>La colonna Note portata da WPF (direzione visiva, onda 2a, Task 9): elenco, editor, elimina sul posto, allegati
/// e visore, versioni; le quattro colonne; il testo scritto e non salvato non si perde mai. DB privato, oggi 28/10/2026.</summary>
public class NotesColumnTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-notes",
        s => s.AddSingleton<Recordum.Presentation.Services.IConfirmationDialog>(new Yes()));

    private sealed class Yes : Recordum.Presentation.Services.IConfirmationDialog
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 28);

    private sealed record Opened(IServiceScope Scope, HabitScreenViewModel Vm, HabitScreenView View, Window Window, Guid Id)
    {
        public NotesColumnView Column => View.GetVisualDescendants().OfType<NotesColumnView>().Single();
        public INoteService Notes => Scope.ServiceProvider.GetRequiredService<INoteService>();
    }

    private static async Task<Opened> OpenAsync(Func<IServiceProvider, Guid, Task>? seed = null, HabitType type = HabitType.Action,
        bool openNotes = true, double width = 1000)
    {
        Host.Clock.Now = Now;
        var scope = Host.Provider.CreateScope();
        await HabitScreenColumnsTests.ResetColumnStateAsync(scope);
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var id = await sp.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = $"Note {Guid.NewGuid():N}", Color = "#2F8FB0",
        });
        if (seed is not null) await seed(sp, id);
        var vm = sp.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        vm.IsNoteOpen = openNotes;
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = width, Height = 900, Content = view };
        w.Show();
        Pump();
        return new Opened(scope, vm, view, w, id);
    }

    private static async Task<Guid> SeedNote(IServiceProvider sp, Guid habit, string body, DateOnly? day = null)
    {
        var note = new Note { HabitNodeId = habit, BodyMd = body, TargetDate = day, UpdatedAtMs = Host.Clock.UtcNowMs };
        await sp.GetRequiredService<INoteService>().CreateAsync(note);
        return note.Id;
    }

    private static void Pump() => PrivateDbHost.Pump();

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 150 && !done(); i++) { await Task.Delay(20); Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static T Named<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    private static List<ListBoxItem> Rows(Opened o) =>
        [.. Named<ListBox>(o.Column, "NotesList").GetVisualDescendants().OfType<ListBoxItem>()];

    private static async Task<ListBoxItem> RowAsync(Opened o, string summary)
    {
        await UntilAsync(() => Rows(o).Any(r => ((NoteRowViewModel)r.DataContext!).Summary == summary));
        return Rows(o).Single(r => ((NoteRowViewModel)r.DataContext!).Summary == summary);
    }

    private static DiaryEditor Editor(Opened o) => Named<DiaryEditor>(o.Column, "NoteEditor");

    private static async Task<string> BodyAsync(Opened o, Guid noteId) =>
        (await o.Notes.ListByHabitAsync(o.Id)).Single(n => n.Id == noteId).BodyMd;

    [AvaloniaFact]
    public async Task Note_button_opens_the_notes_column()
    {
        var o = await OpenAsync(openNotes: false);
        o.View.GetVisualDescendants().OfType<NotesColumnView>().Should().BeEmpty("colonna chiusa: niente costruito");

        Click(o.Window, Named<ToggleButton>(o.View, "ChipNote"));
        await UntilAsync(() => o.View.GetVisualDescendants().OfType<NotesColumnView>().Any());
        Named<Border>(o.View, "NoteColumn").IsVisible.Should().BeTrue();

        Click(o.Window, Named<Button>(o.View, "NoteCloseButton"));
        o.Vm.IsNoteOpen.Should().BeFalse();
        o.View.GetVisualDescendants().OfType<NotesColumnView>().Should().BeEmpty("chiusa, il corpo si butta");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Notes_column_lists_and_edits()
    {
        Guid noteId = default;
        var o = await OpenAsync(async (sp, id) => noteId = await SeedNote(sp, id, "prima stesura"));
        var row = await RowAsync(o, "prima stesura");
        Click(o.Window, row);
        await UntilAsync(() => Editor(o).Markdown == "prima stesura");
        // Visto nei PNG: la riga scelta era un blocco pieno nel colore; è la tinta leggera (alfa < 1).
        var fill = (Avalonia.Media.ISolidColorBrush)row.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>()
            .First(c => c.Name == "PART_ContentPresenter").Background!;
        (fill.Opacity * fill.Color.A / 255.0).Should().BeLessThan(0.5);
        // Visto nei PNG: "+ + Nuova nota". L'icona è il solo segno, la parola non porta glifi.
        Named<Button>(o.Column, "NewButton").GetVisualDescendants().OfType<TextBlock>()
            .Should().OnlyContain(t => t.Text != null && !t.Text.StartsWith('+'));

        Editor(o).Markdown = "seconda stesura";
        Pump();
        var save = Named<Button>(o.Column, "SaveButton");
        save.Classes.Should().Contain("primary");
        Click(o.Window, save);
        await UntilAsync(() => BodyAsync(o, noteId).Result == "seconda stesura");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Delete_asks_in_place()
    {
        Guid noteId = default;
        var o = await OpenAsync(async (sp, id) => noteId = await SeedNote(sp, id, "da togliere"));
        Click(o.Window, await RowAsync(o, "da togliere"));

        Click(o.Window, Named<Button>(o.Column, "DeleteButton"));
        Named<Control>(o.Column, "ConfirmDeleteRow").IsVisible.Should().BeTrue("la conferma è sul posto");
        (await o.Notes.ListByHabitAsync(o.Id)).Should().ContainSingle("non si elimina prima della conferma");

        Click(o.Window, Named<Button>(o.Column, "ConfirmDeleteButton"));
        await UntilAsync(() => o.Notes.ListByHabitAsync(o.Id).AsTask().Result.Count == 0);
        Rows(o).Should().BeEmpty();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Attachments_show_thumbnails_and_open_the_viewer()
    {
        var o = await OpenAsync(async (sp, id) =>
        {
            var noteId = await SeedNote(sp, id, "con le foto");
            foreach (var size in new[] { (160, 120), (120, 160) })
            {
                var png = JournalFixtures.WritePng(size.Item1, size.Item2);
                try
                {
                    var photo = await sp.GetRequiredService<IPhotoImporter>().ImportAsync(png, CancellationToken.None);
                    await sp.GetRequiredService<IAttachmentService>().AddAsync(noteId, photo, CancellationToken.None);
                }
                finally { File.Delete(png); }
            }
        });
        Click(o.Window, await RowAsync(o, "con le foto"));
        var gallery = Named<ItemsControl>(o.Column, "Attachments");
        await UntilAsync(() => gallery.GetVisualDescendants().OfType<Image>().Count(i => i.Source is not null) == 2);

        var viewer = Named<Control>(o.Column, "Lightbox");
        viewer.IsVisible.Should().BeFalse();
        Click(o.Window, gallery.GetVisualDescendants().OfType<Button>().First(b => b.Name == "ThumbnailButton"));
        await UntilAsync(() => viewer.IsVisible && Named<Image>(o.Column, "LightboxImage").Source is not null);
        var first = o.Vm.Notes.LightboxAttachment;

        Click(o.Window, Named<Button>(o.Column, "LightboxNextButton"));
        o.Vm.Notes.LightboxAttachment.Should().NotBeSameAs(first, "› passa alla foto dopo");

        Click(o.Window, Named<Button>(o.Column, "LightboxCloseButton"));
        viewer.IsVisible.Should().BeFalse("× chiude il visore (Esc: ShellIntegrationTests, lo gestisce MainWindow)");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Version_history_restores()
    {
        Guid noteId = default;
        var o = await OpenAsync(async (sp, id) =>
        {
            noteId = await SeedNote(sp, id, "versione uno");
            Host.Clock.Now = Now.AddMinutes(5);
            await sp.GetRequiredService<INoteService>().UpdateAsync(noteId, n => n.BodyMd = "versione due");
            Host.Clock.Now = Now;
        });
        Click(o.Window, await RowAsync(o, "versione due"));
        Click(o.Window, Named<Button>(o.Column, "HistoryButton"));
        var versions = Named<ListBox>(o.Column, "VersionsList");
        await UntilAsync(() => versions.ItemCount == 2);

        versions.SelectedIndex = 1;   // la più vecchia
        Pump();
        var preview = Named<DiaryEditor>(o.Column, "VersionPreview");
        preview.IsReadOnly.Should().BeTrue();
        preview.Markdown.Should().Be("versione uno");

        Click(o.Window, Named<Button>(o.Column, "RestoreButton"));
        await UntilAsync(() => BodyAsync(o, noteId).Result == "versione uno");
        await UntilAsync(() => !o.Vm.Notes.IsShowingHistory && Editor(o).Markdown == "versione uno");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Four_columns_fit_with_scroll()
    {
        var o = await OpenAsync(type: HabitType.Journal);
        o.Vm.IsDiarioOpen = true;
        o.Vm.IsCronologiaOpen = true;
        o.Vm.IsConfiguraOpen = true;
        await UntilAsync(() => o.View.GetVisualDescendants().OfType<NotesColumnView>().Any());
        Pump();

        foreach (var name in new[] { "DiarioColumn", "CronologiaColumn", "NoteColumn", "ConfiguraColumn" })
        {
            var col = Named<Border>(o.View, name);
            col.IsVisible.Should().BeTrue(name);
            col.Bounds.Width.Should().BeGreaterThanOrEqualTo(320, name);
        }
        var x = new[] { "DiarioColumn", "CronologiaColumn", "NoteColumn", "ConfiguraColumn" }
            .Select(n => Named<Border>(o.View, n).Bounds.X).ToList();
        x.Should().BeInAscendingOrder("Diario · Cronologia · Note · Configura");
        var scroll = Named<ScrollViewer>(o.View, "ColumnsScroll");
        scroll.Extent.Width.Should().BeGreaterThan(scroll.Viewport.Width, "quattro colonne da 320 non stanno in 1000 px: si scorre");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task A_note_written_in_the_column_shows_in_the_day_block()
    {
        var o = await OpenAsync(async (sp, id) => await SeedNote(sp, id, "mattina", Today));
        o.Vm.IsCronologiaOpen = true;
        var dayNote = await UntilFoundAsync<TextBox>(o, "DayNoteBox");
        await UntilAsync(() => dayNote.Text == "mattina");

        Click(o.Window, await RowAsync(o, "mattina"));
        await UntilAsync(() => Editor(o).Markdown == "mattina");
        Editor(o).Markdown = "sera";
        Pump();
        Click(o.Window, Named<Button>(o.Column, "SaveButton"));
        await UntilAsync(() => dayNote.Text == "sera");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Changing_note_saves_the_draft()
    {
        Guid first = default;
        var o = await OpenAsync(async (sp, id) =>
        {
            first = await SeedNote(sp, id, "uno");
            Host.Clock.Now = Now.AddMinutes(1);
            await SeedNote(sp, id, "due");
            Host.Clock.Now = Now;
        });
        Click(o.Window, await RowAsync(o, "uno"));
        await UntilAsync(() => Editor(o).Markdown == "uno");
        Editor(o).Markdown = "uno, riscritto";
        Pump();

        Click(o.Window, await RowAsync(o, "due"));
        await UntilAsync(() => BodyAsync(o, first).Result == "uno, riscritto");
        Pump();
        o.Vm.Notes.SelectedNote!.Summary.Should().Be("due", "la nota scelta resta quella cliccata");
        Editor(o).Markdown.Should().Be("due");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task A_note_event_does_not_wipe_the_draft()
    {
        var o = await OpenAsync(async (sp, id) =>
        {
            await SeedNote(sp, id, "in corso");
            Host.Clock.Now = Now.AddMinutes(1);
            await SeedNote(sp, id, "del giorno", Today);
            Host.Clock.Now = Now;
        });
        Click(o.Window, await RowAsync(o, "in corso"));
        await UntilAsync(() => Editor(o).Markdown == "in corso");
        Editor(o).Markdown = "in corso, non salvata";
        Pump();

        // Un'altra nota della stessa abitudine si salva da fuori (la nota del giorno): la colonna si rilegge.
        o.Vm.DayNote.Text = "del giorno, cambiata";
        await o.Vm.DayNote.SaveCommand.ExecuteAsync(null);
        await RowAsync(o, "del giorno, cambiata");

        Editor(o).Markdown.Should().Be("in corso, non salvata", "il testo non salvato resta nell'editor");
        o.Vm.Notes.SelectedNote!.Summary.Should().Be("in corso");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task A_note_event_keeps_the_viewer_open()
    {
        // Review dell'onda 2a: la rilettura per un evento riassegnava la nota scelta (riga nuova, stesso id) e chiudeva il
        // visore; salvare la nota del giorno con il visore aperto lo chiudeva.
        var o = await OpenAsync(async (sp, id) =>
        {
            var noteId = await SeedNote(sp, id, "con la foto");
            var png = JournalFixtures.WritePng(160, 120);
            try
            {
                var photo = await sp.GetRequiredService<IPhotoImporter>().ImportAsync(png, CancellationToken.None);
                await sp.GetRequiredService<IAttachmentService>().AddAsync(noteId, photo, CancellationToken.None);
            }
            finally { File.Delete(png); }
        });
        Click(o.Window, await RowAsync(o, "con la foto"));
        await UntilAsync(() => o.Vm.Notes.Attachments.Count == 1);
        o.Vm.Notes.OpenLightboxCommand.Execute(o.Vm.Notes.Attachments[0]);
        Pump();

        o.Vm.DayNote.Text = "una nota del giorno";
        await o.Vm.DayNote.SaveCommand.ExecuteAsync(null);
        await RowAsync(o, "una nota del giorno");

        o.Vm.Notes.IsLightboxOpen.Should().BeTrue("un salvataggio altrove non chiude il visore");
        o.Vm.Notes.Attachments.Should().ContainSingle();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Closing_the_column_saves_the_draft()
    {
        Guid noteId = default;
        var o = await OpenAsync(async (sp, id) => noteId = await SeedNote(sp, id, "bozza"));
        Click(o.Window, await RowAsync(o, "bozza"));
        await UntilAsync(() => Editor(o).Markdown == "bozza");
        Editor(o).Markdown = "bozza finita";
        Pump();

        Click(o.Window, Named<Button>(o.View, "NoteCloseButton"));
        await UntilAsync(() => BodyAsync(o, noteId).Result == "bozza finita");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task HistoryButton_reads_Versioni_in_Italian_and_Versions_in_English()
    {
        var prevStrings = Recordum.Presentation.Resources.Strings.Culture;
        var prevCurrent = System.Globalization.CultureInfo.CurrentCulture;
        var prevUi = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            var it = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
            Recordum.Presentation.Resources.Strings.Culture = it;
            System.Globalization.CultureInfo.CurrentCulture = it;
            System.Globalization.CultureInfo.CurrentUICulture = it;
            var oIt = await OpenAsync();
            Named<Button>(oIt.Column, "HistoryButton").Content.Should().Be("Versioni");
            oIt.Window.Close();

            var en = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            Recordum.Presentation.Resources.Strings.Culture = en;
            System.Globalization.CultureInfo.CurrentCulture = en;
            System.Globalization.CultureInfo.CurrentUICulture = en;
            var oEn = await OpenAsync();
            Named<Button>(oEn.Column, "HistoryButton").Content.Should().Be("Versions");
            oEn.Window.Close();
        }
        finally
        {
            Recordum.Presentation.Resources.Strings.Culture = prevStrings;
            System.Globalization.CultureInfo.CurrentCulture = prevCurrent;
            System.Globalization.CultureInfo.CurrentUICulture = prevUi;
        }
    }

    private static async Task<T> UntilFoundAsync<T>(Opened o, string name) where T : Control
    {
        await UntilAsync(() => o.View.GetVisualDescendants().OfType<T>().Any(c => c.Name == name));
        return Named<T>(o.View, name);
    }
}
