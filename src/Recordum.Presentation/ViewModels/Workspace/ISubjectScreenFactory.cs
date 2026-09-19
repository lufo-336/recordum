using Recordum.Presentation.ViewModels.HabitScreen;

namespace Recordum.Presentation.ViewModels.Workspace;

/// <summary>Creates a <see cref="HabitScreenViewModel"/> for one tab inside its own DI scope.
/// The returned <see cref="SubjectScreen.Scope"/> is owned by the tab and disposed when the tab
/// closes, releasing the screen and its scoped services.</summary>
public interface ISubjectScreenFactory
{
    SubjectScreen Create();
}

/// <summary>A per-tab screen plus the DI scope that owns it. Dispose the scope to release both.</summary>
public sealed record SubjectScreen(HabitScreenViewModel Screen, IDisposable Scope);
