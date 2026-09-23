using Recordum.Core.Abstractions.Services;

namespace Recordum.UI.Tests.Services;

/// <summary>Copia locale del doppio già in <c>Recordum.Presentation.Tests.Fixtures</c>: i due progetti
/// di test non si referenziano a vicenda (convenzione del repo, v. Recordum.WPF.Tests), quindi il
/// doppio si duplica invece di condividerlo.</summary>
public sealed class FakeAppSettingService : IAppSettingService
{
    private readonly Dictionary<string, string> _store = new();

    public ValueTask<string?> GetAsync(string key, CancellationToken ct = default) =>
        new(_store.TryGetValue(key, out var v) ? v : null);

    public ValueTask SetAsync(string key, string value, CancellationToken ct = default)
    {
        _store[key] = value;
        return ValueTask.CompletedTask;
    }

    public async ValueTask<T?> GetEnumAsync<T>(string key, CancellationToken ct = default) where T : struct, Enum
    {
        var raw = await GetAsync(key, ct);
        return raw is null ? null : Enum.TryParse<T>(raw, ignoreCase: true, out var parsed) ? parsed : null;
    }

    public ValueTask SetEnumAsync<T>(string key, T value, CancellationToken ct = default) where T : struct, Enum =>
        SetAsync(key, value.ToString(), ct);
}
