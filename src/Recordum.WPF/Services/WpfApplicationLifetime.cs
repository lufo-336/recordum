using Recordum.Core.Abstractions;

using Recordum.Presentation.Services;

namespace Recordum.WPF.Services;

/// <summary>IApplicationLifetime su WPF. Muore con la shell; la gemella Avalonia chiuderà
/// l'IClassicDesktopStyleApplicationLifetime, che è un oggetto diverso con lo stesso mestiere.</summary>
public sealed class WpfApplicationLifetime : IApplicationLifetime
{
    public void Shutdown() => System.Windows.Application.Current?.Shutdown();
}
