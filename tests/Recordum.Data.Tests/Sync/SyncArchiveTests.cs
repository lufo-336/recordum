using System.Data;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Models;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures;
using Recordum.Data.Tests.Fixtures.Sync;

namespace Recordum.Data.Tests.Sync;

public sealed class SyncArchiveTests
{
    private static SyncWriteOptions DefaultOptions(long writtenAtMs = 1767225600000L, bool includePhotos = true) =>
        new(
            DeviceId: "device-fedora-01",
            DeviceName: "Fedora Studio",
            AppVersion: "1.0.0",
            IncludePhotos: includePhotos,
            WrittenAtMs: writtenAtMs);

    [Fact]
    public async Task RoundTripEveryTravellingEntity()
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);

        await using var db = await fx.Factory.CreateDbContextAsync();
        var codec = new SyncRowCodec(db.Model);

        using var zipStream = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zipStream, DefaultOptions(fx.Clock.UtcNowMs));
        zipStream.Position = 0;

        using var snapshot = SyncArchiveReader.Open(zipStream);

        snapshot.Manifest.Format.Should().Be("recordum-sync");
        snapshot.Manifest.FormatVersion.Should().Be("1.0");
        snapshot.Manifest.DeviceId.Should().Be("device-fedora-01");
        snapshot.Manifest.DeviceName.Should().Be("Fedora Studio");
        snapshot.Manifest.AppVersion.Should().Be("1.0.0");
        snapshot.Manifest.WrittenAtMs.Should().Be(fx.Clock.UtcNowMs);
        snapshot.Manifest.IncludesPhotos.Should().BeTrue();

        var conn = (SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        SyncCatalog.Entities.Should().HaveCount(26);
        snapshot.Rows.Should().HaveCount(SyncCatalog.Entities.Count);
        snapshot.Manifest.Entities.Should().HaveCount(SyncCatalog.Entities.Count);

        foreach (var entity in SyncCatalog.Entities)
        {
            var originalRows = await ReadRawTableRowsAsync(conn, entity);
            originalRows.Should().NotBeEmpty($"'{entity.PublicName}' must have at least one seeded row");

            // Verifica che in ogni entità ci sia almeno una riga con tutte le colonne non-null e non-default
            originalRows.Should().Contain(row => row.Values.All(v => v != null),
                $"'{entity.PublicName}' must have a seeded row where every column has a non-null value");

            // Ogni colonna ha, in almeno una riga, un valore non di default: un campo perso dal codec si vede.
            foreach (var col in originalRows[0].Keys)
            {
                originalRows.Any(row => !IsDefault(row[col])).Should().BeTrue(
                    $"'{entity.PublicName}.{col}' must be seeded with a non-default value");
            }

            snapshot.Rows.Should().ContainKey(entity.PublicName);
            var readRows = snapshot.Rows[entity.PublicName];
            readRows.Count.Should().Be(originalRows.Count, $"row count mismatch for '{entity.PublicName}'");
            snapshot.Manifest.Entities[entity.PublicName].Should().Be(originalRows.Count);

            readRows.Select(r => r.Id).Should().BeInAscendingOrder(StringComparer.Ordinal);

            for (var i = 0; i < originalRows.Count; i++)
            {
                var roundTripped = codec.ToDb(entity, readRows[i]);
                roundTripped.Keys.Should().BeEquivalentTo(originalRows[i].Keys);
                foreach (var (col, expectedVal) in originalRows[i])
                {
                    roundTripped[col].Should().Be(expectedVal, $"mismatch on '{entity.PublicName}.{col}'");
                }
            }
        }

        // Verifica foto e miniatura byte per byte
        var attachmentId = SyncSeed.Stored(SyncSeed.AttachmentId);
        await using (var photoStream = snapshot.OpenPhoto(attachmentId, thumbnail: false))
        {
            photoStream.Should().NotBeNull();
            using var ms = new MemoryStream();
            await photoStream!.CopyToAsync(ms);
            ms.ToArray().Should().Equal(SyncSeed.CreateSamplePhotoBytes());
        }

        await using (var thumbStream = snapshot.OpenPhoto(attachmentId, thumbnail: true))
        {
            thumbStream.Should().NotBeNull();
            using var ms = new MemoryStream();
            await thumbStream!.CopyToAsync(ms);
            ms.ToArray().Should().Equal(SyncSeed.CreateSampleThumbnailBytes());
        }

        // Verifica tombstones
        snapshot.Tombstones.Should().ContainSingle();
        var tomb = snapshot.Tombstones[0];
        tomb.Entity.Should().Be("tag");
        tomb.EntityId.Should().Be("9ABCDEF9-0000-0000-0000-000000000099");
        tomb.DeviceId.Should().Be("device-seed-a");
        tomb.AliasOf.Should().Be(SyncSeed.Stored(SyncSeed.TagId));
    }

    [Fact]
    public async Task WithoutPhotosOnlyThumbnailsAreWritten()
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);

        await using var db = await fx.Factory.CreateDbContextAsync();
        using var zipStream = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zipStream, DefaultOptions(includePhotos: false));

        var attachmentId = SyncSeed.Stored(SyncSeed.AttachmentId);

        zipStream.Position = 0;
        using (var rawZip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true))
        {
            rawZip.GetEntry($"photos/{attachmentId}.jpg").Should().BeNull();
            rawZip.GetEntry($"photos/{attachmentId}.png").Should().BeNull();
            rawZip.GetEntry($"photos/{attachmentId}.thumb.jpg").Should().NotBeNull();
        }

        zipStream.Position = 0;
        using var snapshot = SyncArchiveReader.Open(zipStream);
        snapshot.Manifest.IncludesPhotos.Should().BeFalse();
        snapshot.OpenPhoto(attachmentId, thumbnail: false).Should().BeNull();

        await using var thumbStream = snapshot.OpenPhoto(attachmentId, thumbnail: true);
        thumbStream.Should().NotBeNull();
        using var ms = new MemoryStream();
        await thumbStream!.CopyToAsync(ms);
        ms.ToArray().Should().Equal(SyncSeed.CreateSampleThumbnailBytes());
    }

    [Fact]
    public async Task LocalEntitiesAndLocalSettingsAreNeverWritten()
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);

        await using var db = await fx.Factory.CreateDbContextAsync();
        db.ActiveTimers.Add(new ActiveTimer
        {
            Id = Guid.NewGuid(),
            HabitNodeId = SyncSeed.HabitNodeId,
            TimerMode = Recordum.Core.Enums.TimerMode.Stopwatch,
            StartedAtMs = fx.Clock.UtcNowMs,
            AccumulatedPausedMs = 0,
            CreatedAt = fx.Clock.UtcNow.UtcDateTime,
            UpdatedAt = fx.Clock.UtcNow.UtcDateTime,
            UpdatedAtMs = fx.Clock.UtcNowMs,
        });
        db.AppSettings.Add(new AppSetting
        {
            Key = "home.rows",
            Value = "Compact",
            UpdatedAt = fx.Clock.UtcNow.UtcDateTime,
            UpdatedAtMs = fx.Clock.UtcNowMs,
        });
        db.AppSettings.Add(new AppSetting
        {
            Key = "sync.passphrase",
            Value = "top-secret-passphrase",
            UpdatedAt = fx.Clock.UtcNow.UtcDateTime,
            UpdatedAtMs = fx.Clock.UtcNowMs,
        });
        await db.SaveChangesAsync();

        using var zipStream = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zipStream, DefaultOptions());

        zipStream.Position = 0;
        using (var rawZip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true))
        {
            rawZip.GetEntry("data/active_timer.jsonl").Should().BeNull();
            foreach (var localTable in SyncCatalog.LocalTables)
            {
                rawZip.GetEntry($"data/{localTable}.jsonl").Should().BeNull();
            }

            using var appSettingsStream = rawZip.GetEntry("data/app_setting.jsonl")!.Open();
            using var reader = new StreamReader(appSettingsStream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync();
            content.Should().NotContain("home.rows");
            content.Should().NotContain("sync.passphrase");
            content.Should().NotContain("top-secret-passphrase");
        }

        zipStream.Position = 0;
        using var snapshot = SyncArchiveReader.Open(zipStream);
        snapshot.Rows.Keys.Should().NotContain("active_timer");
        var settingKeys = snapshot.Rows["app_setting"].Select(r => r.Id).ToList();
        settingKeys.Should().BeEquivalentTo(SyncCatalog.TravellingSettingKeys);
        settingKeys.Should().NotContain(["home.rows", "sync.passphrase"]);
    }

    [Fact]
    public async Task UnknownFieldsAndEntitiesAreIgnored()
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);

        await using var db = await fx.Factory.CreateDbContextAsync();
        using var zipStream = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zipStream, DefaultOptions());

        // Aggiungiamo data/future_thing.jsonl e un campo "future_field" a data/tag.jsonl
        zipStream.Position = 0;
        using (var editZip = new ZipArchive(zipStream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var futureEntry = editZip.CreateEntry("data/future_thing.jsonl");
            await using (var s = futureEntry.Open())
            await using (var w = new StreamWriter(s, new UTF8Encoding(false)))
            {
                await w.WriteAsync("{\"id\":\"future-1\",\"name\":\"quantum habit\",\"updated_at_ms\":123}\n");
            }

            var tagEntry = editZip.GetEntry("data/tag.jsonl")!;
            string tagText;
            using (var s = tagEntry.Open())
            using (var r = new StreamReader(s, Encoding.UTF8))
            {
                tagText = await r.ReadToEndAsync();
            }

            tagEntry.Delete();
            var newTagEntry = editZip.CreateEntry("data/tag.jsonl");
            await using (var s = newTagEntry.Open())
            await using (var w = new StreamWriter(s, new UTF8Encoding(false)))
            {
                var modified = tagText.Replace(
                    "\"name\":\"deep-work\"",
                    "\"name\":\"deep-work\",\"future_field\":\"ignored-value\"",
                    StringComparison.Ordinal);
                await w.WriteAsync(modified);
            }
        }

        zipStream.Position = 0;
        using var snapshot = SyncArchiveReader.Open(zipStream);
        snapshot.Rows.Should().NotContainKey("future_thing");

        var tagEntity = SyncCatalog.Get("tag");
        var tagRow = snapshot.Rows["tag"].Single(r => r.Id == SyncSeed.Stored(SyncSeed.TagId));
        tagRow.Fields.Should().ContainKey("future_field");

        var codec = new SyncRowCodec(db.Model);
        var dbRow = codec.ToDb(tagEntity, tagRow);
        dbRow.Should().NotContainKey("future_field");
        dbRow["name"].Should().Be("deep-work");
    }

    [Fact]
    public async Task MinorVersionNewerIsRead()
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);

        await using var db = await fx.Factory.CreateDbContextAsync();
        using var zipStream = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zipStream, DefaultOptions());

        await ReplaceManifestVersionAsync(zipStream, "1.9");

        zipStream.Position = 0;
        using var snapshot = SyncArchiveReader.Open(zipStream);
        snapshot.Manifest.FormatVersion.Should().Be("1.9");
        snapshot.Rows["habit_node"].Should().ContainSingle();
    }

    [Fact]
    public async Task MergedFromIsWrittenAndMayBeAbsent()
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);
        await using var db = await fx.Factory.CreateDbContextAsync();
        using var zipStream = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zipStream, DefaultOptions() with
        {
            MergedFrom = new Dictionary<string, long> { ["device-x"] = 42 },
        });

        zipStream.Position = 0;
        using (var snapshot = SyncArchiveReader.Open(zipStream))
            snapshot.Manifest.MergedFrom.Should().Equal(new Dictionary<string, long> { ["device-x"] = 42 });

        await EditManifestAsync(zipStream, json => System.Text.RegularExpressions.Regex.Replace(json, ",\"merged_from\":\\{[^}]*\\}", ""));
        zipStream.Position = 0;
        using (var snapshot = SyncArchiveReader.Open(zipStream))
            snapshot.Manifest.MergedFrom.Should().BeEmpty("a file without merged_from merged nothing");

        await EditManifestAsync(zipStream, json => json.TrimEnd()[..^1] + ",\"merged_from\":null}");
        zipStream.Position = 0;
        using (var snapshot = SyncArchiveReader.Open(zipStream))
            snapshot.Manifest.MergedFrom.Should().BeEmpty();
    }

    [Fact]
    public async Task MajorVersionDifferentIsRejected()
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);

        await using var db = await fx.Factory.CreateDbContextAsync();
        using var zipStream = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zipStream, DefaultOptions());

        await ReplaceManifestVersionAsync(zipStream, "2.0");

        zipStream.Position = 0;
        var act = () => SyncArchiveReader.Open(zipStream);
        act.Should().Throw<SyncFormatException>();
    }

    [Fact]
    public async Task BrokenJsonLineRejectsWholeFile()
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);

        await using var db = await fx.Factory.CreateDbContextAsync();
        for (var i = 1; i <= 3; i++)
        {
            db.Completions.Add(new Completion
            {
                Id = Guid.NewGuid(),
                HabitNodeId = SyncSeed.HabitNodeId,
                CompletedAt = fx.Clock.UtcNow.UtcDateTime,
                CompletedAtMs = fx.Clock.UtcNowMs + i,
                UpdatedAtMs = fx.Clock.UtcNowMs + i,
            });
        }

        await db.SaveChangesAsync();

        using var zipStream = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zipStream, DefaultOptions());

        // Tronca la riga 3 di data/completion.jsonl
        zipStream.Position = 0;
        using (var editZip = new ZipArchive(zipStream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = editZip.GetEntry("data/completion.jsonl")!;
            string[] lines;
            using (var s = entry.Open())
            using (var r = new StreamReader(s, Encoding.UTF8))
            {
                var all = await r.ReadToEndAsync();
                lines = all.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            }

            lines.Length.Should().BeGreaterThanOrEqualTo(3);
            lines[2] = lines[2][..(lines[2].Length / 2)]; // riga 3 troncata

            entry.Delete();
            var replacement = editZip.CreateEntry("data/completion.jsonl");
            await using var outStream = replacement.Open();
            await using var writer = new StreamWriter(outStream, new UTF8Encoding(false));
            foreach (var line in lines)
            {
                await writer.WriteAsync(line);
                await writer.WriteAsync('\n');
            }
        }

        zipStream.Position = 0;
        var act = () => SyncArchiveReader.Open(zipStream);
        act.Should().Throw<SyncFormatException>();
    }

    [Fact]
    public async Task DecimalSurvivesExactly()
    {
        const decimal exactDecimal = 0.1000000000000000055511151231m;
        const string exactDecimalText = "0.1000000000000000055511151231";

        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);

        var valueId = new Guid("ABCDEF01-0000-0000-0000-000000000099");
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.CompletionValues.Add(new CompletionValue
        {
            Id = valueId,
            CompletionId = SyncSeed.CompletionId,
            MetricDefinitionId = SyncSeed.MetricDefinitionId,
            SeriesIndex = 2,
            ValueDecimal = exactDecimal,
            UpdatedAtMs = fx.Clock.UtcNowMs,
        });
        await db.SaveChangesAsync();

        using var zipStream = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zipStream, DefaultOptions());
        zipStream.Position = 0;

        using var snapshot = SyncArchiveReader.Open(zipStream);
        var row = snapshot.Rows["completion_value"].Single(r =>
            r.Id == SyncSeed.Stored(valueId));
        var field = row.Fields["value_decimal"];
        field.ValueKind.Should().Be(JsonValueKind.String);
        field.GetString().Should().Be(exactDecimalText);

        var codec = new SyncRowCodec(db.Model);
        var dbRow = codec.ToDb(SyncCatalog.Get("completion_value"), row);
        dbRow["value_decimal"].Should().Be(exactDecimalText);
    }

    [Fact]
    public async Task InvalidUtf8IsRejected() =>
        await AssertEditRejectedAsync("data/tag.jsonl", bytes =>
        {
            // 0xFF dentro un valore stringa: con un decoder permissivo diventerebbe U+FFFD e il JSON resterebbe valido.
            var copy = (byte[])bytes.Clone();
            copy[Array.IndexOf(copy, (byte)'d', Array.IndexOf(copy, (byte)'-') - 4)] = 0xFF;
            return copy;
        });

    [Fact]
    public async Task DuplicateIdIsRejected() =>
        await AssertEditRejectedAsync("data/tag.jsonl", bytes =>
        {
            var line = Encoding.UTF8.GetString(bytes).TrimEnd('\n').Split('\n')[0];
            return Encoding.UTF8.GetBytes(line + "\n" + line + "\n");
        });

    [Fact]
    public async Task RowIdNotMatchingItsKeyFieldsIsRejected() =>
        await AssertEditRejectedAsync("data/habit_node_tag.jsonl", bytes =>
            Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace(
                "\"tag_id\":\"" + SyncSeed.Stored(SyncSeed.TagId) + "\"",
                "\"tag_id\":\"ABCDEF01-0000-0000-0000-00000000FFFF\"", StringComparison.Ordinal)));

    [Fact]
    public async Task DuplicateZipEntryIsRejected()
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);
        await using var db = await fx.Factory.CreateDbContextAsync();
        using var src = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, src, DefaultOptions());
        src.Position = 0;

        using var dup = new MemoryStream();
        using (var inZip = new ZipArchive(src, ZipArchiveMode.Read, leaveOpen: true))
        using (var outZip = new ZipArchive(dup, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var e in inZip.Entries)
            {
                using var ms = new MemoryStream();
                using (var s = e.Open()) await s.CopyToAsync(ms);
                for (var n = 0; n < (e.FullName == "data/tag.jsonl" ? 2 : 1); n++)
                {
                    using var o = outZip.CreateEntry(e.FullName).Open();
                    o.Write(ms.ToArray());
                }
            }
        }

        dup.Position = 0;
        var act = () => SyncArchiveReader.Open(dup);
        act.Should().Throw<SyncFormatException>();
    }

    [Fact]
    public async Task NonAsciiTextIsWrittenUnescaped()
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.Tags.Add(new Tag { Id = Guid.NewGuid(), Name = "perche\u0301 è \"così\"", UpdatedAtMs = 1 });
        await db.SaveChangesAsync();
        using var zip = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zip, DefaultOptions());
        zip.Position = 0;
        using var raw = new ZipArchive(zip, ZipArchiveMode.Read);
        using var r = new StreamReader(raw.GetEntry("data/tag.jsonl")!.Open(), Encoding.UTF8);
        var text = await r.ReadToEndAsync();
        text.Should().Contain("è").And.NotContain("\\u00E8").And.NotContain("\\u00e8");
    }

    private static async Task AssertEditRejectedAsync(string entryName, Func<byte[], byte[]> edit)
    {
        await using var fx = new DbFixture();
        await SyncSeed.SeedAsync(fx);
        await using var db = await fx.Factory.CreateDbContextAsync();
        using var zip = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zip, DefaultOptions());
        zip.Position = 0;
        using (var edit0 = new ZipArchive(zip, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = edit0.GetEntry(entryName)!;
            byte[] bytes;
            using (var s = entry.Open())
            using (var ms = new MemoryStream())
            {
                await s.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            entry.Delete();
            using var o = edit0.CreateEntry(entryName).Open();
            o.Write(edit(bytes));
        }

        zip.Position = 0;
        var act = () => SyncArchiveReader.Open(zip);
        act.Should().Throw<SyncFormatException>();
    }

    private static bool IsDefault(object? v) => v is null or 0L or 0.0 or "";

    private static Task ReplaceManifestVersionAsync(MemoryStream zipStream, string newVersion) =>
        EditManifestAsync(zipStream, json => json.Replace(
            "\"format_version\":\"1.0\"", $"\"format_version\":\"{newVersion}\"", StringComparison.Ordinal));

    private static async Task EditManifestAsync(MemoryStream zipStream, Func<string, string> edit)
    {
        zipStream.Position = 0;
        using var editZip = new ZipArchive(zipStream, ZipArchiveMode.Update, leaveOpen: true);
        var manifestEntry = editZip.GetEntry("manifest.json")!;
        string manifestJson;
        using (var s = manifestEntry.Open())
        using (var r = new StreamReader(s, Encoding.UTF8))
        {
            manifestJson = await r.ReadToEndAsync();
        }

        manifestEntry.Delete();
        var replacement = editZip.CreateEntry("manifest.json");
        await using var outStream = replacement.Open();
        await using var writer = new StreamWriter(outStream, new UTF8Encoding(false));
        await writer.WriteAsync(edit(manifestJson));
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRawTableRowsAsync(
        SqliteConnection conn,
        SyncEntity entity)
    {
        var nonPhotoColumns = entity.Fields
            .Where(f => f.Kind != SyncFieldKind.Photo)
            .Select(f => f.Column)
            .ToArray();

        var selectCols = string.Join(", ", nonPhotoColumns.Select(c => $"\"{c}\""));
        var orderCols = string.Join(", ", entity.KeyColumns.Select(c => $"\"{c}\""));

        await using var cmd = conn.CreateCommand();
        if (string.Equals(entity.PublicName, "app_setting", StringComparison.Ordinal))
        {
            var keys = SyncCatalog.TravellingSettingKeys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            var paramNames = new string[keys.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                paramNames[i] = $"@k{i}";
                cmd.Parameters.AddWithValue(paramNames[i], keys[i]);
            }

            cmd.CommandText = $"SELECT {selectCols} FROM \"{entity.Table}\" WHERE \"key\" IN ({string.Join(", ", paramNames)}) ORDER BY {orderCols}";
        }
        else
        {
            cmd.CommandText = $"SELECT {selectCols} FROM \"{entity.Table}\" ORDER BY {orderCols}";
        }

        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dict = new Dictionary<string, object?>(nonPhotoColumns.Length, StringComparer.Ordinal);
            for (var i = 0; i < nonPhotoColumns.Length; i++)
            {
                dict[nonPhotoColumns[i]] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            }

            rows.Add(dict);
        }

        return rows;
    }
}
