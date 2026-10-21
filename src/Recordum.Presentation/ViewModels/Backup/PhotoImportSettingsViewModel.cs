using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels.Backup;

/// <summary>La sezione "Foto" di Dati e backup: come si conservano le foto scelte (D10). Cambiare il valore lo salva.</summary>
public sealed partial class PhotoImportSettingsViewModel(IAppSettingService settings) : ObservableObject
{
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsGpsWarning))]
    private PhotoImportMode _mode = PhotoImportMode.ResizeAndClean;

    public bool ShowsGpsWarning => Mode == PhotoImportMode.KeepOriginal;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var saved = await settings.GetEnumAsync<PhotoImportMode>(PhotoImportSettings.Key, ct).ConfigureAwait(true);
        _loading = true;
        try { Mode = saved ?? PhotoImportMode.ResizeAndClean; }
        finally { _loading = false; }
    }

    partial void OnModeChanged(PhotoImportMode value)
    {
        if (!_loading) _ = SaveAsync(value);
    }

    private async Task SaveAsync(PhotoImportMode value)
    {
        try
        {
            await settings.SetEnumAsync(PhotoImportSettings.Key, value).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // fire-and-forget dal setter: nessun chiamante a cui propagare
        catch (Exception ex)
#pragma warning restore CA1031
        {
            System.Diagnostics.Debug.WriteLine($"PhotoImportSettingsViewModel save failed: {ex}");
        }
    }
}
