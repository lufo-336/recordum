using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Settings;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeSettingsWindowLauncher : ISettingsWindowLauncher
{
    public List<SettingsCategoryId?> ShowCalls { get; } = [];

    public Task ShowAsync(SettingsCategoryId? initialCategory = null)
    {
        ShowCalls.Add(initialCategory);
        return Task.CompletedTask;
    }
}
