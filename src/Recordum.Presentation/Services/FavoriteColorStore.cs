using System.Text.RegularExpressions;
using Recordum.Core.Abstractions.Services;

namespace Recordum.Presentation.Services;

/// <summary>Up to 12 pinned #RRGGBB favourites as a CSV AppSetting, insertion order, de-duplicated.</summary>
public sealed partial class FavoriteColorStore : IFavoriteColorStore
{
    internal const string SettingKey = "ui.favorite_colors";
    internal const int Cap = 12;

    private readonly IAppSettingService _settings;
    public FavoriteColorStore(IAppSettingService settings) => _settings = settings;

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexPattern();

    public async ValueTask<IReadOnlyList<string>> GetAsync(CancellationToken ct = default)
    {
        var raw = await _settings.GetAsync(SettingKey, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Where(c => HexPattern().IsMatch(c))
                  .Select(c => c.ToLowerInvariant())
                  .ToArray();
    }

    public async ValueTask AddAsync(string hex, CancellationToken ct = default)
    {
        if (hex is null || !HexPattern().IsMatch(hex)) return;
        var norm = hex.ToLowerInvariant();
        var list = new List<string>(await GetAsync(ct).ConfigureAwait(false));
        if (list.Contains(norm)) return;             // keep position; pinning is order-stable
        list.Add(norm);
        if (list.Count > Cap) list.RemoveRange(Cap, list.Count - Cap);
        await _settings.SetAsync(SettingKey, string.Join(',', list), ct).ConfigureAwait(false);
    }

    public async ValueTask RemoveAsync(string hex, CancellationToken ct = default)
    {
        if (hex is null) return;
        var norm = hex.ToLowerInvariant();
        var list = new List<string>(await GetAsync(ct).ConfigureAwait(false));
        if (list.RemoveAll(c => string.Equals(c, norm, StringComparison.Ordinal)) == 0) return;
        await _settings.SetAsync(SettingKey, string.Join(',', list), ct).ConfigureAwait(false);
    }
}
