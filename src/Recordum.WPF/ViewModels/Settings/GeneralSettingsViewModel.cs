using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.ViewModels.Settings;

/// <summary>"General" settings category: edits the app-wide daily reset time (AppSetting
/// daily_reset_time, HH:mm). Saves on change when the value parses; flags HasError otherwise.</summary>
public sealed partial class GeneralSettingsViewModel(IAppSettingService settings) : ObservableObject
{
    internal const string DailyResetTimeKey = "daily_reset_time";
    private bool _loading;

    [ObservableProperty] private string _dailyResetTime = "00:00";
    [ObservableProperty] private bool _hasError;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        _loading = true;
        try
        {
            DailyResetTime = await settings.GetAsync(DailyResetTimeKey, ct).ConfigureAwait(true) ?? "00:00";
            HasError = false;
        }
        finally { _loading = false; }
    }

    partial void OnDailyResetTimeChanged(string value)
    {
        if (_loading) return;
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            HasError = true;
            return;
        }
        HasError = false;
        _ = settings.SetAsync(DailyResetTimeKey, value);
    }
}
