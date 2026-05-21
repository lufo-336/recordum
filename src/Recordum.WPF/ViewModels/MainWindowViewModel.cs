using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.Timer;

namespace Recordum.WPF.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    internal const string EditorPaneWidthSettingKey = "ui.editor_pane_width";
    internal const double DefaultEditorPaneWidth = 480d;
    internal const double MinEditorPaneWidth = 320d;
    internal const double MaxEditorPaneWidth = 900d;

    internal const string HabitScreenWidthSettingKey = "ui.habit_screen_width";
    internal const double DefaultHabitScreenWidth = 520d;
    internal const double MinHabitScreenWidth = 360d;
    internal const double MaxHabitScreenWidth = 800d;

    private readonly IAppSettingService _appSettings;

    public HomeViewModel Home { get; }
    public SettingsViewModel Settings { get; }
    public HabitEditorViewModel Editor { get; }
    public HabitScreenViewModel HabitScreen { get; }
    public TimerStatusBarViewModel TimerStatusBar { get; }

    [ObservableProperty] private double _editorPaneWidth = DefaultEditorPaneWidth;
    [ObservableProperty] private double _habitScreenWidth = DefaultHabitScreenWidth;

    public MainWindowViewModel(
        HomeViewModel home,
        SettingsViewModel settings,
        HabitEditorViewModel editor,
        IAppSettingService appSettings,
        TimerStatusBarViewModel timerStatusBar,
        HabitScreenViewModel habitScreen)
    {
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(timerStatusBar);
        ArgumentNullException.ThrowIfNull(habitScreen);
        Home = home;
        Settings = settings;
        Editor = editor;
        _appSettings = appSettings;
        TimerStatusBar = timerStatusBar;
        HabitScreen = habitScreen;
        Home.PropertyChanged += OnHomePropertyChanged;
        HabitScreen.ConfigureRequested += OnHabitScreenConfigureRequested;
    }

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        await LoadEditorPaneWidthAsync(ct).ConfigureAwait(true);
        await LoadHabitScreenWidthAsync(ct).ConfigureAwait(true);
        await Settings.InitializeAsync(ct);
        await Home.LoadCommand.ExecuteAsync(null);
        await TimerStatusBar.LoadAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand]
    private static void Quit() => System.Windows.Application.Current?.Shutdown();

    public async Task CommitEditorPaneWidthAsync(CancellationToken ct = default)
    {
        var clamped = Math.Clamp(EditorPaneWidth, MinEditorPaneWidth, MaxEditorPaneWidth);
        if (Math.Abs(clamped - EditorPaneWidth) > 0.5)
            EditorPaneWidth = clamped;
        await _appSettings.SetAsync(
            EditorPaneWidthSettingKey,
            EditorPaneWidth.ToString("0.##", CultureInfo.InvariantCulture),
            ct).ConfigureAwait(true);
    }

    public async Task CommitHabitScreenWidthAsync(CancellationToken ct = default)
    {
        var clamped = Math.Clamp(HabitScreenWidth, MinHabitScreenWidth, MaxHabitScreenWidth);
        if (Math.Abs(clamped - HabitScreenWidth) > 0.5)
            HabitScreenWidth = clamped;
        await _appSettings.SetAsync(
            HabitScreenWidthSettingKey,
            HabitScreenWidth.ToString("0.##", CultureInfo.InvariantCulture),
            ct).ConfigureAwait(true);
    }

    private async ValueTask LoadEditorPaneWidthAsync(CancellationToken ct)
        => EditorPaneWidth = await LoadWidthAsync(EditorPaneWidthSettingKey, DefaultEditorPaneWidth, MinEditorPaneWidth, MaxEditorPaneWidth, ct);

    private async ValueTask LoadHabitScreenWidthAsync(CancellationToken ct)
        => HabitScreenWidth = await LoadWidthAsync(HabitScreenWidthSettingKey, DefaultHabitScreenWidth, MinHabitScreenWidth, MaxHabitScreenWidth, ct);

    private async Task<double> LoadWidthAsync(string key, double @default, double min, double max, CancellationToken ct)
    {
        var raw = await _appSettings.GetAsync(key, ct).ConfigureAwait(true);
        var value = @default;
        if (raw is not null
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed))
        {
            value = parsed;
        }
        return Math.Clamp(value, min, max);
    }

    private async void OnHomePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.SelectedRow)) return;
        await HabitScreen.LoadAsync(Home.SelectedRow?.HabitNodeId).ConfigureAwait(true);
        if (Home.SelectedRow is null)
            await Editor.LoadAsync(null).ConfigureAwait(true);
    }

    private async void OnHabitScreenConfigureRequested(object? sender, EventArgs e)
    {
        await Editor.LoadAsync(Home.SelectedRow?.HabitNodeId).ConfigureAwait(true);
    }
}
