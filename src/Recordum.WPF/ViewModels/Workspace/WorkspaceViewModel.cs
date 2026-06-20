using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.ViewModels.Workspace;

/// <summary>Owns the tabbed workspace shell: the open subject tabs, the always-present Panoramica
/// (active when no subject tab is), the preview/pin lifecycle, and light session persistence.
/// Ships no XAML; Plans 2–3 render it.</summary>
public sealed partial class WorkspaceViewModel : ObservableObject, IDisposable
{
    internal const double DefaultSidebarWidth = 280d;
    internal const string StateSettingKey = "ui.workspace";

    private readonly ISubjectScreenFactory _screenFactory;
    private readonly IAppSettingService _appSettings;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private SubjectTabViewModel? _previewTab;
    private bool _restoring;

    public ObservableCollection<SubjectTabViewModel> Tabs { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPanoramicaActive))]
    private SubjectTabViewModel? _activeSubjectTab;

    [ObservableProperty] private bool _sidebarCollapsed;
    [ObservableProperty] private double _sidebarWidth = DefaultSidebarWidth;

    public bool IsPanoramicaActive => ActiveSubjectTab is null;

    /// <summary>Raised when a tab asks to export its subject (bubbled from the tab's screen).</summary>
    public event EventHandler<Guid>? ExportRequested;
    /// <summary>Raised when something inside a tab (e.g. a Panoramica child row) asks to open another subject.</summary>
    public event EventHandler<Guid>? SubjectActivationRequested;

    public WorkspaceViewModel(ISubjectScreenFactory screenFactory, IAppSettingService appSettings)
    {
        ArgumentNullException.ThrowIfNull(screenFactory);
        ArgumentNullException.ThrowIfNull(appSettings);
        _screenFactory = screenFactory;
        _appSettings = appSettings;
    }

    /// <summary>Make the Panoramica the active view (no subject tab selected).</summary>
    public void ActivatePanoramica() => ActiveSubjectTab = null;

    [RelayCommand]
    private void ToggleSidebar() => SidebarCollapsed = !SidebarCollapsed;

    /// <summary>Parameterized wrapper so the tab strip's ✕ button can bind with a CommandParameter.</summary>
    [RelayCommand]
    private void CloseTabCmd(SubjectTabViewModel? tab)
    {
        if (tab is not null) CloseTab(tab);
    }

    /// <summary>Ctrl+W: close the active tab (no-op on Panoramica).</summary>
    [RelayCommand]
    private void CloseActiveTab()
    {
        if (ActiveSubjectTab is { } t) CloseTab(t);
    }

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

        var created = _screenFactory.Create();
        var tab = new SubjectTabViewModel(subjectId, kind, title, created.Screen, created.Scope, isPreview: !pin);
        tab.AllColumnsClosed += (_, _) => CloseTab(tab);
        tab.ExportRequested += (_, _) => ExportRequested?.Invoke(this, tab.SubjectId);
        tab.ChildSelected += (_, id) => SubjectActivationRequested?.Invoke(this, id);

        var previewIdx = !pin && _previewTab is not null ? Tabs.IndexOf(_previewTab) : -1;
        if (previewIdx >= 0)
        {
            var replaced = Tabs[previewIdx];
            Tabs[previewIdx] = tab;       // replace the existing preview tab in place (no accumulation)
            replaced.Dispose();           // release the discarded preview tab's scope
        }
        else
        {
            Tabs.Add(tab);
        }
        _previewTab = pin ? null : tab;
        ActiveSubjectTab = tab;

        await created.Screen.LoadAsync(subjectId, ct).ConfigureAwait(true);
    }

    public void PinActiveTab()
    {
        if (ActiveSubjectTab is { } t)
        {
            t.Pin();
            if (_previewTab == t) _previewTab = null;
        }
    }

    public void ActivateTab(SubjectTabViewModel tab) => ActiveSubjectTab = tab;

    public void CloseTab(SubjectTabViewModel tab)
    {
        var idx = Tabs.IndexOf(tab);
        if (idx < 0) return;
        if (_previewTab == tab) _previewTab = null;
        Tabs.RemoveAt(idx);
        if (ActiveSubjectTab == tab)
            ActiveSubjectTab = Tabs.Count == 0 ? null : Tabs[Math.Min(idx, Tabs.Count - 1)];
        tab.Dispose();
    }

    public void MoveTab(SubjectTabViewModel tab, int newIndex)
    {
        var old = Tabs.IndexOf(tab);
        if (old < 0) return;
        newIndex = Math.Clamp(newIndex, 0, Tabs.Count - 1);
        if (old != newIndex) Tabs.Move(old, newIndex);
    }

    /// <summary>Called when the user mutates a subject (saved a diary entry, recorded a completion,
    /// edited config). Promotes that subject's preview tab to a persistent one (spec §3.3). Plan 3
    /// wires the per-tab save paths to call this; in Plan 1 it is unit-tested directly.</summary>
    public void NotifySubjectEdited(Guid subjectId)
    {
        var tab = Tabs.FirstOrDefault(t => t.SubjectId == subjectId && t.IsPreview);
        if (tab is null) return;
        tab.Pin();
        if (_previewTab == tab) _previewTab = null;
    }

    /// <summary>Persist the light session state (pinned tabs + active + sidebar) to <c>ui.workspace</c>.
    /// Suppressed while <see cref="RestoreStateAsync"/> is replaying (the restored state == the persisted
    /// state, and the replay mutates many properties — saving each would be a write storm). Serialized via
    /// a gate so two near-simultaneous changes can't both INSERT a not-yet-existing key (UNIQUE violation).</summary>
    public async Task SaveStateAsync(CancellationToken ct = default)
    {
        if (_restoring) return;
        var state = new WorkspaceState
        {
            SidebarCollapsed = SidebarCollapsed,
            SidebarWidth = SidebarWidth,
            ActiveTabId = ActiveSubjectTab?.SubjectId.ToString() ?? WorkspaceState.PanoramicaId,
            Tabs = Tabs.Where(t => !t.IsPreview)
                       .Select(t => new WorkspaceTabState(t.SubjectId, t.Kind))
                       .ToList(),
        };
        var json = state.ToJson();
        await _saveGate.WaitAsync(ct).ConfigureAwait(true);
        try
        {
            await _appSettings.SetAsync(StateSettingKey, json, ct).ConfigureAwait(true);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    /// <summary>Reopen pinned tabs + active tab + sidebar from <c>ui.workspace</c>. <paramref name="titleResolver"/>
    /// supplies a fresh title for each persisted subject (the workspace doesn't depend on the node service).
    /// A stale legacy <c>ui.open_columns</c> value is never parsed.</summary>
    public async Task RestoreStateAsync(Func<Guid, WorkspaceTabKind, string> titleResolver, CancellationToken ct = default)
    {
        var state = WorkspaceState.FromJson(await _appSettings.GetAsync(StateSettingKey, ct).ConfigureAwait(true));
        if (state is null) return;

        _restoring = true;
        try
        {
            SidebarCollapsed = state.SidebarCollapsed;
            if (state.SidebarWidth > 0) SidebarWidth = state.SidebarWidth;

            foreach (var t in state.Tabs)
                await OpenSubjectAsync(t.SubjectId, t.Kind, titleResolver(t.SubjectId, t.Kind), pin: true, ct).ConfigureAwait(true);

            if (state.ActiveTabId == WorkspaceState.PanoramicaId || !Guid.TryParse(state.ActiveTabId, out var activeId))
                ActivatePanoramica();
            else
                ActiveSubjectTab = Tabs.FirstOrDefault(t => t.SubjectId == activeId);
        }
        finally
        {
            _restoring = false;
        }
    }

    /// <summary>Dispose every open tab's DI scope (app shutdown). Plan 1 deferred this here.</summary>
    public void Dispose()
    {
        foreach (var tab in Tabs) tab.Dispose();
        Tabs.Clear();
        _saveGate.Dispose();
    }
}
