using Recordum.Presentation.ViewModels.Settings;

namespace Recordum.Presentation.Services;

public interface ISettingsWindowLauncher
{
    Task ShowAsync(SettingsCategoryId? initialCategory = null);
}
