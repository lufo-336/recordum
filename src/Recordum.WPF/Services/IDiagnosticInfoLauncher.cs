namespace Recordum.WPF.Services;

/// <summary>Abstraction over the WPF diagnostic-info modal so VMs can request it without referencing the window.</summary>
public interface IDiagnosticInfoLauncher
{
    Task ShowAsync();
}
