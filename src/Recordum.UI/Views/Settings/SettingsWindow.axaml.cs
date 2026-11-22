using Recordum.Presentation.ViewModels.Settings;
using Recordum.Presentation.ViewModels;

namespace Recordum.UI.Views.Settings;

/// <summary>Finestra Impostazioni di Avalonia: Aspetto e Dati e backup, scelte da un segmento in testa.</summary>
public partial class SettingsWindow : RecordumWindow
{
    public SettingsWindow() => InitializeComponent();

    /// <summary>Il ViewModel della pagina Aspetto. Impostato, il segmento compare e la finestra apre su Aspetto.</summary>
    public SettingsViewModel? Appearance
    {
        get => AppearancePage.DataContext as SettingsViewModel;
        set
        {
            AppearancePage.DataContext = value;
            UpdateTabsVisibility();
            ShowPage(null);
        }
    }

    /// <summary>Il ViewModel della pagina Unisci. Impostato, compare il terzo segmento.</summary>
    public SyncSettingsViewModel? Sync
    {
        get => SyncPage.DataContext as SyncSettingsViewModel;
        set
        {
            SyncPage.DataContext = value;
            SyncTab.IsVisible = value is not null;
            UpdateTabsVisibility();
            ShowPage(null);
        }
    }

    private void UpdateTabsVisibility() => PageTabs.IsVisible = Appearance is not null || Sync is not null;

    /// <summary>La pagina della categoria: Backup apre Dati e backup, Sync apre Unisci, e cosi' la diagnostica
    /// che non ha ancora una pagina; il resto apre Aspetto, se c'e'.</summary>
    public void ShowPage(SettingsCategoryId? category)
    {
        if (category == SettingsCategoryId.Sync && Sync is not null)
        {
            SyncTab.IsChecked = true;
            DataTab.IsChecked = false;
            AppearanceTab.IsChecked = false;
            return;
        }
        var data = Appearance is null || category is SettingsCategoryId.Backup or SettingsCategoryId.Diagnostics;
        DataTab.IsChecked = data;
        AppearanceTab.IsChecked = !data;
        SyncTab.IsChecked = false;
    }
}
