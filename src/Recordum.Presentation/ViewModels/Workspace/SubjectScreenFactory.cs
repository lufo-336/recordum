using Microsoft.Extensions.DependencyInjection;
using Recordum.Presentation.ViewModels.HabitScreen;

namespace Recordum.Presentation.ViewModels.Workspace;

/// <summary>Produces each tab's HabitScreenViewModel inside a fresh DI scope so the screen and its
/// scoped services live and die with the tab (disposed on tab close).</summary>
public sealed class SubjectScreenFactory(IServiceScopeFactory scopeFactory) : ISubjectScreenFactory
{
    public SubjectScreen Create()
    {
        var scope = scopeFactory.CreateScope();
        var screen = scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        return new SubjectScreen(screen, scope);
    }
}
