using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.Settings;

/// <summary>"General" settings category: the app-wide daily reset time (AppSetting daily_reset_time,
/// HH:mm) and the two culture knobs (F0-6). Culture lives here rather than under Appearance because it
/// changes how the app <em>behaves</em> — which strings it reads, how it writes dates, and which
/// decimal separator its numeric fields accept — not how it looks.</summary>
public sealed partial class GeneralSettingsViewModel(
    IAppSettingService settings, ICultureService cultures, IClock clock) : ObservableObject
{
    internal const string DailyResetTimeKey = "daily_reset_time";
    private static readonly CompositeFormat PreviewFormat = CompositeFormat.Parse(Strings.Settings_Format_Preview);
    private bool _loading;

    [ObservableProperty] private string _dailyResetTime = "00:00";
    [ObservableProperty] private bool _hasError;

    /// <summary>Which .resx the UI reads. Separate from <see cref="RegionalFormat"/> the way Windows
    /// separates them: an Italian user who wants the app in English still writes 2,5.</summary>
    [ObservableProperty] private AppCulture _uiLanguage;

    /// <summary>Which culture formats dates and numbers, and therefore which decimal separator the
    /// numeric fields accept (W2V-17c).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormatPreview))]
    private AppCulture _regionalFormat;

    /// <summary>A worked example of the selected format, updated as the radio buttons move. A label
    /// reading "2,5" states a fact without saying what it is a fact about; this names both the date and
    /// the decimal number, in the culture being chosen, using today's real date.</summary>
    public string FormatPreview
    {
        get
        {
            var culture = CultureBootstrap.Resolve(RegionalFormat, CultureInfo.CurrentCulture);
            var today = clock.UtcNow.UtcDateTime.ToString("d", culture);
            var number = 2.5m.ToString("0.#", culture);
            return string.Format(CultureInfo.CurrentCulture, PreviewFormat, today, number);
        }
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        _loading = true;
        try
        {
            DailyResetTime = await settings.GetAsync(DailyResetTimeKey, ct).ConfigureAwait(true) ?? "00:00";
            await cultures.InitializeAsync(ct).ConfigureAwait(true);
            UiLanguage = cultures.Language;
            RegionalFormat = cultures.Format;
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

    partial void OnUiLanguageChanged(AppCulture value)
    {
        if (_loading) return;
        _ = cultures.SetLanguageAsync(value).AsTask();
    }

    partial void OnRegionalFormatChanged(AppCulture value)
    {
        if (_loading) return;
        _ = cultures.SetFormatAsync(value).AsTask();
    }
}
