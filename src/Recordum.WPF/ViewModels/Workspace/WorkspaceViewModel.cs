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
    private SubjectTabViewModel? _previewTab;

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

    /// <summary>Open a subject as a tab. Single open = a transient preview tab that the next single
    /// open replaces; <paramref name="pin"/> = a persistent tab. Re-opening an already-open subject
    /// just activates it (spec §3.3).</summary>
    public async Task OpenSubjectAsync(Guid subjectId, WorkspaceTabKind kind, string title,
        bool pin = false, CancellationToken ct = default)
    {
        var existing = Tabs.FirstOrDefault(t => t.SubjectId == subjectId);
        if (existing is not null)
        {
            if (pin) existing.Pin();
            if (!existing.IsPreview) _previewTab = _previewTab == existing ? null : _previewTab;
            ActiveSubjectTab = existing;
            return;
        }

        var screen = _screenFactory();
        var tab = new SubjectTabViewModel(subjectId, kind, title, screen, isPreview: !pin);

        if (!pin && _previewTab is not null)
        {
            // Replace the existing preview tab in place (no accumulation).
            var idx = Tabs.IndexOf(_previewTab);
            Tabs[idx] = tab;
        }
        else
        {
            Tabs.Add(tab);
        }
        _previewTab = pin ? null : tab;
        ActiveSubjectTab = tab;

        await screen.LoadAsync(subjectId, ct).ConfigureAwait(true);
    }
}
