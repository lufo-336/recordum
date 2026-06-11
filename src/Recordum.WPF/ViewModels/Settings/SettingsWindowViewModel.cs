using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.WPF.Resources;
using Recordum.WPF.ViewModels.Backup;
using Recordum.WPF.ViewModels.DiagnosticInfo;

namespace Recordum.WPF.ViewModels.Settings;

/// <summary>Obsidian-style unified settings window: a fixed, ordered list of categories whose
/// content panes reuse the per-area viewmodels. Disabled categories carry ComingSoonContent.</summary>
public sealed partial class SettingsWindowViewModel : ObservableObject
{
    [ObservableProperty] private SettingsCategory? _selectedCategory;

    public ObservableCollection<SettingsCategory> Categories { get; }

    public SettingsWindowViewModel(
        SettingsViewModel appearance,
        GeneralSettingsViewModel general,
        BackupSettingsViewModel backup,
        DiagnosticInfoViewModel diagnostics)
    {
        Categories = new ObservableCollection<SettingsCategory>
        {
            new(SettingsCategoryId.Appearance,  Strings.Settings_Category_Appearance,  "Icon.Palette",           true,  appearance),
            new(SettingsCategoryId.General,     Strings.Settings_Category_General,     "Icon.SlidersHorizontal", true,  general),
            new(SettingsCategoryId.Backup,      Strings.Settings_Category_Backup,      "Icon.LifeBuoy",          true,  backup),
            new(SettingsCategoryId.Diagnostics, Strings.Settings_Category_Diagnostics, "Icon.Info",              true,  diagnostics),
            new(SettingsCategoryId.Sync,        Strings.Settings_Category_Sync,        "Icon.RefreshCw",         false, new ComingSoonContent(Strings.Settings_ComingSoon_Sync)),
            new(SettingsCategoryId.Plugins,     Strings.Settings_Category_Plugins,     "Icon.Puzzle",            false, new ComingSoonContent(Strings.Settings_ComingSoon_Plugins)),
            new(SettingsCategoryId.Ai,          Strings.Settings_Category_Ai,          "Icon.Sparkles",          false, new ComingSoonContent(Strings.Settings_ComingSoon_Ai)),
        };
    }

    /// <summary>Selects the requested category (launcher deep-link); disabled or null requests
    /// fall back to Appearance.</summary>
    public void SelectInitial(SettingsCategoryId? id)
    {
        var match = id is { } i ? Categories.FirstOrDefault(c => c.Id == i && c.IsEnabled) : null;
        SelectedCategory = match ?? Categories[0];
    }
}
