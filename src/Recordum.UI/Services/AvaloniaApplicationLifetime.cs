using Avalonia.Controls.ApplicationLifetimes;
namespace Recordum.UI.Services;

/// <summary>Gemella di <c>Recordum.WPF.Services.WpfApplicationLifetime</c> (<c>Application.Current?.Shutdown()</c>).
/// Prende <see cref="IControlledApplicationLifetime"/> per costruttore invece di leggerlo da
/// <c>Avalonia.Application.Current.ApplicationLifetime</c> al proprio interno: quella proprietà è
/// <c>null</c> finché il framework non ha finito <c>OnFrameworkInitializationCompleted</c> (verificato
/// in un test headless — un <c>[AvaloniaFact]</c> normale la trova sempre <c>null</c>, perché
/// <c>TestAppBuilder</c> non passa mai dal ramo desktop), quindi un servizio che la leggesse da sé non
/// sarebbe costruibile in un test isolato. La registrazione reale (Task 11) la passa esplicitamente
/// da <c>App</c>, dove il framework l'ha già assegnata.</summary>
public sealed class AvaloniaApplicationLifetime(IControlledApplicationLifetime lifetime) : Recordum.Core.Abstractions.IApplicationLifetime
{
    public void Shutdown() => lifetime.Shutdown();
}
