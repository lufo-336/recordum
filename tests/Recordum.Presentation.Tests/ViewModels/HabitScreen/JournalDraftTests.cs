using System.Text.Json;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>La bozza del diario (F2d-2, Task 4): ogni modifica la scrive in app_setting, l'apertura la rilegge senza riscriverla.</summary>
public class JournalDraftTests
{
    private static readonly HabitNode Node = new()
    {
        Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "Diary",
    };

    private static string Key => "diary.draft." + Node.Id.ToString("N");

    /// <summary>Un'impostazione che tiene ferme le scritture finche' il test non le sblocca.</summary>
    private sealed class GatedSettings : IAppSettingService
    {
        public List<string> Written { get; } = [];
        public List<(string Key, string Value)> WrittenWithKey { get; } = [];
        public int InFlight { get; private set; }
        public int MaxInFlight { get; private set; }
        private readonly List<TaskCompletionSource> _gates = [];

        // Senza contesto di sincronizzazione le continuazioni del VM girano sul pool: la lista va letta sotto lock.
        public TaskCompletionSource[] Gates { get { lock (_gates) return [.. _gates]; } }

        public ValueTask<string?> GetAsync(string key, CancellationToken ct = default) => new((string?)null);

        public async ValueTask SetAsync(string key, string value, CancellationToken ct = default)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gates) _gates.Add(gate);
            InFlight++;
            MaxInFlight = Math.Max(MaxInFlight, InFlight);
            await gate.Task;
            InFlight--;
            Written.Add(value);
            WrittenWithKey.Add((key, value));
        }

        public ValueTask<T?> GetEnumAsync<T>(string key, CancellationToken ct = default) where T : struct, Enum => new((T?)null);
        public ValueTask SetEnumAsync<T>(string key, T value, CancellationToken ct = default) where T : struct, Enum => ValueTask.CompletedTask;
    }

    private static (JournalQuickEntryViewModel Sut, FakeNoteService Notes, FakeDiaryService Diary) Build(IAppSettingService settings)
    {
        var notes = new FakeNoteService();
        var diary = new FakeDiaryService(notes);
        var sut = new JournalQuickEntryViewModel(diary, notes, new FakeAttachmentService(), new FakePhotoImporter(),
            new FakeTagService(), new FakeOpenFileDialog(), new FakeConfirmationDialog(), new FakeClock(), settings);
        return (sut, notes, diary);
    }

    private static JournalQuickEntryViewModel BuildOn(FakeNoteService notes, FakeDiaryService diary, IAppSettingService settings) =>
        new(diary, notes, new FakeAttachmentService(), new FakePhotoImporter(),
            new FakeTagService(), new FakeOpenFileDialog(), new FakeConfirmationDialog(), new FakeClock(), settings);

    [Fact]
    public async Task Text_and_tag_come_back_in_a_new_composer_on_the_same_settings()
    {
        var settings = new FakeAppSettingService();
        var (sut, notes, diary) = Build(settings);
        await sut.LoadAsync(Node, DayBoundary.Utc, true);
        sut.Draft = "Cena con Anna";
        sut.PropertyTags.Tags.Add("amici");
        await sut.FlushDraftAsync();

        var again = BuildOn(notes, diary, settings);
        await again.LoadAsync(Node, DayBoundary.Utc, true);

        again.Draft.Should().Be("Cena con Anna");
        again.PropertyTags.Tags.Should().Equal("amici");
    }

    [Fact]
    public async Task An_open_edit_of_a_day_entry_comes_back_and_a_vanished_entry_drops_only_the_edit_part()
    {
        var settings = new FakeAppSettingService();
        var (sut, notes, diary) = Build(settings);
        await sut.LoadAsync(Node, DayBoundary.Utc, true);
        sut.Draft = "voce nuova";
        await sut.SaveCommand.ExecuteAsync(null);
        sut.Draft = "bozza nuova";
        await sut.BeginEditCommand.ExecuteAsync(sut.Entries.Single());
        sut.ActiveText = "voce modificata";
        await sut.FlushDraftAsync();

        var again = BuildOn(notes, diary, settings);
        await again.LoadAsync(Node, DayBoundary.Utc, true);
        again.EditingEntry.Should().NotBeNull();
        again.ActiveText.Should().Be("voce modificata");
        again.Draft.Should().Be("bozza nuova");

        // La voce non c'e' piu' fra quelle del giorno: la parte di modifica si scarta, la bozza nuova resta.
        var editedId = sut.Entries.Single().Id;
        var json = JsonDocument.Parse(settings.Store[Key]).RootElement;
        json.GetProperty("editNoteId").GetGuid().Should().Be(editedId);
        settings.Store[Key] = json.GetRawText().Replace(editedId.ToString(), Guid.NewGuid().ToString(), StringComparison.Ordinal);
        var other = BuildOn(notes, diary, settings);
        await other.LoadAsync(Node, DayBoundary.Utc, true);
        other.EditingEntry.Should().BeNull();
        other.Draft.Should().Be("bozza nuova");
    }

    [Fact]
    public async Task Ten_edits_while_the_first_write_is_blocked_keep_one_write_in_flight_and_write_the_last_text_last()
    {
        var settings = new GatedSettings();
        var (sut, _, _) = Build(settings);
        await sut.LoadAsync(Node, DayBoundary.Utc, true);

        for (var i = 1; i <= 10; i++) sut.Draft = $"testo {i}";
        settings.Gates.Should().HaveCount(1);

        var flush = sut.FlushDraftAsync();
        while (!flush.IsCompleted)
        {
            settings.Gates.Last().TrySetResult();
            await Task.Yield();
        }
        await flush;

        settings.MaxInFlight.Should().Be(1);
        settings.Written.Should().HaveCountLessThan(10);
        JsonDocument.Parse(settings.Written.Last()).RootElement.GetProperty("text").GetString().Should().Be("testo 10");
    }

    [Fact]
    public async Task A_failing_write_shows_an_error_and_keeps_the_text()
    {
        var settings = new FakeAppSettingService { ThrowOnSet = new InvalidOperationException("boom") };
        var (sut, _, _) = Build(settings);
        await sut.LoadAsync(Node, DayBoundary.Utc, true);

        sut.Draft = "non perderlo";
        await sut.FlushDraftAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        sut.Draft.Should().Be("non perderlo");
    }

    [Fact]
    public async Task A_successful_save_clears_the_stored_draft()
    {
        var settings = new FakeAppSettingService();
        var (sut, _, _) = Build(settings);
        await sut.LoadAsync(Node, DayBoundary.Utc, true);
        sut.Draft = "da salvare";
        await sut.FlushDraftAsync();
        settings.Store[Key].Should().NotBeEmpty();

        await sut.SaveCommand.ExecuteAsync(null);
        await sut.FlushDraftAsync();

        settings.Store[Key].Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_never_writes_the_draft()
    {
        var settings = new FakeAppSettingService();
        settings.Store[Key] = JsonSerializer.Serialize(new { text = "salvata", tags = new[] { "t" } });
        var (sut, _, _) = Build(settings);

        await sut.LoadAsync(Node, DayBoundary.Utc, true);
        await sut.FlushDraftAsync();

        sut.Draft.Should().Be("salvata");
        settings.SetCount.Should().Be(0);
    }

    [Fact]
    public async Task An_unreadable_stored_value_means_no_draft()
    {
        var settings = new FakeAppSettingService();
        settings.Store[Key] = "{non json";
        var (sut, _, _) = Build(settings);

        await sut.LoadAsync(Node, DayBoundary.Utc, true);

        sut.Draft.Should().BeEmpty();
        sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Switching_habit_with_a_write_in_flight_never_writes_the_old_text_under_the_new_key()
    {
        var settings = new GatedSettings();
        var (sut, _, _) = Build(settings);
        await sut.LoadAsync(Node, DayBoundary.Utc, true);
        sut.Draft = "testo di A";
        sut.Draft = "testo di A, ancora"; // il primo SetAsync e' fermo: questo e' il valore sporco

        var other = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "B" };
        var load = sut.LoadAsync(other, DayBoundary.Utc, true);
        while (!load.IsCompleted)
        {
            foreach (var g in settings.Gates) g.TrySetResult();
            await Task.Yield();
        }
        await load;
        await sut.FlushDraftAsync();

        var keyB = "diary.draft." + other.Id.ToString("N");
        settings.WrittenWithKey.Where(w => w.Key == keyB).Should().BeEmpty();
        sut.Draft.Should().BeEmpty("B non eredita il testo di A");
    }

    [Fact]
    public async Task A_failed_draft_read_shows_an_error_and_does_not_stop_loading()
    {
        var settings = new FakeAppSettingService { ThrowOnGet = new InvalidOperationException("boom") };
        var (sut, _, _) = Build(settings);

        await sut.LoadAsync(Node, DayBoundary.Utc, true);

        sut.ErrorMessage.Should().Be(Recordum.Presentation.Resources.Strings.Diario_Editor_DraftReadFailed);
    }

    [Fact]
    public async Task The_draft_error_goes_away_on_the_next_successful_write_but_other_errors_stay()
    {
        var settings = new FakeAppSettingService { ThrowOnSet = new InvalidOperationException("boom") };
        var (sut, _, _) = Build(settings);
        await sut.LoadAsync(Node, DayBoundary.Utc, true);
        sut.Draft = "uno";
        await sut.FlushDraftAsync();
        sut.ErrorMessage.Should().Be(Recordum.Presentation.Resources.Strings.Diario_Editor_DraftFailed);

        settings.ThrowOnSet = null;
        sut.Draft = "due";
        await sut.FlushDraftAsync();
        sut.ErrorMessage.Should().BeNull();

        sut.ErrorMessage = "errore di Salva";
        sut.Draft = "tre";
        await sut.FlushDraftAsync();
        sut.ErrorMessage.Should().Be("errore di Salva");
    }

    [Fact]
    public async Task Saving_an_edit_clears_the_stored_edit_part_and_keeps_the_new_entry_draft()
    {
        var settings = new FakeAppSettingService();
        var (sut, _, _) = Build(settings);
        await sut.LoadAsync(Node, DayBoundary.Utc, true);
        sut.Draft = "voce";
        await sut.SaveCommand.ExecuteAsync(null);
        sut.Draft = "bozza nuova";
        await sut.BeginEditCommand.ExecuteAsync(sut.Entries.Single());
        sut.ActiveText = "voce cambiata";
        await sut.FlushDraftAsync();
        JsonDocument.Parse(settings.Store[Key]).RootElement.GetProperty("editNoteId").ValueKind.Should().NotBe(JsonValueKind.Null);

        await sut.SaveCommand.ExecuteAsync(null);
        await sut.FlushDraftAsync();

        var json = JsonDocument.Parse(settings.Store[Key]).RootElement;
        json.GetProperty("text").GetString().Should().Be("bozza nuova");
        json.GetProperty("editNoteId").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("editText").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("editTags").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task In_quick_note_mode_a_stored_edit_part_is_not_restored()
    {
        var settings = new FakeAppSettingService();
        var (sut, notes, diary) = Build(settings);
        await sut.LoadAsync(Node, DayBoundary.Utc, true);
        sut.Draft = "voce";
        await sut.SaveCommand.ExecuteAsync(null);
        var id = sut.Entries.Single().Id;
        settings.Store[Key] = JsonSerializer.Serialize(new { text = "bozza", tags = Array.Empty<string>(), editNoteId = id, editText = "modificata", editTags = Array.Empty<string>() });

        var quick = BuildOn(notes, diary, settings);
        await quick.LoadAsync(Node, DayBoundary.Utc, false);

        quick.Draft.Should().Be("bozza");
        quick.Entries.Should().ContainSingle().Which.IsEditing.Should().BeFalse();
    }

    // ---- F2d2-6: la bozza ricorda il suo giorno ----

    private static readonly DateOnly Yesterday = new(2025, 12, 31); // FakeClock parte il 2026-01-01

    [Fact]
    public async Task A_draft_written_on_yesterday_reopens_on_yesterday_and_Save_puts_the_entry_there()
    {
        var settings = new FakeAppSettingService();
        var (sut, notes, diary) = Build(settings);
        await sut.LoadAsync(Node, DayBoundary.Utc, true);
        sut.SetDay(Yesterday);
        sut.Draft = "cena di ieri";
        await sut.FlushDraftAsync();

        var writesBefore = settings.SetCount;
        var again = BuildOn(notes, diary, settings);
        await again.LoadAsync(Node, DayBoundary.Utc, true);
        await again.FlushDraftAsync();

        again.Day.Should().Be(Yesterday);
        again.Draft.Should().Be("cena di ieri");
        settings.SetCount.Should().Be(writesBefore, "riaprire sul giorno della bozza non la riscrive");
        await again.SaveCommand.ExecuteAsync(null);
        diary.SavedEntries.Single().Day.Should().Be(Yesterday);
    }

    [Fact]
    public async Task SetDay_with_draft_content_writes_the_new_day_and_without_content_writes_nothing()
    {
        var settings = new FakeAppSettingService();
        var (sut, _, _) = Build(settings);
        await sut.LoadAsync(Node, DayBoundary.Utc, true);
        sut.SetDay(Yesterday);
        await sut.FlushDraftAsync();
        settings.SetCount.Should().Be(0, "senza contenuto non c'e' niente da ricordare");

        sut.Draft = "x";
        await sut.FlushDraftAsync();
        JsonDocument.Parse(settings.Store[Key]).RootElement.GetProperty("day").GetString().Should().Be("2025-12-31");

        sut.SetDay(new DateOnly(2025, 12, 30));
        await sut.FlushDraftAsync();
        JsonDocument.Parse(settings.Store[Key]).RootElement.GetProperty("day").GetString().Should().Be("2025-12-30");
    }

    [Fact]
    public async Task A_draft_without_day_from_an_older_build_opens_on_today()
    {
        var settings = new FakeAppSettingService();
        settings.Store[Key] = JsonSerializer.Serialize(new { text = "vecchia", tags = new[] { "t" } });
        var (sut, _, _) = Build(settings);

        await sut.LoadAsync(Node, DayBoundary.Utc, true);

        sut.Day.Should().Be(new DateOnly(2026, 1, 1));
        sut.Draft.Should().Be("vecchia");
    }
}
