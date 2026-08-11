using System.Text.Json;
using System.Text.Json.Serialization;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

/// <summary>Contratto JSON di ui_prefs_json (camelCase, enum come stringhe, null omessi —
/// docs/ARCHITECTURE.md). Unico punto: HabitUiPrefsService e ComposedHabitService DEVONO
/// condividere queste options, o il round-trip diverge.</summary>
internal static class HabitUiPrefsJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static string Serialize(HabitUiPrefs prefs) => JsonSerializer.Serialize(prefs, Options);

    internal static HabitUiPrefs Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new HabitUiPrefs()
            : JsonSerializer.Deserialize<HabitUiPrefs>(json, Options) ?? new HabitUiPrefs();
}
