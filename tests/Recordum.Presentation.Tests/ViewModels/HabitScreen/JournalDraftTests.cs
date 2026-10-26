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
        public int InFlight { get; private set; }
        public int MaxInFlight { get; private set; }
        public List<TaskCompletionSource> Gates { get; } = [];

        public ValueTask<string?> GetAsync(string key, CancellationToken ct = default) => new((string?)null);

        public async ValueTask SetAsync(string key, string value, CancellationToken ct = default)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Gates.Add(gate);
            InFlight++;
            MaxInFlight = Math.Max(MaxInFlight, InFlight);
            await gate.Task;
            InFlight--;
            Written.Add(value);
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
}
