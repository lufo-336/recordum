using System.Text.Json;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Services;
using Recordum.Core.Text;
using Recordum.Core.Time;
using Recordum.Plugins.Contracts.Events;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>Regressioni della review finale di F2d-2: bozza in modifica riletta, Cronologia aggiornata,
/// colonna Diario allineata alla Cronologia, riferimenti a foto sconosciuti in nota veloce.</summary>
public class FinalReviewFixTests
{
    private sealed record Rig(
        HabitScreenViewModel Sut, FakeNoteService Notes, FakeDiaryService Diary, FakeAppSettingService Settings,
        FakeEventBus Bus, FakeCompletionService Completions, HabitNode Node);

    private static async Task<Rig> BuildAsync(JournalPrimaryGesture gesture = JournalPrimaryGesture.Diary)
    {
        var nodes = new FakeHabitNodeService();
        var notes = new FakeNoteService();
        var uiPrefs = new FakeHabitUiPrefsService();
        var metrics = new FakeMetricDefinitionService();
        var configs = new FakeHabitConfigService();
        var settings = new FakeAppSettingService();
        var completions = new FakeCompletionService();
        var diary = new FakeDiaryService(notes);
        var bus = new FakeEventBus();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        var editor = new Recordum.Presentation.ViewModels.HabitEditor.HabitEditorViewModel(
            nodes, configs, new FakeGoalService(), metrics, new FakeNotificationService(), new FakeTagService(),
            settings, new FakeReferenceData(), new Recordum.Presentation.Services.RecentColorStore(settings),
            new Recordum.Presentation.Services.FavoriteColorStore(settings), new FakeScreenColorPicker(), uiPrefs);
        var sut = new HabitScreenViewModel(
            nodes, completions, new FakeActiveTimerService(), metrics, new FakeReferenceData(), configs,
            notes, new FakeGoalService(), new IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            new FakeAbstinenceService(), clock, bus, new FakeEditorAvailability(false), settings, editor,
            new FakeHabitTreeQueryService(), diary, new FakeConfirmationDialog(),
            new FakeAttachmentService(), new FakePhotoImporter(), new FakeTagService(), new FakeOpenFileDialog(), uiPrefs, new FakeResetTimeResolver());
        var node = HabitScreenViewModelTests.AddJournal(nodes, "J");
        await uiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = gesture });
        return new Rig(sut, notes, diary, settings, bus, completions, node);
    }

    /// <summary>Una voce del diario di oggi, con la sua completion (la Cronologia legge da li').</summary>
    private static Note SeedEntry(Rig r, string body)
    {
        var cid = Guid.NewGuid();
        var at = new DateTime(2026, 5, 21, 10, 0, 0, DateTimeKind.Utc);
        r.Completions.Store[cid] = new Completion
        {
            Id = cid, HabitNodeId = r.Node.Id, CompletedAt = at,
            CompletedAtMs = new DateTimeOffset(at).ToUnixTimeMilliseconds(),
        };
        var note = new Note
        {
            Id = Guid.NewGuid(), HabitNodeId = r.Node.Id, Category = NoteCategory.Diario, BodyMd = body,
            CompletionId = cid, TargetDate = new DateOnly(2026, 5, 21), CreatedAt = at,
        };
        r.Notes.Store[note.Id] = note;
        r.Diary.Notes[note.Id] = note;
        r.Diary.Completions[cid] = r.Completions.Store[cid];
        return note;
    }

    private static async Task SettleAsync()
    {
        for (var i = 0; i < 20; i++) await Task.Yield();
        await Task.Delay(100);
    }

    [Fact]
    public async Task Crash_recovered_edit_survives_the_habit_screen_load()
    {
        var r = await BuildAsync();
        var note = SeedEntry(r, "saved");
        var key = "diary.draft." + r.Node.Id.ToString("N");
        r.Settings.Store[key] = JsonSerializer.Serialize(new
        { text = "bozza", tags = Array.Empty<string>(), editNoteId = note.Id, editText = "edited after crash", editTags = Array.Empty<string>() });

        await r.Sut.LoadAsync(r.Node.Id);
        await SettleAsync();

        var j = r.Sut.JournalQuickEntry!;
        j.ActiveText = j.ActiveText + "!";
        await j.FlushDraftAsync();
        r.Settings.Store[key].Should().Contain(note.Id.ToString(), "la prossima battuta tiene editNoteId");
        j.EditingEntry.Should().NotBeNull();
        j.ActiveText.Should().Be("edited after crash!");
    }

    [Fact]
    public async Task Same_day_SetDay_keeps_an_open_edit()
    {
        var r = await BuildAsync();
        SeedEntry(r, "voce");
        await r.Sut.LoadAsync(r.Node.Id);
        await SettleAsync();
        var j = r.Sut.JournalQuickEntry!;
        await j.BeginEditCommand.ExecuteAsync(j.Entries.Single());
        j.ActiveText = "half-written edit";

        j.SetDay(j.Day); // clic sul giorno gia' selezionato
        await SettleAsync();

        j.EditingEntry.Should().NotBeNull();
        j.ActiveText.Should().Be("half-written edit");
    }

    [Fact]
    public async Task Editing_a_diary_entry_refreshes_the_Cronologia_row()
    {
        var r = await BuildAsync();
        var note = SeedEntry(r, "old");
        await r.Sut.LoadAsync(r.Node.Id);
        await SettleAsync();
        r.Sut.CompletionList.Rows.Single().Notes.Should().Be("old");

        note.BodyMd = "new body";
        r.Diary.Tags[note.Id] = [new EntryTag("sera", NoteTagSource.Property)];
        r.Bus.Publish(new NoteUpdatedPayload(note.Id, r.Node.Id));
        await SettleAsync();

        var row = r.Sut.CompletionList.Rows.Single();
        row.Notes.Should().Be("new body");
        row.TagChips.Select(c => c.Name).Should().Contain("sera");
    }

    [Fact]
    public async Task Deleting_an_entry_from_the_Cronologia_removes_it_from_the_Diario_column()
    {
        var r = await BuildAsync();
        var note = SeedEntry(r, "voce");
        await r.Sut.LoadAsync(r.Node.Id);
        await SettleAsync();
        r.Sut.JournalQuickEntry!.Entries.Should().ContainSingle();

        r.Notes.Store.Remove(note.Id);
        r.Completions.Store.Remove(note.CompletionId!.Value);
        r.Bus.Publish(new CompletionDeletedPayload(note.CompletionId!.Value, r.Node.Id, DateTime.UtcNow));
        await SettleAsync();

        r.Sut.JournalQuickEntry.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_the_entry_under_an_open_edit_moves_the_edit_text_to_the_draft()
    {
        var r = await BuildAsync();
        var note = SeedEntry(r, "voce");
        await r.Sut.LoadAsync(r.Node.Id);
        await SettleAsync();
        var j = r.Sut.JournalQuickEntry!;
        await j.BeginEditCommand.ExecuteAsync(j.Entries.Single());
        j.ActiveText = "testo non salvato";

        r.Notes.Store.Remove(note.Id);
        r.Completions.Store.Remove(note.CompletionId!.Value);
        r.Bus.Publish(new CompletionDeletedPayload(note.CompletionId!.Value, r.Node.Id, DateTime.UtcNow));
        await SettleAsync();

        j.Entries.Should().BeEmpty();
        j.EditingEntry.Should().BeNull();
        j.Draft.Should().Be("testo non salvato");
    }

    [Fact]
    public async Task A_completion_event_for_another_entry_keeps_the_open_edit()
    {
        var r = await BuildAsync();
        var note = SeedEntry(r, "voce");
        await r.Sut.LoadAsync(r.Node.Id);
        await SettleAsync();
        var j = r.Sut.JournalQuickEntry!;
        await j.BeginEditCommand.ExecuteAsync(j.Entries.Single());
        j.ActiveText = "in corso";

        r.Bus.Publish(new CompletionUpdatedPayload(note.CompletionId!.Value, r.Node.Id, DateTime.UtcNow));
        await SettleAsync();

        j.EditingEntry.Should().NotBeNull();
        j.ActiveText.Should().Be("in corso");
    }

    [Fact]
    public async Task QuickNote_save_strips_unknown_photo_references_but_keeps_refless_photos()
    {
        var r = await BuildAsync(JournalPrimaryGesture.QuickNote);
        await r.Sut.LoadAsync(r.Node.Id);
        var j = r.Sut.JournalQuickEntry!;
        j.IsDiaryMode.Should().BeFalse();
        var known = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        j.PendingPhotos.Add(new ImportedPhoto(known, "a.jpg", "image/jpeg", [1], [2]));
        j.Draft = $"cena\n{PhotoRefs.Format(unknown, "x.jpg")}\n{PhotoRefs.Format(known, "a.jpg")}";

        await j.SaveCommand.ExecuteAsync(null);

        var draft = r.Diary.Drafts.Should().ContainSingle().Subject;
        draft.Text.Should().NotContain(unknown.ToString("N")).And.Contain(known.ToString("N"));
        draft.Photos.Select(p => p.Id).Should().Equal(known);

        // senza riferimento la foto resta allegata (comportamento di F2d-1)
        var bare = Guid.NewGuid();
        j.PendingPhotos.Add(new ImportedPhoto(bare, "b.jpg", "image/jpeg", [1], [2]));
        j.Draft = "solo testo";
        await j.SaveCommand.ExecuteAsync(null);
        r.Diary.Drafts.Last().Photos.Select(p => p.Id).Should().Equal(bare);
    }
}
