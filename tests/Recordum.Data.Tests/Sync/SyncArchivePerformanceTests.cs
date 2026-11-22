using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures;
using Recordum.Data.Tests.Fixtures.Sync;

namespace Recordum.Data.Tests.Sync;

[Trait("Category", "Performance")]
public sealed class SyncArchivePerformanceTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private const int PhotoCount = 200;
    private const int PhotoSizeBytes = 500 * 1024; // 500 KB -> ~100 MB totali
    private const int ThumbSizeBytes = 2 * 1024;
    private const long MaxManagedMemoryPeakBytes = 64L * 1024 * 1024; // 64 MB

    [Fact]
    [Trait("Category", "Performance")]
    public async Task WritesAndReads200PhotosWithBoundedMemory()
    {
        var workDir = Path.Combine(AppContext.BaseDirectory, "sync-perf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var zipPath = Path.Combine(workDir, "perf.rcsync.zip");

        try
        {
            await using var fx = new DbFixture();
            await SyncSeed.SeedAsync(fx);

            var attachmentIds = new List<string>(PhotoCount);

            await using (var seedDb = await fx.Factory.CreateDbContextAsync())
            {
                var conn = (SqliteConnection)seedDb.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                // Riusiamo un singolo buffer da 500 KB in un comando SQL parametrico per popolare i 100 MB in SQLite
                // senza trattenere 200 array nel ChangeTracker di EF.
                var sharedPhoto = new byte[PhotoSizeBytes];
                for (var i = 0; i < sharedPhoto.Length; i++)
                    sharedPhoto[i] = (byte)((i * 19 + 7) & 0xFF);

                var sharedThumb = new byte[ThumbSizeBytes];
                for (var i = 0; i < sharedThumb.Length; i++)
                    sharedThumb[i] = (byte)((i * 37 + 11) & 0xFF);

                await using var tx = await conn.BeginTransactionAsync();
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = (SqliteTransaction)tx;
                cmd.CommandText = """
                    INSERT INTO "attachment"
                    ("id", "note_id", "filename", "mime_type", "file_size_bytes", "content", "thumbnail", "is_missing", "created_at", "updated_at", "updated_at_ms")
                    VALUES (@id, @noteId, @filename, 'image/jpeg', @size, @content, @thumb, 0, '2026-02-10T14:30:00.0000000Z', '2026-02-10T15:00:00.0000000Z', 1770735600000)
                    """;

                var pId = cmd.Parameters.Add("@id", SqliteType.Text);
                cmd.Parameters.AddWithValue("@noteId", SyncSeed.NoteId.ToString());
                var pFile = cmd.Parameters.Add("@filename", SqliteType.Text);
                cmd.Parameters.AddWithValue("@size", PhotoSizeBytes);
                cmd.Parameters.AddWithValue("@content", sharedPhoto);
                cmd.Parameters.AddWithValue("@thumb", sharedThumb);

                for (var i = 0; i < PhotoCount; i++)
                {
                    var id = new Guid(i + 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x99).ToString();
                    attachmentIds.Add(id);
                    pId.Value = id;
                    pFile.Value = $"photo-{i:D3}.jpg";
                    await cmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
            }

            ForceFullGc();
            var baselineMemory = GC.GetTotalMemory(forceFullCollection: false);
            var peakDeltaBytes = 0L;

            void SampleMemory()
            {
                var current = GC.GetTotalMemory(forceFullCollection: false);
                var delta = current - baselineMemory;
                if (delta > peakDeltaBytes)
                    peakDeltaBytes = delta;
            }

            await using (var writeDb = await fx.Factory.CreateDbContextAsync())
            await using (var outStream = new SamplingFileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, SampleMemory))
            {
                var options = new SyncWriteOptions("dev-perf", "Perf Runner", "1.0.0", IncludePhotos: true, WrittenAtMs: fx.Clock.UtcNowMs);
                await SyncArchiveWriter.WriteAsync(writeDb, outStream, options);
                SampleMemory();
            }

            output.WriteLine($"PERF write peak managed delta: {peakDeltaBytes / 1048576.0:F1} MB");
            peakDeltaBytes.Should().BeLessThan(
                MaxManagedMemoryPeakBytes,
                "writing 200 photos (100 MB) must stream one photo at a time under 64 MB of managed heap growth");

            ForceFullGc();
            baselineMemory = GC.GetTotalMemory(forceFullCollection: false);
            var readPeakDeltaBytes = 0L;

            void SampleReadMemory()
            {
                var current = GC.GetTotalMemory(forceFullCollection: false);
                var delta = current - baselineMemory;
                if (delta > readPeakDeltaBytes)
                    readPeakDeltaBytes = delta;
            }

            var copyBuffer = new byte[16 * 1024];
            await using (var inStream = new SamplingFileStream(zipPath, FileMode.Open, FileAccess.Read, SampleReadMemory))
            using (var snapshot = SyncArchiveReader.Open(inStream))
            {
                SampleReadMemory();

                foreach (var id in attachmentIds)
                {
                    await using var photoStream = snapshot.OpenPhoto(id, thumbnail: false);
                    photoStream.Should().NotBeNull();

                    var totalRead = 0;
                    int read;
                    while ((read = await photoStream!.ReadAsync(copyBuffer)) > 0)
                    {
                        totalRead += read;
                    }

                    totalRead.Should().Be(PhotoSizeBytes);
                    SampleReadMemory();
                }
            }

            output.WriteLine($"PERF read peak managed delta: {readPeakDeltaBytes / 1048576.0:F1} MB");
            readPeakDeltaBytes.Should().BeLessThan(
                MaxManagedMemoryPeakBytes,
                "opening snapshot and streaming 200 photos (100 MB) on demand must stay under 64 MB of managed heap growth");
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }

    private static void ForceFullGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class SamplingFileStream(
        string path,
        FileMode mode,
        FileAccess access,
        Action onSample)
        : FileStream(path, mode, access, FileShare.Read, bufferSize: 16 * 1024, useAsync: false)
    {
        private long _bytesSinceLastSample;

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            base.Write(buffer);
            Track(buffer.Length);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Track(buffer.Length);
            return base.WriteAsync(buffer, cancellationToken);
        }

        public override int Read(Span<byte> buffer)
        {
            var r = base.Read(buffer);
            Track(r);
            return r;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Track(buffer.Length);
            return base.ReadAsync(buffer, cancellationToken);
        }

        private void Track(int count)
        {
            _bytesSinceLastSample += count;
            if (_bytesSinceLastSample >= 512 * 1024)
            {
                _bytesSinceLastSample = 0;
                onSample();
            }
        }
    }
}
