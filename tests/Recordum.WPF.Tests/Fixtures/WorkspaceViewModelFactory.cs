using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.Workspace;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeSubjectScreenFactory : ISubjectScreenFactory
{
    public int CreatedCount { get; private set; }
    public List<TrackingDisposable> Scopes { get; } = [];

    public SubjectScreen Create()
    {
        CreatedCount++;
        var screen = HabitScreenViewModelFactory.Create().Vm;
        var scope = new TrackingDisposable();
        Scopes.Add(scope);
        return new SubjectScreen(screen, scope);
    }
}

public sealed class TrackingDisposable : IDisposable
{
    public bool Disposed { get; private set; }
    public void Dispose() => Disposed = true;
}

public static class WorkspaceViewModelFactory
{
    public static (WorkspaceViewModel Vm, FakeAppSettingService Settings) Create()
    {
        var settings = new FakeAppSettingService();
        return (new WorkspaceViewModel(new FakeSubjectScreenFactory(), settings), settings);
    }

    /// <summary>Variant exposing the fake factory so disposal can be asserted.</summary>
    public static (WorkspaceViewModel Vm, FakeAppSettingService Settings, FakeSubjectScreenFactory Screens) CreateWithScreens()
    {
        var settings = new FakeAppSettingService();
        var screens = new FakeSubjectScreenFactory();
        return (new WorkspaceViewModel(screens, settings), settings, screens);
    }
}
