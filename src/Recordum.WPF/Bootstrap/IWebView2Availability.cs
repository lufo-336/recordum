using Microsoft.Web.WebView2.Core;
using Recordum.WPF.Diagnostics;

namespace Recordum.WPF.Bootstrap;

public interface IWebView2Availability
{
    bool IsAvailable { get; }
    CoreWebView2Environment? Environment { get; }
    Task ProbeAsync();
}

public sealed class WebView2Availability : IWebView2Availability
{
    public bool IsAvailable { get; private set; }
    public CoreWebView2Environment? Environment { get; private set; }

    public async Task ProbeAsync()
    {
        try
        {
            Environment = await CreateEnvironmentAsync().ConfigureAwait(false);
            IsAvailable = true;
        }
#pragma warning disable CA1031 // probe must be non-fatal — runtime missing is a normal Win10 case
        catch (Exception ex)
        {
            Environment = null;
            IsAvailable = false;
            // Was silently swallowed before — leaving us blind to *why* the editor
            // fell back. Log so the failure shows up in recordum.log + Diagnostic info.
            CrashLogger.Log("WebView2 probe failed — markdown editor falls back to plain text", ex);
        }
#pragma warning restore CA1031
    }

    // CoreWebView2Environment.CreateAsync() must run on an STA thread with a running
    // message pump: its async COM completion is delivered via a window message. The
    // app reaches this probe deep inside ApplicationInitializer's ConfigureAwait(false)
    // chain — i.e. on an MTA threadpool thread with no pump — where CreateAsync throws
    // and we wrongly degrade to the plain-text fallback even when the Runtime is
    // installed. Marshal onto the WPF UI Dispatcher so it runs on the correct thread.
    private static Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            // No WPF Application (unit tests) or already on the UI thread — call directly.
            return CoreWebView2Environment.CreateAsync();
        }

        return dispatcher.InvokeAsync(() => CoreWebView2Environment.CreateAsync()).Task.Unwrap();
    }
}
