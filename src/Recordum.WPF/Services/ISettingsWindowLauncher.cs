using Recordum.WPF.ViewModels.Settings;

namespace Recordum.WPF.Services;

public interface ISettingsWindowLauncher
{
    Task ShowAsync(SettingsCategoryId? initialCategory = null);
}
