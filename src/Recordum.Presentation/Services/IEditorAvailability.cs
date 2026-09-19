using Recordum.Presentation.Resources;

namespace Recordum.Presentation.Services;

/// <summary>The part of "is the rich markdown editor usable?" that ViewModels need to know. Portable:
/// the Windows implementation (<c>IWebView2Availability</c> in the WPF shell) probes the WebView2
/// runtime and adds the environment handle the WPF control needs; another shell implements this
/// against its own editor. ViewModels only ever see this base contract, so Recordum.Presentation
/// carries no reference to the WebView2 package.</summary>
public interface IEditorAvailability
{
    bool IsAvailable { get; }

    /// <summary>Human-readable runtime version for the diagnostic dialog; the default says "not available".</summary>
    string RuntimeVersion => Strings.Diagnostic_NotAvailable;

    Task ProbeAsync();
}
