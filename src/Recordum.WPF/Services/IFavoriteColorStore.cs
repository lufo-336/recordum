namespace Recordum.WPF.Services;

/// <summary>User-pinned favourite habit colours (UI state, persisted in AppSetting). Insertion order.</summary>
public interface IFavoriteColorStore
{
    ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken ct = default);
    ValueTask AddAsync(string hex, CancellationToken ct = default);
    ValueTask RemoveAsync(string hex, CancellationToken ct = default);
}
