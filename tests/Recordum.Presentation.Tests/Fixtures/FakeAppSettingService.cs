using Recordum.Core.Abstractions.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeAppSettingService : IAppSettingService
{
    public Dictionary<string, string> Store { get; } = new();

    /// <summary>Number of <see cref="SetAsync"/> calls — lets tests assert write storms / suppression.</summary>
    public int SetCount { get; private set; }

    public ValueTask<string?> GetAsync(string key, CancellationToken ct = default) =>
        new ValueTask<string?>(Store.TryGetValue(key, out var v) ? v : null);

    public Exception? ThrowOnSet { get; set; }

    public ValueTask SetAsync(string key, string value, CancellationToken ct = default)
    {
        if (ThrowOnSet is { } boom) throw boom;
        SetCount++;
        Store[key] = value;
        return ValueTask.CompletedTask;
    }

    public async ValueTask<T?> GetEnumAsync<T>(string key, CancellationToken ct = default)
        where T : struct, Enum
    {
        var raw = await GetAsync(key, ct);
        return raw is null ? null : Enum.TryParse<T>(raw, ignoreCase: true, out var parsed) ? parsed : null;
    }

    public ValueTask SetEnumAsync<T>(string key, T value, CancellationToken ct = default)
        where T : struct, Enum
        => SetAsync(key, value.ToString(), ct);
}
