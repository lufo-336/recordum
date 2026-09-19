using System.IO;
using Microsoft.Web.WebView2.Core;
using Recordum.Presentation.Services;
using Recordum.WPF.Diagnostics;

namespace Recordum.WPF.Bootstrap;

/// <summary>WPF-shell extension of <see cref="IEditorAvailability"/>: adds the WebView2 environment
/// handle the markdown control needs. Dies with WebView2 at F4.</summary>
public interface IWebView2Availability : IEditorAvailability
{
    CoreWebView2Environment? Environment { get; }
}

public sealed class WebView2Availability : IWebView2Availability
{
    public bool IsAvailable { get; private set; }
    public CoreWebView2Environment? Environment { get; private set; }

#pragma warning disable CA1031 // non-fatal probe — runtime or git missing is expected
    string IEditorAvailability.RuntimeVersion
    {
        get
        {
            try
            {
                return CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch
            {
                return Recordum.Presentation.Resources.Strings.Diagnostic_NotAvailable;
            }
        }
    }
#pragma warning restore CA1031

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

    // Explicit, writable user-data folder under %LocalAppData%\Recordum (same root as the DB,
    // logs and backups). Passing null lets WebView2 default the folder to the *host executable's*
    // directory — which is the install dir for a deployed app (e.g. Program Files), or
    // C:\Program Files\dotnet\ when launched via `dotnet run`. Both are read-only, so CreateAsync
    // throws E_ACCESSDENIED and we wrongly degrade to the plain-text fallback even though the
    // WebView2 Runtime is installed. Anchoring the folder to LocalAppData fixes both cases.
    private static string UserDataFolder()
    {
        var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Recordum", "WebView2");
    }

    // CoreWebView2Environment.CreateAsync() must run on an STA thread with a running
    // message pump: its async COM completion is delivered via a window message. The
    // app reaches this probe deep inside ApplicationInitializer's ConfigureAwait(false)
    // chain — i.e. on an MTA threadpool thread with no pump — where CreateAsync throws
    // and we wrongly degrade to the plain-text fallback even when the Runtime is
    // installed. Marshal onto the WPF UI Dispatcher so it runs on the correct thread.
    private static Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var userDataFolder = UserDataFolder();
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            // No WPF Application (unit tests) or already on the UI thread — call directly.
            return CoreWebView2Environment.CreateAsync(null, userDataFolder, null);
        }

        return dispatcher.InvokeAsync(
            () => CoreWebView2Environment.CreateAsync(null, userDataFolder, null)).Task.Unwrap();
    }
}
