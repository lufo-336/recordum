namespace Recordum.Presentation.Services;

/// <summary>Recently-picked habit colors (UI state, persisted in AppSetting). Most-recent-first.</summary>
public interface IRecentColorStore
{
    ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken ct = default);

    /// <param name="hex">A <c>#RRGGBB</c> color; null or invalid hex is silently ignored (no-op).</param>
    ValueTask AddAsync(string hex, CancellationToken ct = default);
}
