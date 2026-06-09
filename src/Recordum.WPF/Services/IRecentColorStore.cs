namespace Recordum.WPF.Services;

/// <summary>Recently-picked habit colors (UI state, persisted in AppSetting). Most-recent-first.</summary>
public interface IRecentColorStore
{
    ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken ct = default);
    ValueTask AddAsync(string hex, CancellationToken ct = default);
}
