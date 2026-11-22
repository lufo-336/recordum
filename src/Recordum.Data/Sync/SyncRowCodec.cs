using System.Buffers;
using System.Globalization;
using System.Text;
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
    private readonly Dictionary<string, Dictionary<string, ColumnInfo>> _tableColumns = new(StringComparer.Ordinal);

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
                colMap = new Dictionary<string, ColumnInfo>(StringComparer.Ordinal);
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

                if (!colMap.TryGetValue(field.Column, out var info))
                    throw new InvalidOperationException($"Column '{entity.Table}.{field.Column}' is not present in EF model.");

                writer.WritePropertyName(field.PublicName);
                WriteJsonValue(writer, info.Kind, rawValue);
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

            if (!colMap.TryGetValue(field.Column, out var info))
                throw new InvalidOperationException($"Column '{entity.Table}.{field.Column}' is not present in EF model.");

            dbRow[field.Column] = ReadDbValue(entity.PublicName, field.PublicName, info, element);
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

    private static object? ReadDbValue(string entityName, string fieldName, ColumnInfo info, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
            return null;

        var where = $"Field '{entityName}.{fieldName}'";
        switch (info.Kind)
        {
            case ColumnCodecKind.Boolean:
                return element.ValueKind switch
                {
                    JsonValueKind.True => 1L,
                    JsonValueKind.False => 0L,
                    _ => throw new SyncFormatException($"{where} must be a boolean."),
                };
            case ColumnCodecKind.Integer:
            {
                if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var l))
                    throw new SyncFormatException($"{where} must be an integer.");
                var fits = info.ProviderType == typeof(int) ? l is >= int.MinValue and <= int.MaxValue
                    : info.ProviderType == typeof(short) ? l is >= short.MinValue and <= short.MaxValue
                    : info.ProviderType != typeof(byte) || l is >= byte.MinValue and <= byte.MaxValue;
                return fits ? l : throw new SyncFormatException($"{where} is out of range for {info.ProviderType.Name}.");
            }
            case ColumnCodecKind.Real:
                return element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var d)
                    ? d
                    : throw new SyncFormatException($"{where} must be a number.");
            case ColumnCodecKind.Text:
            {
                if (element.ValueKind != JsonValueKind.String)
                    throw new SyncFormatException($"{where} must be a string.");
                var text = element.GetString()!;
                return IsCanonicalText(info.ModelType, text) ? text : throw new SyncFormatException($"{where} is not a valid {info.ModelType.Name} in canonical form.");
            }
            default:
                throw new InvalidOperationException($"Unsupported column codec kind '{info.Kind}'.");
        }
    }

    // Il testo deve coincidere con la forma salvata da EF: si interpreta con il formato esatto e lo si riscrive.
    private static bool IsCanonicalText(Type type, string text)
    {
        var inv = CultureInfo.InvariantCulture;
        if (type == typeof(Guid))
            return Guid.TryParseExact(text, "D", out var g) && string.Equals(g.ToString("D").ToUpperInvariant(), text, StringComparison.Ordinal);
        if (type == typeof(DateTime))
            return DateTime.TryParseExact(text, DateTimeFormat, inv, DateTimeStyles.None, out var dt) && dt.ToString(DateTimeFormat, inv) == text;
        if (type == typeof(DateOnly))
            return DateOnly.TryParseExact(text, DateOnlyFormat, inv, DateTimeStyles.None, out var d) && d.ToString(DateOnlyFormat, inv) == text;
        if (type == typeof(TimeOnly))
            return TimeOnly.TryParseExact(text, TimeOnlyFormat, inv, DateTimeStyles.None, out var t) && t.ToString(TimeOnlyFormat, inv) == text;
        if (type == typeof(decimal))
            return decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, inv, out var m) && m.ToString(inv) == text;
        if (type.IsEnum)
            return Array.IndexOf(Enum.GetNames(type), text) >= 0;
        return true;
    }

    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.FFFFFFF";
    private const string DateOnlyFormat = "yyyy-MM-dd";
    private const string TimeOnlyFormat = "HH:mm:ss.FFFFFFF";

    /// <summary>
    /// Forma canonica di una riga (per lo spareggio di §8.3): i campi senza <c>id</c>, ordinati per nome
    /// (byte UTF-8, ordinale), ciascuno come <c>nome 0x00 valore 0x00</c> in UTF-8.
    /// Valore: testo della stringa; numero come cifre intere o double "R" invariante; <c>true</c>/<c>false</c>/<c>null</c>.
    /// </summary>
    public static byte[] Canonical(SyncRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var utf8 = new UTF8Encoding(false);
        var names = row.Fields.Keys.Where(k => k != "id").Select(k => (Name: k, Bytes: utf8.GetBytes(k))).ToList();
        names.Sort(static (a, b) => a.Bytes.AsSpan().SequenceCompareTo(b.Bytes));

        using var ms = new MemoryStream();
        foreach (var (name, nameBytes) in names)
        {
            var el = row.Fields[name];
            var value = el.ValueKind switch
            {
                JsonValueKind.String => el.GetString()!,
                JsonValueKind.Number => el.TryGetInt64(out var l)
                    ? l.ToString(CultureInfo.InvariantCulture)
                    : el.GetDouble().ToString("R", CultureInfo.InvariantCulture),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => el.GetRawText(),
            };
            ms.Write(nameBytes);
            ms.WriteByte(0);
            ms.Write(utf8.GetBytes(value));
            ms.WriteByte(0);
        }

        return ms.ToArray();
    }

    private static string FormatRawAsString(object rawValue) =>
        rawValue switch
        {
            string s => s,
            long or int or short or byte => Convert.ToString(rawValue, CultureInfo.InvariantCulture)!,
            _ => throw new InvalidOperationException($"Unexpected stored value type '{rawValue.GetType().Name}'."),
        };

    private static ColumnInfo ClassifyProperty(IProperty property)
    {
        var modelType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        var providerType = property.GetProviderClrType() ?? property.ClrType;
        providerType = Nullable.GetUnderlyingType(providerType) ?? providerType;

        var kind = providerType == typeof(bool) ? ColumnCodecKind.Boolean
            : providerType == typeof(int) || providerType == typeof(long) || providerType == typeof(short) || providerType == typeof(byte) ? ColumnCodecKind.Integer
            : providerType == typeof(double) || providerType == typeof(float) ? ColumnCodecKind.Real
            : providerType == typeof(byte[]) ? ColumnCodecKind.Blob
            : ColumnCodecKind.Text;
        return new ColumnInfo(kind, modelType, providerType);
    }

    private readonly record struct ColumnInfo(ColumnCodecKind Kind, Type ModelType, Type ProviderType);

    private Dictionary<string, ColumnInfo> GetTableColumnMap(string tableName) =>
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
