using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.WPF.ViewModels.HabitScreen;

namespace Recordum.WPF.ViewModels.Workspace;

/// <summary>Owns the tabbed workspace shell: the open subject tabs, the always-present Panoramica
/// (active when no subject tab is), the preview/pin lifecycle, and light session persistence.
/// Ships no XAML; Plans 2–3 render it.</summary>
public sealed partial class WorkspaceViewModel : ObservableObject
{
    internal const double DefaultSidebarWidth = 280d;

    private readonly Func<HabitScreenViewModel> _screenFactory;
    private readonly IAppSettingService _appSettings;

    public ObservableCollection<SubjectTabViewModel> Tabs { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPanoramicaActive))]
    private SubjectTabViewModel? _activeSubjectTab;

    [ObservableProperty] private bool _sidebarCollapsed;
    [ObservableProperty] private double _sidebarWidth = DefaultSidebarWidth;

    public bool IsPanoramicaActive => ActiveSubjectTab is null;

    public WorkspaceViewModel(Func<HabitScreenViewModel> screenFactory, IAppSettingService appSettings)
    {
        ArgumentNullException.ThrowIfNull(screenFactory);
        ArgumentNullException.ThrowIfNull(appSettings);
        _screenFactory = screenFactory;
        _appSettings = appSettings;
    }

    /// <summary>Make the Panoramica the active view (no subject tab selected).</summary>
    public void ActivatePanoramica() => ActiveSubjectTab = null;
}
