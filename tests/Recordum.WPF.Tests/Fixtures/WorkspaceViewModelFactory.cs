using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.Workspace;

namespace Recordum.WPF.Tests.Fixtures;

/// <summary>Builds a WorkspaceViewModel whose per-tab HabitScreenViewModels come from the existing
/// HabitScreenViewModelFactory (all-fakes). Tab-lifecycle tests don't exercise screen content, so a
/// fresh fake screen per tab is fine.</summary>
public static class WorkspaceViewModelFactory
{
    public static (WorkspaceViewModel Vm, FakeAppSettingService Settings) Create()
    {
        var settings = new FakeAppSettingService();
        Func<HabitScreenViewModel> screenFactory = () => HabitScreenViewModelFactory.Create().Vm;
        return (new WorkspaceViewModel(screenFactory, settings), settings);
    }
}
