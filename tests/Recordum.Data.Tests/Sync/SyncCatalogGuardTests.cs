using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Recordum.Data.SeedData;
using Recordum.Data.Sync;
using Recordum.Data.Tests.Fixtures;

namespace Recordum.Data.Tests.Sync;

public sealed partial class SyncCatalogGuardTests
{
    private static readonly string[] ExpectedLocalTables =
    [
        "active_timer",
        "backup_log",
        "export_jobs",
        "ai_insights",
        "sync_meta",
        "sync_tombstone",
        "sync_base",
        "sync_field_ms",
    ];

    private static readonly string[] ExpectedTravellingPublicNames =
    [
        "habit_node",
        "habit_config",
        "habit_config_history",
        "unit_of_measure",
        "scale_template",
        "scale_anchor",
        "metric_definition",
        "segment_definition",
        "completion",
        "completion_value",
        "completion_segment",
        "streak_pause",
        "abstinence_period",
        "note",
        "note_history",
        "attachment",
        "goal",
        "tag",
        "habit_node_tag",
        "note_tag",
        "habit_template",
        "export_template",
        "chart",
        "chart_series",
        "notification",
        "app_setting",
    ];

    private static readonly string[] ExpectedTravellingSettingKeys =
    [
        "daily_reset_time",
        "monthly_summary_day",
        "monthly_summary_time",
        SyncService.AcceptedRepairsSettingKey,
    ];

    private static readonly string[] ExpectedTextColumns =
    [
        "notes.body_md",
        "completions.notes",
    ];

    private static readonly string[] ExpectedPhotoColumns =
    [
        "attachment.content",
        "attachment.thumbnail",
    ];

    private static readonly string[] ExpectedHistoryEntities =
    [
        "habit_config_history",
        "note_history",
    ];

    private static readonly string[] TagNaturalKey = ["name"];
    private static readonly string[] CodeNaturalKey = ["code"];
    private static readonly string[] ScaleAnchorNaturalKey = ["scale_template_id", "value"];

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex SnakeCaseRegex();

    [Fact]
    public async Task EveryEntityTypeIsClassified()
    {
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();

        var travellingTables = SyncCatalog.Entities.Select(e => e.Table).ToList();
        travellingTables.Should().OnlyHaveUniqueItems();

        var travellingSet = travellingTables.ToHashSet(StringComparer.Ordinal);
        travellingSet.Intersect(SyncCatalog.LocalTables, StringComparer.Ordinal).Should().BeEmpty(
            "nessuna tabella può stare sia fra quelle che viaggiano sia in LocalTables");

        var modelTables = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .ToList();
        modelTables.Should().NotContainNulls();

        foreach (var table in modelTables)
        {
            var classified = travellingSet.Contains(table!) || SyncCatalog.LocalTables.Contains(table!);
            classified.Should().BeTrue($"la tabella '{table}' del modello EF deve essere classificata in SyncCatalog");
        }
    }

    [Fact]
    public async Task EveryLocalTableExistsInTheModel()
    {
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();

        var modelTables = db.Model.GetEntityTypes().Select(e => e.GetTableName()).ToHashSet(StringComparer.Ordinal);
        foreach (var table in SyncCatalog.LocalTables)
        {
            modelTables.Should().Contain(table, $"'{table}' in LocalTables deve essere una tabella del modello EF (niente nomi morti)");
        }
    }

    [Fact]
    public async Task TravellingAndLocalSetsMatchSpec()
    {
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();

        SyncCatalog.LocalTables.Should().BeEquivalentTo(ExpectedLocalTables);
        SyncCatalog.Entities.Select(e => e.PublicName).Should().BeEquivalentTo(ExpectedTravellingPublicNames);

        var modelTravellingTables = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName()!)
            .Where(t => !SyncCatalog.LocalTables.Contains(t))
            .ToList();

        SyncCatalog.Entities.Select(e => e.Table).Should().BeEquivalentTo(modelTravellingTables);
    }

    [Fact]
    public async Task EveryColumnOfTravellingTablesIsMappedOrExcluded()
    {
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();

        var byTable = db.Model.GetEntityTypes().ToDictionary(e => e.GetTableName()!, StringComparer.Ordinal);
        var allRealColumns = new HashSet<string>(StringComparer.Ordinal);
        var allCatalogPending = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entity in SyncCatalog.Entities)
        {
            byTable.Should().ContainKey(entity.Table);
            var entityType = byTable[entity.Table];
            var storeObject = StoreObjectIdentifier.Table(entity.Table, entityType.GetSchema());

            var realCols = entityType.GetProperties()
                .Select(p => p.GetColumnName(storeObject)!)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var col in realCols)
            {
                allRealColumns.Add($"{entity.Table}.{col}");
            }

            var mappedCols = entity.Fields.Select(f => f.Column).ToHashSet(StringComparer.Ordinal);

            foreach (var col in realCols)
            {
                var key = $"{entity.Table}.{col}";
                var isMapped = mappedCols.Contains(col);
                var isExcluded = SyncCatalog.ExcludedColumns.TryGetValue(key, out var reason)
                    && !string.IsNullOrWhiteSpace(reason);

                (isMapped || isExcluded).Should().BeTrue(
                    $"la colonna '{key}' deve essere mappata in SyncCatalog o esclusa con una ragione");
                (isMapped && isExcluded).Should().BeFalse(
                    $"la colonna '{key}' non può essere sia mappata sia esclusa");
            }

            foreach (var field in entity.Fields)
            {
                var key = $"{entity.Table}.{field.Column}";
                if (realCols.Contains(field.Column))
                {
                    SyncCatalog.PendingColumns.Contains(key).Should().BeFalse(
                        $"la colonna '{key}' esiste già nel modello e va rimossa da PendingColumns");
                }
                else
                {
                    SyncCatalog.PendingColumns.Contains(key).Should().BeTrue(
                        $"la colonna '{key}' non esiste ancora nel modello e deve stare in PendingColumns fino al Task 2");
                    allCatalogPending.Add(key);
                }
            }

            var pkCols = entityType.FindPrimaryKey()!.Properties
                .Select(p => p.GetColumnName(storeObject)!)
                .ToList();
            entity.KeyColumns.Should().Equal(pkCols, $"KeyColumns di '{entity.Table}' deve combaciare con la PK");
            foreach (var keyCol in entity.KeyColumns)
            {
                mappedCols.Contains(keyCol).Should().BeTrue(
                    $"la colonna chiave '{entity.Table}.{keyCol}' deve essere mappata");
            }
        }

        SyncCatalog.PendingColumns.Should().BeSubsetOf(allCatalogPending,
            "ogni voce in PendingColumns deve corrispondere a un campo dichiarato nel catalogo");

        foreach (var (key, reason) in SyncCatalog.ExcludedColumns)
        {
            reason.Should().NotBeNullOrWhiteSpace();
            allRealColumns.Should().Contain(key);
        }
    }

    [Fact]
    public void PublicNamesAreUniqueSnakeCase()
    {
        var entityNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entity in SyncCatalog.Entities)
        {
            AssertSnakeCase(entity.PublicName);
            entityNames.Add(entity.PublicName).Should().BeTrue($"nome pubblico entità duplicato: '{entity.PublicName}'");
            SyncCatalog.Get(entity.PublicName).Should().BeSameAs(entity);

            var fieldPublicNames = new HashSet<string>(StringComparer.Ordinal);
            var fieldColumns = new HashSet<string>(StringComparer.Ordinal);

            foreach (var field in entity.Fields)
            {
                AssertSnakeCase(field.PublicName);
                fieldPublicNames.Add(field.PublicName).Should().BeTrue(
                    $"nome pubblico campo duplicato in '{entity.PublicName}': '{field.PublicName}'");
                fieldColumns.Add(field.Column).Should().BeTrue(
                    $"colonna duplicata in '{entity.Table}': '{field.Column}'");
            }
        }
    }

    [Fact]
    public async Task EveryUniqueIndexIsClassified()
    {
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();

        var byTable = db.Model.GetEntityTypes().ToDictionary(e => e.GetTableName()!, StringComparer.Ordinal);
        var seenExceptions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entity in SyncCatalog.Entities)
        {
            var entityType = byTable[entity.Table];
            var storeObject = StoreObjectIdentifier.Table(entity.Table, entityType.GetSchema());

            foreach (var index in entityType.GetIndexes().Where(i => i.IsUnique))
            {
                var cols = index.Properties.Select(p => p.GetColumnName(storeObject)!).ToArray();
                var matchesNaturalKey = entity.NaturalKey.Count > 0
                    && entity.NaturalKey.SequenceEqual(cols, StringComparer.Ordinal);

                if (matchesNaturalKey) continue;

                var exceptionKey = $"{entity.Table}.{string.Join(",", cols)}";
                SyncCatalog.UniqueIndexExceptions.TryGetValue(exceptionKey, out var reason).Should().BeTrue(
                    $"l'indice unico '{exceptionKey}' deve essere coperto da NaturalKey o da UniqueIndexExceptions");
                reason.Should().NotBeNullOrWhiteSpace();
                seenExceptions.Add(exceptionKey);
            }
        }

        SyncCatalog.UniqueIndexExceptions.Keys.Should().BeEquivalentTo(seenExceptions);
    }

    [Fact]
    public async Task NaturalKeysMatchSpec()
    {
        SyncCatalog.Get("tag").NaturalKey.Should().Equal(TagNaturalKey);
        SyncCatalog.Get("unit_of_measure").NaturalKey.Should().Equal(CodeNaturalKey);
        SyncCatalog.Get("scale_template").NaturalKey.Should().Equal(CodeNaturalKey);
        SyncCatalog.Get("scale_anchor").NaturalKey.Should().Equal(ScaleAnchorNaturalKey);

        foreach (var entity in SyncCatalog.Entities)
        {
            if (entity.PublicName is "tag" or "unit_of_measure" or "scale_template" or "scale_anchor") continue;
            entity.NaturalKey.Should().BeEmpty($"'{entity.PublicName}' non dovrebbe avere una NaturalKey");
        }

        // Tag.Name usa la collation NOCASE su SQLite (confronto senza maiuscole).
        await using var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var tagEntity = db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetEntityTypes().Single(e => e.GetTableName() == "tags");
        var tagStore = StoreObjectIdentifier.Table("tags", tagEntity.GetSchema());
        var nameProp = tagEntity.GetProperties().Single(p => p.GetColumnName(tagStore) == "name");
        nameProp.GetCollation().Should().Be("NOCASE");
    }

    [Fact]
    public void TextFieldsAreExactly()
    {
        var textFields = SyncCatalog.Entities
            .SelectMany(e => e.Fields
                .Where(f => f.Kind == SyncFieldKind.Text)
                .Select(f => $"{e.Table}.{f.Column}"))
            .ToList();

        var photoFields = SyncCatalog.Entities
            .SelectMany(e => e.Fields
                .Where(f => f.Kind == SyncFieldKind.Photo)
                .Select(f => $"{e.Table}.{f.Column}"))
            .ToList();

        var historyEntities = SyncCatalog.Entities
            .Where(e => e.IsHistory)
            .Select(e => e.PublicName)
            .ToList();

        textFields.Should().BeEquivalentTo(ExpectedTextColumns);
        photoFields.Should().BeEquivalentTo(ExpectedPhotoColumns);
        historyEntities.Should().BeEquivalentTo(ExpectedHistoryEntities);
    }

    [Fact]
    public void EverySeededSettingKeyIsClassified()
    {
        SyncCatalog.TravellingSettingKeys.Should().BeEquivalentTo(ExpectedTravellingSettingKeys);
        SyncCatalog.TravellingSettingKeys
            .Intersect(SyncCatalog.LocalSettingKeys, StringComparer.Ordinal)
            .Should().BeEmpty();

        var seededKeys = AppSettingSeed.All.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in seededKeys)
        {
            var classified = SyncCatalog.TravellingSettingKeys.Contains(key)
                || SyncCatalog.LocalSettingKeys.Contains(key);
            classified.Should().BeTrue($"la chiave seed '{key}' deve essere classificata in SyncCatalog");
        }

        var allClassified = SyncCatalog.TravellingSettingKeys
            .Union(SyncCatalog.LocalSettingKeys, StringComparer.Ordinal);
        allClassified.Should().BeEquivalentTo([.. seededKeys, SyncService.AcceptedRepairsSettingKey]);
    }

    private static void AssertSnakeCase(string name)
    {
        SnakeCaseRegex().IsMatch(name).Should().BeTrue($"'{name}' non è snake_case");
        name.Contains("__", StringComparison.Ordinal).Should().BeFalse($"'{name}' non può avere doppi underscore");
        name.EndsWith('_').Should().BeFalse($"'{name}' non può terminare con underscore");
    }
}
