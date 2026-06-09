using System.Text.RegularExpressions;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.Services;

/// <summary>Stores up to 8 recently-picked #RRGGBB colors as a CSV AppSetting, most-recent-first,
/// de-duplicated. Invalid hex is ignored.</summary>
public sealed partial class RecentColorStore : IRecentColorStore
{
    internal const string SettingKey = "ui.recent_colors";
    internal const int Cap = 8;

    private readonly IAppSettingService _settings;
    public RecentColorStore(IAppSettingService settings) => _settings = settings;

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexPattern();

    public async ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken ct = default)
    {
        var raw = await _settings.GetAsync(SettingKey, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async ValueTask AddAsync(string hex, CancellationToken ct = default)
    {
        if (hex is null || !HexPattern().IsMatch(hex)) return;
        var norm = hex.ToLowerInvariant();
        var list = new List<string>(await GetAsync(ct).ConfigureAwait(false));
        list.RemoveAll(c => string.Equals(c, norm, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, norm);
        if (list.Count > Cap) list.RemoveRange(Cap, list.Count - Cap);
        await _settings.SetAsync(SettingKey, string.Join(',', list), ct).ConfigureAwait(false);
    }
}
