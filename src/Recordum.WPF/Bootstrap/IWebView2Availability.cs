using Microsoft.Web.WebView2.Core;

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
            Environment = await CoreWebView2Environment.CreateAsync().ConfigureAwait(false);
            IsAvailable = true;
        }
#pragma warning disable CA1031 // probe must be non-fatal — runtime missing is a normal Win10 case
        catch
        {
            Environment = null;
            IsAvailable = false;
        }
#pragma warning restore CA1031
    }
}
