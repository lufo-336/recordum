using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Recordum.Data.Sync;

/// <summary>
/// Converte una riga tra i valori grezzi salvati in SQLite e la rappresentazione JSON pubblica
/// del formato <c>recordum-sync</c>, decidendo i tipi dai metadati EF (<see cref="IModel"/>).
/// Le colonne <see cref="SyncFieldKind.Photo"/> non entrano nelle righe JSON ma viaggiano in <c>photos/</c>.
/// </summary>
public sealed class SyncRowCodec
{
    private readonly Dictionary<string, Dictionary<string, ColumnCodecKind>> _tableColumns = new(StringComparer.Ordinal);

    public SyncRowCodec(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName))
                continue;

            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            if (!_tableColumns.TryGetValue(tableName, out var colMap))
            {
                colMap = new Dictionary<string, ColumnCodecKind>(StringComparer.Ordinal);
                _tableColumns[tableName] = colMap;
            }

            foreach (var prop in entityType.GetProperties())
            {
                var colName = prop.GetColumnName(storeObject);
                if (string.IsNullOrEmpty(colName))
                    continue;

                colMap[colName] = ClassifyProperty(prop);
            }
        }
    }

    public SyncRow ToPublic(SyncEntity entity, IReadOnlyDictionary<string, object?> dbRow)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dbRow);

        var colMap = GetTableColumnMap(entity.Table);
        var id = BuildRowId(entity, dbRow);

        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            foreach (var field in entity.Fields)
            {
                if (field.Kind == SyncFieldKind.Photo)
                    continue;

                if (!dbRow.TryGetValue(field.Column, out var rawValue))
                    continue;

                if (!colMap.TryGetValue(field.Column, out var kind))
                    throw new InvalidOperationException($"Column '{entity.Table}.{field.Column}' is not present in EF model.");

                writer.WritePropertyName(field.PublicName);
                WriteJsonValue(writer, kind, rawValue);
            }

            writer.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(buffer.WrittenMemory);
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            fields[prop.Name] = prop.Value.Clone();
        }

        return new SyncRow(id, fields);
    }

    public IReadOnlyDictionary<string, object?> ToDb(SyncEntity entity, SyncRow row)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(row);

        var colMap = GetTableColumnMap(entity.Table);
        var dbRow = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var field in entity.Fields)
        {
            if (field.Kind == SyncFieldKind.Photo)
                continue;

            if (!row.Fields.TryGetValue(field.PublicName, out var element))
                continue;

            if (!colMap.TryGetValue(field.Column, out var kind))
                throw new InvalidOperationException($"Column '{entity.Table}.{field.Column}' is not present in EF model.");

            dbRow[field.Column] = ReadDbValue(entity.PublicName, field.PublicName, kind, element);
        }

        return dbRow;
    }

    private static string BuildRowId(SyncEntity entity, IReadOnlyDictionary<string, object?> dbRow)
    {
        if (entity.KeyColumns.Count == 1)
        {
            var keyCol = entity.KeyColumns[0];
            if (!dbRow.TryGetValue(keyCol, out var val) || val is null or DBNull)
                throw new ArgumentException($"Missing key column '{keyCol}' for entity '{entity.PublicName}'.", nameof(dbRow));

            return FormatRawAsString(val);
        }

        var parts = new string[entity.KeyColumns.Count];
        for (var i = 0; i < entity.KeyColumns.Count; i++)
        {
            var keyCol = entity.KeyColumns[i];
            if (!dbRow.TryGetValue(keyCol, out var val) || val is null or DBNull)
                throw new ArgumentException($"Missing key column '{keyCol}' for entity '{entity.PublicName}'.", nameof(dbRow));

            parts[i] = FormatRawAsString(val);
        }

        return string.Join("|", parts);
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, ColumnCodecKind kind, object? rawValue)
    {
        if (rawValue is null or DBNull)
        {
            writer.WriteNullValue();
            return;
        }

        switch (kind)
        {
            case ColumnCodecKind.Boolean:
            {
                var b = rawValue switch
                {
                    bool bv => bv,
                    long lv => lv != 0,
                    int iv => iv != 0,
                    _ => Convert.ToInt64(rawValue, CultureInfo.InvariantCulture) != 0,
                };
                writer.WriteBooleanValue(b);
                break;
            }
            case ColumnCodecKind.Integer:
            {
                var l = rawValue is long lv ? lv : Convert.ToInt64(rawValue, CultureInfo.InvariantCulture);
                writer.WriteNumberValue(l);
                break;
            }
            case ColumnCodecKind.Real:
            {
                var d = rawValue is double dv ? dv : Convert.ToDouble(rawValue, CultureInfo.InvariantCulture);
                writer.WriteNumberValue(d);
                break;
            }
            case ColumnCodecKind.Text:
            {
                writer.WriteStringValue(FormatRawAsString(rawValue));
                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported column codec kind '{kind}'.");
        }
    }

    private static object? ReadDbValue(string entityName, string fieldName, ColumnCodecKind kind, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
            return null;

        return kind switch
        {
            ColumnCodecKind.Boolean => element.ValueKind switch
            {
                JsonValueKind.True => 1L,
                JsonValueKind.False => 0L,
                _ => throw new SyncFormatException($"Field '{entityName}.{fieldName}' must be a boolean."),
            },
            ColumnCodecKind.Integer => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var l)
                ? l
                : throw new SyncFormatException($"Field '{entityName}.{fieldName}' must be an integer."),
            ColumnCodecKind.Real => element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var d)
                ? d
                : throw new SyncFormatException($"Field '{entityName}.{fieldName}' must be a number."),
            ColumnCodecKind.Text => element.ValueKind == JsonValueKind.String
                ? element.GetString()!
                : throw new SyncFormatException($"Field '{entityName}.{fieldName}' must be a string."),
            _ => throw new InvalidOperationException($"Unsupported column codec kind '{kind}'."),
        };
    }

    private static string FormatRawAsString(object rawValue) =>
        rawValue switch
        {
            string s => s,
            decimal dec => dec.ToString("0.0###########################", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => rawValue.ToString() ?? string.Empty,
        };

    private static ColumnCodecKind ClassifyProperty(IProperty property)
    {
        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

        if (clrType == typeof(bool))
            return ColumnCodecKind.Boolean;

        if (clrType == typeof(int) || clrType == typeof(long) || clrType == typeof(short) || clrType == typeof(byte))
            return ColumnCodecKind.Integer;

        if (clrType == typeof(double) || clrType == typeof(float))
            return ColumnCodecKind.Real;

        if (clrType == typeof(byte[]))
            return ColumnCodecKind.Blob;

        return ColumnCodecKind.Text;
    }

    private Dictionary<string, ColumnCodecKind> GetTableColumnMap(string tableName) =>
        _tableColumns.TryGetValue(tableName, out var map)
            ? map
            : throw new InvalidOperationException($"Table '{tableName}' is not present in EF model.");

    private enum ColumnCodecKind
    {
        Boolean,
        Integer,
        Real,
        Text,
        Blob,
    }
}
