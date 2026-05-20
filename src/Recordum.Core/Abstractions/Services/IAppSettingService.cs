namespace Recordum.Core.Abstractions.Services;

public interface IAppSettingService
{
    ValueTask<string?> GetAsync(string key, CancellationToken ct = default);
    ValueTask SetAsync(string key, string value, CancellationToken ct = default);
    ValueTask<T?> GetEnumAsync<T>(string key, CancellationToken ct = default) where T : struct, Enum;
    ValueTask SetEnumAsync<T>(string key, T value, CancellationToken ct = default) where T : struct, Enum;
}
