using System.Globalization;
using System.Text;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Theming;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Diagnostics;
using CoreThemeMode = Recordum.Core.Enums.ThemeMode;
using Recordum.WPF.Resources;

namespace Recordum.WPF;

public partial class App : Application
{
    private static readonly CompositeFormat StartupErrorMessageFormat =
        CompositeFormat.Parse(Strings.StartupErrorMessage);

    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Must be first: any later failure needs the global log to capture it.
        CrashLogger.WireUp();

        var collection = new ServiceCollection();
        collection.AddRecordumServices(RecordumOptions.Default());
        _services = collection.BuildServiceProvider();

        try
        {
            await _services.GetRequiredService<ApplicationInitializer>().InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            CrashLogger.Log("ApplicationInitializer.InitializeAsync (fatal)", ex);
            MessageBox.Show(
                string.Format(CultureInfo.CurrentCulture, StartupErrorMessageFormat, ex.Message),
                Strings.StartupErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(exitCode: 1);
            return;
        }

        // Dev tool: `--seed-demo` populates a believable habit library (idempotent, see DemoDataSeeder).
        if (e.Args.Contains("--seed-demo"))
            await _services.GetRequiredService<DemoDataSeeder>().SeedAsync().ConfigureAwait(true);

        var theme = _services.GetRequiredService<IThemeService>();
        await theme.InitializeAsync().ConfigureAwait(true);
        ApplyTheme(theme.EffectiveTheme);
        theme.EffectiveThemeChanged += (_, mode) => Dispatcher.Invoke(() => ApplyTheme(mode));

        var window = _services.GetRequiredService<MainWindow>();
        window.Show();
    }

    private void ApplyTheme(CoreThemeMode effective)
    {
        var path = effective == CoreThemeMode.Dark ? "Resources/Dark.xaml" : "Resources/Light.xaml";
        var dict = new ResourceDictionary { Source = new Uri(path, UriKind.Relative) };
        // Index 0 = Tokens.xaml (static). Index 1 = active theme (swappable).
        Resources.MergedDictionaries[1] = dict;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
