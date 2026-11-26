using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Startup;
using Recordum.UI.Bootstrap;
using Recordum.UI.Diagnostics;
using Recordum.UI.Views;
using Recordum.UI.Views.Startup;

namespace Recordum.UI.Phone;

/// <summary>L'ospite d'avvio della testa telefono: versione minima per l'onda 4a (al Task 5 arriva PhoneShell).</summary>
public sealed class PhoneStartupHost : IStartupHost
{
    private readonly ContentControl _root;
    private WaitingView? _waitingView;
    private MainWindowViewModel? _mainViewModel;

    /// <param name="attach">Riceve la radice: l'app la assegna a <c>ISingleViewApplicationLifetime.MainView</c>
    /// (un'interfaccia che i test non possono implementare).</param>
    public PhoneStartupHost(Action<Control> attach)
    {
        ArgumentNullException.ThrowIfNull(attach);
        _root = new ContentControl();
        attach(_root);
    }

    public void ShowWaiting(string status)
    {
        _waitingView ??= new WaitingView();
        _waitingView.Status = status;
        if (!ReferenceEquals(_root.Content, _waitingView))
            _root.Content = _waitingView;
    }

    public async Task<IDbKeyProvider?> UnlockAsync(UnlockViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _root.Content = new UnlockView { DataContext = viewModel };
        // Sul telefono non c'e' il pulsante chiudi sulla vista di sblocco: Result non si annulla.
        return await viewModel.Result.ConfigureAwait(true);
    }

    public void ShowFatal(string message, string? logPath)
    {
        ArgumentNullException.ThrowIfNull(message);

        var brand = new TextBlock
        {
            Text = Strings.AppTitle,
            FontSize = 28,
            FontWeight = FontWeight.SemiBold,
        };
        brand[!TextBlock.FontFamilyProperty] = new DynamicResourceExtension("Font.Serif");
        brand[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("Brush.Text");

        var title = new TextBlock
        {
            Text = Strings.StartupErrorTitle,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        title[!TextBlock.FontSizeProperty] = new DynamicResourceExtension("FontSize.Heading");
        title[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("Brush.Text");

        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        };
        messageBlock[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("Brush.Text");

        var logBlock = new SelectableTextBlock
        {
            Text = logPath ?? string.Empty,
            IsVisible = !string.IsNullOrEmpty(logPath),
            TextWrapping = TextWrapping.Wrap,
        };
        logBlock[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("Brush.TextMuted");
        logBlock[!TextBlock.FontSizeProperty] = new DynamicResourceExtension("FontSize.Small");

        var panel = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children = { brand, title, messageBlock, logBlock },
        };

        var scroll = new ScrollViewer { Content = panel };
        scroll[!ScrollViewer.BackgroundProperty] = new DynamicResourceExtension("Brush.Background");
        scroll[!ScrollViewer.FontFamilyProperty] = new DynamicResourceExtension("Font.Default");
        _root.Content = scroll;
    }

    // Task 5: qui la PhoneShell
    public void ShowMain(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _mainViewModel = services.GetRequiredService<MainWindowViewModel>();
        var container = new Border
        {
            Child = new HomeView { DataContext = _mainViewModel.Home },
        };
        container[!Border.BackgroundProperty] = new DynamicResourceExtension("Brush.Background");
        _root.Content = container;
        _ = InitializeMainAsync();
    }

    private async Task InitializeMainAsync()
    {
        if (_mainViewModel is not { } vm) return;
        try
        {
            await vm.InitializeAsync().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // un guasto transitorio all'avvio della Home non deve sfuggire da una fire-and-forget
        catch (Exception ex)
#pragma warning restore CA1031
        {
            CrashLogger.Log("PhoneStartupHost.ShowMain", ex);
        }
    }
}
