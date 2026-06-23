using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Recordum.WPF.Bootstrap;

namespace Recordum.WPF.Controls;

public partial class MarkdownEditorControl : UserControl
{
    private bool _bridgeReady;
    private bool _isDisposed;
    private bool _initStarted;

    // Tripwire bound to a theme brush via DynamicResource (SetResourceReference in the ctor). When
    // the app swaps Dark.xaml <-> Light.xaml at runtime the brush re-resolves and this callback
    // re-pushes the new theme into an open editor. PushThemeToEditor no-ops until the bridge is
    // ready, so the early resolution during load is harmless (the "ready" handler does the first push).
    private static readonly DependencyProperty ThemeTripwireProperty =
        DependencyProperty.Register(nameof(ThemeTripwire), typeof(Brush), typeof(MarkdownEditorControl),
            new PropertyMetadata(null, (d, _) => ((MarkdownEditorControl)d).PushThemeToEditor()));
    private Brush? ThemeTripwire
    {
        get => (Brush?)GetValue(ThemeTripwireProperty);
        set => SetValue(ThemeTripwireProperty, value);
    }

    // Second tripwire, same idea, bound to the UI font (Font.Default) so a font-only switch re-pushes too.
    private static readonly DependencyProperty FontTripwireProperty =
        DependencyProperty.Register(nameof(FontTripwire), typeof(FontFamily), typeof(MarkdownEditorControl),
            new PropertyMetadata(null, (d, _) => ((MarkdownEditorControl)d).PushThemeToEditor()));
    private FontFamily? FontTripwire
    {
        get => (FontFamily?)GetValue(FontTripwireProperty);
        set => SetValue(FontTripwireProperty, value);
    }

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
        // Re-push to the editor whenever the app swaps theme dictionaries or the UI font at runtime.
        SetResourceReference(ThemeTripwireProperty, "Brush.Surface");
        SetResourceReference(FontTripwireProperty, "Font.Default");
        Loaded += async (_, _) => await TryInitAsync().ConfigureAwait(true);
        Unloaded += (_, _) => _isDisposed = true;
    }

    private static async void OnAvailabilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => await ((MarkdownEditorControl)d).TryInitAsync().ConfigureAwait(true);

    private async Task TryInitAsync()
    {
        if (_isDisposed) return;
        if (Availability is null || !Availability.IsAvailable || Availability.Environment is null)
        {
            ActivateFallback();
            return;
        }
        // Both Loaded and OnAvailabilityChanged call this; the async EnsureCoreWebView2Async
        // below means a second concurrent call would pass the CoreWebView2 null-check and
        // double-subscribe WebMessageReceived (every bridge message handled twice). Guard once.
        if (_initStarted || WebView.CoreWebView2 is not null) return;
        _initStarted = true;

        await WebView.EnsureCoreWebView2Async(Availability.Environment).ConfigureAwait(true);
        var core = WebView.CoreWebView2!;
        var assetsRoot = Path.Combine(AppContext.BaseDirectory, "Resources", "EditorAssets");
        core.SetVirtualHostNameToFolderMapping(
            "recordum.local",
            assetsRoot,
            CoreWebView2HostResourceAccessKind.Allow);
        core.WebMessageReceived += OnWebMessageReceived;
        core.Navigate("https://recordum.local/editor.html");
    }

    private void ActivateFallback()
    {
        if (FallbackBox.Visibility == Visibility.Visible) return;
        WebView.Visibility = Visibility.Collapsed;
        FallbackBox.Visibility = Visibility.Visible;
        FallbackBox.Text = Markdown;
        FallbackBox.TextChanged += (_, _) =>
        {
            if (FallbackBox.Text != Markdown) SetCurrentValue(MarkdownProperty, FallbackBox.Text);
        };
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
                    PushThemeToEditor();
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
        if (FallbackBox.Visibility == Visibility.Visible)
        {
            if (FallbackBox.Text != md) FallbackBox.Text = md;
            return;
        }
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

    /// <summary>Pushes the current theme brushes to the WebView editor so its surface, text and
    /// toolbar match the app exactly (dark by default, follows a light theme too).</summary>
    private void PushThemeToEditor()
    {
        if (!_bridgeReady || _isDisposed || WebView.CoreWebView2 is null) return;
        var vars = new
        {
            bg = Hex("Brush.Surface"),
            text = Hex("Brush.Text"),
            muted = Hex("Brush.TextMuted"),
            border = Hex("Brush.Border"),
            accent = Hex("Brush.Accent"),
            elev = Hex("Brush.SurfaceElevated"),
            dark = ThemeProbe.IsDark(),
            font = (Application.Current?.TryFindResource("Font.Default") as FontFamily)?.Source,
        };
        var payload = JsonSerializer.Serialize(new { type = "setTheme", vars });
        WebView.CoreWebView2.PostWebMessageAsJson(payload);
    }

    private static string? Hex(string brushKey)
    {
        if (Application.Current?.TryFindResource(brushKey) is SolidColorBrush b)
        {
            var c = b.Color;
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        return null;
    }
}
