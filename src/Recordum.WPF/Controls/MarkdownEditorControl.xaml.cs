using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Recordum.WPF.Bootstrap;

namespace Recordum.WPF.Controls;

public partial class MarkdownEditorControl : UserControl
{
    private bool _bridgeReady;
    private bool _isDisposed;

    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(nameof(Markdown), typeof(string), typeof(MarkdownEditorControl),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, e) => ((MarkdownEditorControl)d).PushContentToEditor((string)e.NewValue)));
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(MarkdownEditorControl),
            new PropertyMetadata(
                false,
                (d, e) => ((MarkdownEditorControl)d).PushReadOnlyToEditor((bool)e.NewValue)));
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(MarkdownEditorControl),
            new PropertyMetadata(string.Empty));
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public static readonly DependencyProperty AvailabilityProperty =
        DependencyProperty.Register(nameof(Availability), typeof(IWebView2Availability), typeof(MarkdownEditorControl),
            new PropertyMetadata(null, OnAvailabilityChanged));
    public IWebView2Availability? Availability
    {
        get => (IWebView2Availability?)GetValue(AvailabilityProperty);
        set => SetValue(AvailabilityProperty, value);
    }

    public MarkdownEditorControl()
    {
        InitializeComponent();
        Loaded += async (_, _) => await TryInitAsync().ConfigureAwait(true);
        Unloaded += (_, _) => _isDisposed = true;
    }

    private static async void OnAvailabilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => await ((MarkdownEditorControl)d).TryInitAsync().ConfigureAwait(true);

    private async Task TryInitAsync()
    {
        if (_isDisposed) return;
        if (Availability is not { IsAvailable: true, Environment: not null } a) return;
        if (WebView.CoreWebView2 is not null) return; // already inited

        await WebView.EnsureCoreWebView2Async(a.Environment).ConfigureAwait(true);
        var core = WebView.CoreWebView2!;
        var assetsRoot = Path.Combine(AppContext.BaseDirectory, "Resources", "EditorAssets");
        core.SetVirtualHostNameToFolderMapping(
            "recordum.local",
            assetsRoot,
            CoreWebView2HostResourceAccessKind.Allow);
        core.WebMessageReceived += OnWebMessageReceived;
        core.Navigate("https://recordum.local/editor.html");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_isDisposed) return;
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            switch (type)
            {
                case "ready":
                    _bridgeReady = true;
                    PushContentToEditor(Markdown);
                    PushReadOnlyToEditor(IsReadOnly);
                    break;
                case "change":
                    var md = root.GetProperty("md").GetString() ?? string.Empty;
                    if (Markdown != md) SetCurrentValue(MarkdownProperty, md);
                    break;
            }
        }
        catch (JsonException)
        {
            // malformed JS message — ignore
        }
    }

    private void PushContentToEditor(string md)
    {
        if (!_bridgeReady || _isDisposed || WebView.CoreWebView2 is null) return;
        var payload = JsonSerializer.Serialize(new { type = "setContent", md });
        WebView.CoreWebView2.PostWebMessageAsJson(payload);
    }

    private void PushReadOnlyToEditor(bool value)
    {
        if (!_bridgeReady || _isDisposed || WebView.CoreWebView2 is null) return;
        var payload = JsonSerializer.Serialize(new { type = "setReadOnly", value });
        WebView.CoreWebView2.PostWebMessageAsJson(payload);
    }
}
