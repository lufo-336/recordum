using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Backup;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Backup;

/// <summary>
/// Il ponte fra le due macchine del dogfooding (decisione D1, 2026-09-25): un <c>.recbak</c> fatto su un sistema deve
/// ripristinarsi sull'altro. I file in <c>Fixtures/CrossMachine/</c> sono stati creati una volta e committati; la CI li
/// ripristina sia nel job Linux sia in quello Windows, quindi un file fatto su Linux viene aperto davvero su Windows.
/// <para>Quando lo schema sale oltre 2.18 questo test deve continuare a passare: è la promessa di E17 ("un backup
/// della versione precedente si ripristina").</para>
/// </summary>
public class CrossMachineRestoreTests
{
    private const string Pass = "cross-machine-passphrase";
    private static readonly Guid NoteId = Guid.Parse("7d3e1c2a-0000-4000-8000-000000000002");

    [Theory]
    [InlineData("made-on-linux-v2.18.recbak", "made on Linux")]
    public async Task A_backup_made_on_another_machine_restores_here(string file, string noteBody)
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CrossMachine", file);

            await fixture.NewRestoreService().RestoreAsync(source, Pass, new RestoreOptions(SkipSafetySnapshot: true));

            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
                (await db.Notes.SingleAsync(n => n.Id == NoteId)).BodyMd.Should().Be(noteBody);

            var attachments = new AttachmentService(fixture.DbFactory, fixture.Clock, new FakePluginContext(), fixture.RootDir);
            var rows = await attachments.ListByNoteAsync(NoteId, default);
            rows.Should().ContainSingle();
            (await File.ReadAllBytesAsync(attachments.ResolveAbsolutePath(rows[0])))
                .Should().Equal("CROSS-MACHINE-PHOTO"u8.ToArray());
        }
        finally { await fixture.DisposeAsync(); }
    }
}
