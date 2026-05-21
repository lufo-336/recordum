using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.WPF.ViewModels.HabitEditor;

namespace Recordum.WPF.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    internal const string EditorPaneWidthSettingKey = "ui.editor_pane_width";
    internal const double DefaultEditorPaneWidth = 480d;
    internal const double MinEditorPaneWidth = 320d;
    internal const double MaxEditorPaneWidth = 900d;

    private readonly IAppSettingService _appSettings;

    public HomeViewModel Home { get; }
    public SettingsViewModel Settings { get; }
    public HabitEditorViewModel Editor { get; }

    [ObservableProperty] private double _editorPaneWidth = DefaultEditorPaneWidth;

    public MainWindowViewModel(
        HomeViewModel home,
        SettingsViewModel settings,
        HabitEditorViewModel editor,
        IAppSettingService appSettings)
    {
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(appSettings);
        Home = home;
        Settings = settings;
        Editor = editor;
        _appSettings = appSettings;
        Home.PropertyChanged += OnHomePropertyChanged;
    }

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        await LoadEditorPaneWidthAsync(ct).ConfigureAwait(true);
        await Settings.InitializeAsync(ct);
        await Home.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private static void Quit() => System.Windows.Application.Current?.Shutdown();

    /// <summary>
    /// Called by the view when the user finishes dragging the pane splitter. Clamps the value and persists it.
    /// </summary>
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

    private async ValueTask LoadEditorPaneWidthAsync(CancellationToken ct)
    {
        var raw = await _appSettings.GetAsync(EditorPaneWidthSettingKey, ct).ConfigureAwait(true);
        var value = DefaultEditorPaneWidth;
        if (raw is not null
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed))
        {
            value = parsed;
        }
        EditorPaneWidth = Math.Clamp(value, MinEditorPaneWidth, MaxEditorPaneWidth);
    }

    private async void OnHomePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.SelectedRow)) return;
        await Editor.LoadAsync(Home.SelectedRow?.HabitNodeId).ConfigureAwait(true);
    }
}
