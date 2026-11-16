using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitBuilder.Preview;
using Recordum.Presentation.ViewModels.Home;

namespace Recordum.Presentation.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _theme;
    private readonly IAppFontService _fonts;
    private readonly HomeRowsPreference? _rows;
    private bool _initializing;

    [ObservableProperty] private ThemeMode _themeMode;
    [ObservableProperty] private AppFont _appFont;

    /// <summary>Le righe della Home (Impostazioni › Aspetto). Senza preferenza (WPF) vale Larga e non si salva.</summary>
    [ObservableProperty] private HomeRows _homeRows = HomeRows.Comfortable;

    public SettingsViewModel(IThemeService theme, IAppFontService fonts, HomeRowsPreference? rows = null)
    {
        _theme = theme;
        _fonts = fonts;
        _rows = rows;
    }

    /// <summary>La riga d'esempio della pagina Aspetto, disegnata in ognuna delle tre versioni: un contatore, sette
    /// giorni, oggi 35 (il giorno scelto).</summary>
    public HomeRowPreviewModel HomeRowsSample { get; } = BuildSample();

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        _initializing = true;
        try
        {
            ThemeMode = _theme.CurrentMode;
            AppFont = _fonts.CurrentFont;
            HomeRows = _rows?.Current ?? HomeRows.Comfortable;
        }
        finally
        {
            _initializing = false;
        }
        await ValueTask.CompletedTask;
    }

    partial void OnThemeModeChanged(ThemeMode value)
    {
        if (_initializing) return;
        _ = _theme.SetModeAsync(value).AsTask();
    }

    partial void OnAppFontChanged(AppFont value)
    {
        if (_initializing) return;
        _ = _fonts.SetFontAsync(value).AsTask();
    }

    partial void OnHomeRowsChanged(HomeRows value)
    {
        if (_initializing || _rows is null) return;
        _ = _rows.SetAsync(value).AsTask();
    }

    /// <summary>I sette giorni sopra la riga d'esempio: una settimana qualunque che finisce oggi (di lunedì). È la testa
    /// a distinguere Compatta (iniziali) da Larga (i nomi): senza, le due anteprime erano la stessa riga (GIO-19).</summary>
    public ImmutableArray<Home.DayHeaderCell> HomeRowsSampleDays { get; } = BuildSampleDays();

    private static ImmutableArray<Home.DayHeaderCell> BuildSampleDays()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        DayOfWeek[] week = [DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
            DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday];
        return week.Select((day, i) =>
        {
            var name = culture.DateTimeFormat.GetAbbreviatedDayName(day);
            var today = i == week.Length - 1;
            return new Home.DayHeaderCell(today ? Resources.Strings.Home_StripHeader_Today : name,
                culture.TextInfo.ToLower(name[..1]), culture.DateTimeFormat.GetDayName(day), week.Length - 1 - i, today, today);
        }).ToImmutableArray();
    }

    private static HomeRowPreviewModel BuildSample()
    {
        decimal?[] days = [20, null, 30, 25, null, 40, 35];
        var max = days.Max() ?? 1m;
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        var cells = days.Select((v, i) => new StripCell(
            v is { } d ? d.ToString(culture) : "·", v is not null, IsToday: i == days.Length - 1, IsSelected: i == days.Length - 1));
        return new HomeRowPreviewModel(
            Name: Resources.Strings.Settings_Appearance_HomeRows_Sample,
            Color: "#3f9fe0",
            Icon: "Icon.BookOpen",
            KeyFigure: "35",
            StripStyle: StripStyle.Bars,
            Counts: days.Select(v => v is null ? 0 : 1).ToImmutableArray(),
            Fractions: days.Select(v => v is { } d ? (double)(d / max) : 0d).ToImmutableArray(),
            Values: cells.ToImmutableArray());
    }
}
