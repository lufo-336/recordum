
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Backup;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Export;
using Xunit;

namespace Recordum.Data.Tests.Integration;

/// <summary>
/// Onda 1b (photo attachments) Task 8: end-to-end chain across Tasks 2/3/6, all against real
/// services (no fakes) — attach two photos, export, back up, wipe the live copies, restore, and
/// verify both the raw attachment rows/files AND a fresh export are correct on the far side.
/// </summary>
public class AttachmentEndToEndTests
{
    private const string Pass = "passphrase-1234567890";

    private sealed class StubAggregator : Core.Abstractions.Services.IStreakAggregator
    {
        public ValueTask<IReadOnlyList<Core.Abstractions.Services.StreakRun>> GetHistoryAsync(
            Guid habitNodeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<Core.Abstractions.Services.StreakRun>>([]);
    }

    private static ExportQueryService BuildExportService(EncryptedDbFixture fixture, AttachmentService attachments)
    {
        var bus = new RecordingEventBus();
        var resetTimes = new ResetTimeResolver(
            fixture.DbFactory,
            new AppSettingService(fixture.DbFactory, fixture.Clock, NullLogger<AppSettingService>.Instance),
            fixture.Clock,
            NullLogger<ResetTimeResolver>.Instance);
        var completions = new CompletionService(fixture.DbFactory, fixture.Clock, NullLogger<CompletionService>.Instance, bus, resetTimes);
        var notes = new NoteService(fixture.DbFactory, fixture.Clock, NullLogger<NoteService>.Instance, bus, resetTimes);
        var metrics = new MetricDefinitionService(fixture.DbFactory, fixture.Clock, NullLogger<MetricDefinitionService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var goals = new GoalService(fixture.DbFactory, fixture.Clock, NullLogger<GoalService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var configs = new HabitConfigService(fixture.DbFactory, fixture.Clock, NullLogger<HabitConfigService>.Instance, new Recordum.Data.Tests.Fixtures.RecordingEventBus());
        var ramp = new Recordum.Core.Services.IntensityRampResolver();
        var abstinence = new AbstinenceService(fixture.DbFactory, fixture.Clock, new RecordingEventBus(), resetTimes);
        var refData = new ReferenceDataQueryService(fixture.DbFactory);
        return new ExportQueryService(fixture.DbFactory, fixture.Clock, completions, notes, metrics, goals, configs,
            ramp, new StubAggregator(), abstinence, attachments, refData, resetTimes);
    }

    [Fact]
    public async Task Attach_export_backup_restore_reexport_survives_the_full_chain()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var attachments = new AttachmentService(
                fixture.DbFactory, fixture.Clock, new FakePluginContext());

            Guid habitId, noteId;
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
            {
                var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "Food diary" };
                db.HabitNodes.Add(habit);
                var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habit.Id, BodyMd = "lunch" };
                db.Notes.Add(note);
                await db.SaveChangesAsync();
                habitId = habit.Id;
                noteId = note.Id;
            }

            // Attach two photos to the same note (Task 2).
            await attachments.AddAsync(noteId, new ImportedPhoto(Guid.NewGuid(), "plate.jpg", "image/jpeg", "PLATE-BYTES"u8.ToArray(), [1]), default);
            await attachments.AddAsync(noteId, new ImportedPhoto(Guid.NewGuid(), "dessert.png", "image/png", "DESSERT-BYTES"u8.ToArray(), [2]), default);

            // Export sees both, correctly attributed to the note (Task 3).
            var exportService = BuildExportService(fixture, attachments);
            var request = new ExportRequest(ExportSubject.SingleHabit, habitId,
                new DateOnly(2020, 1, 1), new DateOnly(2030, 1, 1), new ExportLayout());
            var beforeModel = await exportService.BuildAsync(request);
            var beforeNote = Assert.Single(Assert.Single(beforeModel.Habits).Notes);
            beforeNote.Attachments.Should().HaveCount(2);
            beforeNote.Attachments.Select(a => a.Filename).Should().BeEquivalentTo("plate.jpg", "dessert.png");

            // Back up, then destroy the live rows + files to make restore observable (Task 6).
            var bk = await fixture.CreateBackupAsync(Pass);
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
                await db.Attachments.Where(a => a.NoteId == noteId).ExecuteDeleteAsync();

            var restore = fixture.NewRestoreService();
            var result = await restore.RestoreAsync(bk.Path, Pass, new RestoreOptions(SkipSafetySnapshot: true));
            result.UnavailablePlugins.Should().BeEmpty();

            // Both attachments are back, byte-identical, from the database.
            var restoredRows = await attachments.ListByNoteAsync(noteId, default);
            restoredRows.Should().HaveCount(2);
            (await attachments.ReadContentAsync(restoredRows.Single(r => r.Filename == "plate.jpg").Id, default))
                .Should().Equal("PLATE-BYTES"u8.ToArray());
            (await attachments.ReadContentAsync(restoredRows.Single(r => r.Filename == "dessert.png").Id, default))
                .Should().Equal("DESSERT-BYTES"u8.ToArray());

            // A fresh export after restore still sees both photos correctly.
            var afterModel = await BuildExportService(fixture, attachments).BuildAsync(request);
            var afterNote = Assert.Single(Assert.Single(afterModel.Habits).Notes);
            afterNote.Attachments.Should().HaveCount(2);
            afterNote.Attachments.Select(a => a.Filename).Should().BeEquivalentTo("plate.jpg", "dessert.png");
        }
        finally { await fixture.DisposeAsync(); }
    }
}
