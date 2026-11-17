using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Backup;
using Recordum.Core.Models;
using Recordum.Data.Security;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Migrations;

/// <summary>
/// ScaleAnchorsV220 (GIO-23, spec §8): una posizione salvata p diventa il valore vero <c>min + p − 1</c> in
/// <c>value_decimal</c>; le etichette diventano scaglioni (e tag, sulle scale dell'utente); le copie per abitudine
/// vere diventano scale proprie. Il seme è scritto in SQL grezzo sullo schema 2.19: il modello EF di oggi non ha più
/// <c>labels_json</c>.
/// </summary>
public sealed class ScaleAnchorsV220MigrationTests
{
    private static readonly Guid Nrs = Guid.Parse("00000002-0000-0000-0000-000000000003");
    private static readonly Guid Mood5 = Guid.Parse("00000002-0000-0000-0000-000000000007");

    [Fact]
    public async Task Official_NRS_position_becomes_its_true_value_and_official_anchors_are_seeded()
    {
        await using var conn = await OpenAt219Async();
        var h = await HabitAsync(conn, "Dolore", "State");
        var m = await MetricAsync(conn, h, "Dolore", Nrs, null);
        var low = await ValueAsync(conn, h, m, 1);
        var six = await ValueAsync(conn, h, m, 6);
        var top = await ValueAsync(conn, h, m, 11);

        await MigrateAsync(conn);

        (await DecimalOf(conn, six)).Should().Be(5m);
        (await DecimalOf(conn, low)).Should().Be(0m);
        (await DecimalOf(conn, top)).Should().Be(10m);
        (await ScalarAsync(conn, $"SELECT value_integer FROM completion_values WHERE id = '{G(six)}'")).Should().BeNull();
        (await ScalarAsync(conn, $"SELECT type FROM metric_definitions WHERE id = '{G(m)}'")).Should().Be("Decimal");
        (await ScalarAsync(conn, $"SELECT scale_template_id FROM metric_definitions WHERE id = '{G(m)}'")).Should().Be(G(Nrs));
        (await ScalarAsync(conn, $"SELECT max_value FROM scale_templates WHERE id = '{G(Nrs)}'")).Should().Be(10L);

        var anchors = await RowsAsync(conn, $"SELECT value, label, tag_id FROM scale_anchors WHERE scale_template_id = '{G(Nrs)}' ORDER BY value");
        anchors.Should().HaveCount(11);
        anchors.Select(a => (long)a[0]!).Should().Equal(Enumerable.Range(0, 11).Select(i => (long)i));
        anchors[0][1].Should().Be("No pain");
        anchors[10][1].Should().Be("Worst pain");
        anchors.Should().OnlyContain(a => a[2] == null);
        (await ScalarAsync(conn, "SELECT count(*) FROM tags")).Should().Be(0L);
    }

    [Fact]
    public async Task The_decimal_written_by_the_migration_is_the_text_EF_writes_and_reads_back()
    {
        // Misurato, non dedotto: il testo che la migrazione scrive in value_decimal è quello che EF scrive per lo
        // stesso decimale, ed EF lo rilegge come decimale.
        await using var conn = await OpenAt219Async();
        var h = await HabitAsync(conn, "Dolore", "State");
        var m = await MetricAsync(conn, h, "Dolore", Nrs, null);
        var six = await ValueAsync(conn, h, m, 6);
        await MigrateAsync(conn);

        await using var db = TestDbHelper.CreateFull(conn);
        var completionId = Guid.Parse((string)(await ScalarAsync(conn, $"SELECT completion_id FROM completion_values WHERE id = '{G(six)}'"))!);
        var byEf = new CompletionValue { Id = Guid.NewGuid(), CompletionId = completionId, MetricDefinitionId = m, ValueDecimal = 5m };
        db.CompletionValues.Add(byEf);
        await db.SaveChangesAsync();

        var migratedText = await ScalarAsync(conn, $"SELECT value_decimal FROM completion_values WHERE id = '{G(six)}'");
        var efText = await ScalarAsync(conn, $"SELECT value_decimal FROM completion_values WHERE id = '{G(byEf.Id)}'");
        migratedText.Should().Be(efText);

        db.ChangeTracker.Clear();
        (await db.CompletionValues.SingleAsync(v => v.Id == six)).ValueDecimal.Should().Be(5m);
    }

    [Fact]
    public async Task Official_copy_identical_to_the_italian_translation_is_not_a_true_copy()
    {
        await using var conn = await OpenAt219Async();
        var h = await HabitAsync(conn, "Umore", "State");
        var m = await MetricAsync(conn, h, "Umore", Mood5,
            """["Molto basso","Basso","Neutro","Buono","Molto buono"]""");
        var v = await ValueAsync(conn, h, m, 4);
        var scalesBefore = await ScalarAsync(conn, "SELECT count(*) FROM scale_templates");

        await MigrateAsync(conn);

        (await ScalarAsync(conn, "SELECT count(*) FROM scale_templates")).Should().Be(scalesBefore);
        (await ScalarAsync(conn, $"SELECT scale_template_id FROM metric_definitions WHERE id = '{G(m)}'")).Should().Be(G(Mood5));
        (await DecimalOf(conn, v)).Should().Be(4m); // min 1: posizione 4 → 4
        (await ScalarAsync(conn, "SELECT count(*) FROM tags")).Should().Be(0L);
    }

    [Fact]
    public async Task Official_copy_with_a_changed_label_becomes_a_scale_of_its_own()
    {
        await using var conn = await OpenAt219Async();
        var h = await HabitAsync(conn, "Sonno", "State");
        var m = await MetricAsync(conn, h, "Sonno", Mood5,
            """["Molto basso","Basso","Così così","Buono","Molto buono"]""");
        var v = await ValueAsync(conn, h, m, 3);

        await MigrateAsync(conn);

        var scaleId = (string)(await ScalarAsync(conn, $"SELECT scale_template_id FROM metric_definitions WHERE id = '{G(m)}'"))!;
        scaleId.Should().NotBe(G(Mood5));
        var scale = (await RowsAsync(conn, $"SELECT name, min_value, max_value, is_official, code FROM scale_templates WHERE id = '{scaleId}'")).Single();
        scale[0].Should().Be("Mood Scale (1–5) · Sonno");
        scale[1].Should().Be(1L);
        scale[2].Should().Be(5L);
        scale[3].Should().Be(0L);
        ((string)scale[4]!).Should().NotBeNullOrWhiteSpace();

        var anchors = await AnchorsWithTagsAsync(conn, scaleId);
        anchors.Select(a => (a.Value, a.Label, a.Tag)).Should().Equal(
            (1L, "Molto basso", "molto-basso"),
            (2L, "Basso", "basso"),
            (3L, "Così così", "così-così"),
            (4L, "Buono", "buono"),
            (5L, "Molto buono", "molto-buono"));
        (await DecimalOf(conn, v)).Should().Be(3m);

        // EF ritrova la riga creata dalla migrazione per Id: il GUID è nel formato che EF scrive.
        await using var db = TestDbHelper.CreateFull(conn);
        var byEf = await db.ScaleTemplates.Include(s => s.Anchors).SingleAsync(s => s.Id == Guid.Parse(scaleId));
        byEf.Anchors.Should().HaveCount(5);
        (await db.MetricDefinitions.SingleAsync(x => x.Id == m)).ScaleTemplateId.Should().Be(Guid.Parse(scaleId));
    }

    [Fact]
    public async Task Guids_written_by_the_migration_match_the_format_EF_writes()
    {
        await using var conn = await OpenAt219Async();
        var h = await HabitAsync(conn, "Umore", "State");
        await MetricAsync(conn, h, "Umore", null, """["Giù","Su"]""");
        await MigrateAsync(conn);

        await using (var db = TestDbHelper.CreateFull(conn))
        {
            db.Tags.Add(new Tag { Id = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789"), Name = "ef-tag" });
            await db.SaveChangesAsync();
        }
        var efId = (string)(await ScalarAsync(conn, "SELECT id FROM tags WHERE name = 'ef-tag'"))!;
        var migrated = (string)(await ScalarAsync(conn, "SELECT id FROM tags WHERE name = 'giù'"))!;

        efId.Should().Be("ABCDEF01-2345-6789-ABCD-EF0123456789");
        migrated.Should().MatchRegex("^[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}$");
    }

    [Fact]
    public async Task User_scale_labels_become_anchors_with_derived_tags_and_own_numbers_are_skipped()
    {
        await using var conn = await OpenAt219Async();
        var s = await ScaleAsync(conn, "Come sto", 1, 3, """["Sto malissimo","2","Bene"]""");
        var h = await HabitAsync(conn, "Come sto", "State");
        var m = await MetricAsync(conn, h, "Come sto", s, """["Sto malissimo","2","Bene"]""");
        var v = await ValueAsync(conn, h, m, 2);

        await MigrateAsync(conn);

        var anchors = await AnchorsWithTagsAsync(conn, G(s));
        anchors.Select(a => (a.Value, a.Label, a.Tag)).Should().Equal(
            (1L, "Sto malissimo", "sto-malissimo"),
            (3L, "Bene", "bene"));
        (await ScalarAsync(conn, $"SELECT scale_template_id FROM metric_definitions WHERE id = '{G(m)}'")).Should().Be(G(s));
        (await ScalarAsync(conn, "SELECT count(*) FROM scale_templates WHERE is_official = 0")).Should().Be(1L);
        (await DecimalOf(conn, v)).Should().Be(2m);
    }

    [Fact]
    public async Task Repeated_labels_give_two_anchors_and_one_tag()
    {
        await using var conn = await OpenAt219Async();
        var s = await ScaleAsync(conn, "Doppia", 1, 2, """["Male","male"]""");

        await MigrateAsync(conn);

        var anchors = await AnchorsWithTagsAsync(conn, G(s));
        anchors.Select(a => (a.Value, a.Label, a.Tag)).Should().Equal((1L, "Male", "male"), (2L, "male", "male"));
        (await ScalarAsync(conn, "SELECT count(*) FROM tags")).Should().Be(1L);
    }

    [Fact]
    public async Task An_existing_tag_is_reused_not_duplicated()
    {
        await using var conn = await OpenAt219Async();
        var existing = Guid.NewGuid();
        await ExecAsync(conn, $"INSERT INTO tags (id, name, created_at) VALUES ('{G(existing)}', 'Bene', '2026-06-01T00:00:00Z')");
        var s = await ScaleAsync(conn, "Come sto", 1, 2, """["Male","Bene"]""");

        await MigrateAsync(conn);

        (await ScalarAsync(conn, $"SELECT tag_id FROM scale_anchors WHERE scale_template_id = '{G(s)}' AND value = 2")).Should().Be(G(existing));
        (await ScalarAsync(conn, "SELECT count(*) FROM tags WHERE name = 'bene'")).Should().Be(1L);
        (await ScalarAsync(conn, "SELECT count(*) FROM tags")).Should().Be(2L);
    }

    [Fact]
    public async Task Labels_without_a_scale_become_a_user_scale_one_to_N()
    {
        // Come l'"Umore" di DemoDataSeeder: etichette sulla metrica, nessuna scala.
        await using var conn = await OpenAt219Async();
        var h = await HabitAsync(conn, "Mood", "State");
        var m = await MetricAsync(conn, h, "Umore", null, """["Pessimo","Giù","Ok","Bene","Ottimo"]""");
        var v = await ValueAsync(conn, h, m, 4);

        await MigrateAsync(conn);

        var scaleId = (string?)await ScalarAsync(conn, $"SELECT scale_template_id FROM metric_definitions WHERE id = '{G(m)}'");
        scaleId.Should().NotBeNull();
        var scale = (await RowsAsync(conn, $"SELECT min_value, max_value, is_official FROM scale_templates WHERE id = '{scaleId}'")).Single();
        scale.Should().Equal(1L, 5L, 0L);
        (await AnchorsWithTagsAsync(conn, scaleId!)).Select(a => (a.Value, a.Label)).Should().Equal(
            (1L, "Pessimo"), (2L, "Giù"), (3L, "Ok"), (4L, "Bene"), (5L, "Ottimo"));
        (await DecimalOf(conn, v)).Should().Be(4m);
        (await ScalarAsync(conn, $"SELECT type FROM metric_definitions WHERE id = '{G(m)}'")).Should().Be("Decimal");
    }

    [Fact]
    public async Task State_without_scale_or_labels_becomes_a_zero_to_ten_scale_named_after_the_habit()
    {
        await using var conn = await OpenAt219Async();
        var h = await HabitAsync(conn, "Energia", "State");
        var m = await MetricAsync(conn, h, "Energia", null, null);
        var first = await ValueAsync(conn, h, m, 1);
        var last = await ValueAsync(conn, h, m, 11);

        await MigrateAsync(conn);

        var scaleId = (string?)await ScalarAsync(conn, $"SELECT scale_template_id FROM metric_definitions WHERE id = '{G(m)}'");
        scaleId.Should().NotBeNull();
        var scale = (await RowsAsync(conn, $"SELECT name, min_value, max_value FROM scale_templates WHERE id = '{scaleId}'")).Single();
        scale.Should().Equal("Energia", 0L, 10L);
        (await ScalarAsync(conn, $"SELECT count(*) FROM scale_anchors WHERE scale_template_id = '{scaleId}'")).Should().Be(0L);
        (await DecimalOf(conn, first)).Should().Be(0m);
        (await DecimalOf(conn, last)).Should().Be(10m);
    }

    [Fact]
    public async Task Counter_metric_without_scale_or_labels_is_left_alone()
    {
        await using var conn = await OpenAt219Async();
        var h = await HabitAsync(conn, "Flessioni", "Counter");
        var m = await MetricAsync(conn, h, "Ripetizioni", null, null);
        var v = await ValueAsync(conn, h, m, 25);

        await MigrateAsync(conn);

        (await ScalarAsync(conn, $"SELECT scale_template_id FROM metric_definitions WHERE id = '{G(m)}'")).Should().BeNull();
        (await ScalarAsync(conn, $"SELECT type FROM metric_definitions WHERE id = '{G(m)}'")).Should().Be("Integer");
        (await ScalarAsync(conn, $"SELECT value_integer FROM completion_values WHERE id = '{G(v)}'")).Should().Be(25L);
        (await ScalarAsync(conn, $"SELECT value_decimal FROM completion_values WHERE id = '{G(v)}'")).Should().BeNull();
    }

    [Fact]
    public async Task A_position_beyond_the_top_widens_the_scale()
    {
        await using var conn = await OpenAt219Async();
        var s = await ScaleAsync(conn, "Corta", 1, 5, "[]");
        var h = await HabitAsync(conn, "Corta", "State");
        var m = await MetricAsync(conn, h, "Corta", s, null);
        var v = await ValueAsync(conn, h, m, 7);

        await MigrateAsync(conn);

        (await ScalarAsync(conn, $"SELECT max_value FROM scale_templates WHERE id = '{G(s)}'")).Should().Be(7L);
        (await DecimalOf(conn, v)).Should().Be(7m);
    }

    [Fact]
    public async Task Labels_and_columns_are_gone_and_schema_version_is_2_20()
    {
        await using var conn = await OpenAt219Async();
        await MigrateAsync(conn);

        (await ScalarAsync(conn, "SELECT value FROM app_settings WHERE key = 'schema_version'")).Should().Be("2.20");
        (await ScalarAsync(conn, "SELECT count(*) FROM pragma_table_info('scale_templates') WHERE name = 'labels_json'")).Should().Be(0L);
        (await ScalarAsync(conn, "SELECT count(*) FROM pragma_table_info('metric_definitions') WHERE name = 'labels_json'")).Should().Be(0L);
        (await ScalarAsync(conn, "SELECT count(*) FROM scale_templates WHERE precision <> 'Integer'")).Should().Be(0L);
        var bad = async () => await ExecAsync(conn, $"UPDATE scale_templates SET precision = 'Half' WHERE id = '{G(Nrs)}'");
        await bad.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task A_failure_at_the_last_step_leaves_the_2_19_database_untouched()
    {
        // Spec §8.7: un DB a metà non deve esistere. L'ultimo passo (schema_version) fallisce apposta: la conversione
        // dei valori, gli scaglioni e la caduta delle colonne devono sparire con lui.
        await using var conn = await OpenAt219Async();
        var h = await HabitAsync(conn, "Dolore", "State");
        var m = await MetricAsync(conn, h, "Dolore", Nrs, null);
        var six = await ValueAsync(conn, h, m, 6);
        await ExecAsync(conn, """
            CREATE TRIGGER _test_abort BEFORE UPDATE ON app_settings WHEN NEW.value = '2.20'
            BEGIN SELECT RAISE(ABORT, 'forced'); END;
            """);

        var migrate = async () => await MigrateAsync(conn);
        await migrate.Should().ThrowAsync<SqliteException>();

        (await ScalarAsync(conn, $"SELECT value_integer FROM completion_values WHERE id = '{G(six)}'")).Should().Be(6L);
        (await ScalarAsync(conn, $"SELECT value_decimal FROM completion_values WHERE id = '{G(six)}'")).Should().BeNull();
        (await ScalarAsync(conn, "SELECT count(*) FROM pragma_table_info('metric_definitions') WHERE name = 'labels_json'")).Should().Be(1L);
        (await ScalarAsync(conn, "SELECT count(*) FROM sqlite_master WHERE name LIKE 'scale_anchors' OR name LIKE '\\_m220\\_%' ESCAPE '\\'")).Should().Be(0L);
        (await ScalarAsync(conn, "SELECT value FROM app_settings WHERE key = 'schema_version'")).Should().Be("2.19");
    }

    [Fact]
    public async Task Restore_of_a_2_19_backup_migrates_scale_values()
    {
        const string pass = "passphrase-1234567890";
        var fixture = await EncryptedDbFixture.SeedAsync(); // la base viva è già a 2.20
        try
        {
            var dbPath = fixture.Options.DatabasePath;
            var livePath = dbPath + ".live";
            SqliteConnection.ClearAllPools();
            File.Move(dbPath, livePath);

            // Una base 2.19 con un valore di scala (NRS, posizione 6), nello stesso posto: il backup la legge da lì.
            Guid valueId;
            using (var key = await fixture.KeyProvider.GetKeyAsync())
            using (var conn = SqlCipherConnectionFactory.Open(dbPath, key))
            {
                await using (TestDbHelper.CreateAtMigration(conn, "NoteTagAttachmentBlobV219")) { }
                var h = await HabitAsync(conn, "Dolore", "State");
                var m = await MetricAsync(conn, h, "Dolore", Nrs, null);
                valueId = await ValueAsync(conn, h, m, 6);
            }
            SqliteConnection.ClearAllPools();
            var bk = await fixture.CreateBackupAsync(pass);
            bk.Manifest.SchemaVersion.Should().Be("2.19");

            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
            File.Move(livePath, dbPath);

            await fixture.NewRestoreService().RestoreAsync(bk.Path, pass, new RestoreOptions(SkipSafetySnapshot: true));

            // Il restore lascia lo schema del backup; il prossimo avvio migra (CoreInitializer).
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
                await db.Database.MigrateAsync();

            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
            {
                var v = await db.CompletionValues.SingleAsync(x => x.Id == valueId);
                v.ValueDecimal.Should().Be(5m);
                v.ValueInteger.Should().BeNull();
                (await db.AppSettings.SingleAsync(s => s.Key == "schema_version")).Value.Should().Be("2.20");
            }
        }
        finally { await fixture.DisposeAsync(); }
    }

    // ── seme e letture in SQL grezzo ────────────────────────────────────────

    private static string G(Guid g) => g.ToString("D").ToUpperInvariant();

    private static async Task<SqliteConnection> OpenAt219Async()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();
        await using (TestDbHelper.CreateAtMigration(conn, "NoteTagAttachmentBlobV219")) { }
        return conn;
    }

    private static async Task MigrateAsync(SqliteConnection conn)
    {
        await using var db = TestDbHelper.CreateFull(conn);
        await db.Database.MigrateAsync();
    }

    private static async Task<Guid> HabitAsync(SqliteConnection conn, string name, string type)
    {
        var id = Guid.NewGuid();
        await TestDbHelper.RawInsertHabitNodeAsync(conn, id, name, type);
        return id;
    }

    private static async Task<Guid> ScaleAsync(SqliteConnection conn, string name, int min, int max, string labelsJson)
    {
        var id = Guid.NewGuid();
        await ExecAsync(conn, """
            INSERT INTO scale_templates (id, code, name, min_value, max_value, labels_json, is_official, created_at)
            VALUES ($id, $code, $name, $min, $max, $labels, 0, '2026-06-01T00:00:00Z')
            """, ("$id", G(id)), ("$code", "u-" + id.ToString("N")), ("$name", name), ("$min", min), ("$max", max),
            ("$labels", labelsJson));
        return id;
    }

    private static async Task<Guid> MetricAsync(SqliteConnection conn, Guid habit, string name, Guid? scale, string? labelsJson)
    {
        var id = Guid.NewGuid();
        await ExecAsync(conn, """
            INSERT INTO metric_definitions (id, habit_node_id, name, type, sort_order, scale_template_id, labels_json)
            VALUES ($id, $h, $name, 'Integer', 0, $scale, $labels)
            """, ("$id", G(id)), ("$h", G(habit)), ("$name", name),
            ("$scale", scale is { } s ? G(s) : DBNull.Value), ("$labels", (object?)labelsJson ?? DBNull.Value));
        return id;
    }

    /// <summary>Una registrazione con la posizione salvata come la salva la 2.19 (1-based in value_integer).</summary>
    private static async Task<Guid> ValueAsync(SqliteConnection conn, Guid habit, Guid metric, int position)
    {
        var completion = Guid.NewGuid();
        var value = Guid.NewGuid();
        await ExecAsync(conn, """
            INSERT INTO completions (id, habit_node_id, completed_at, completed_at_ms, is_retroactive)
            VALUES ($c, $h, '2026-06-01T12:00:00Z', 1748779200000, 0);
            INSERT INTO completion_values (id, completion_id, metric_definition_id, series_index, value_integer)
            VALUES ($v, $c, $m, 0, $p);
            """, ("$c", G(completion)), ("$h", G(habit)), ("$v", G(value)), ("$m", G(metric)), ("$p", position));
        return value;
    }

    private static async Task ExecAsync(SqliteConnection conn, string sql, params (string Name, object Value)[] args)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var r = await cmd.ExecuteScalarAsync();
        return r is DBNull ? null : r;
    }

    private static async Task<List<object?[]>> RowsAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<object?[]>();
        while (await reader.ReadAsync())
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    private static async Task<List<(long Value, string? Label, string? Tag)>> AnchorsWithTagsAsync(SqliteConnection conn, string scaleId) =>
        (await RowsAsync(conn, $"""
            SELECT a.value, a.label, t.name FROM scale_anchors a LEFT JOIN tags t ON t.id = a.tag_id
            WHERE a.scale_template_id = '{scaleId}' ORDER BY a.value
            """))
        .Select(r => ((long)r[0]!, (string?)r[1], (string?)r[2])).ToList();

    private static async Task<decimal?> DecimalOf(SqliteConnection conn, Guid valueId) =>
        await ScalarAsync(conn, $"SELECT value_decimal FROM completion_values WHERE id = '{G(valueId)}'") is { } v
            ? decimal.Parse(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)!, System.Globalization.CultureInfo.InvariantCulture)
            : null;
}
