using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Theming;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Diagnostics;
using CoreThemeMode = Recordum.Core.Enums.ThemeMode;
using CoreAppFont = Recordum.Core.Enums.AppFont;
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

        // Dev tool: `--seed-demo` populates a believable habit library (idempotent);
        // `--seed-habit <name>` backfills an existing habit with ~90 days of completions.
        if (e.Args.Contains("--seed-demo"))
            await _services.GetRequiredService<DemoDataSeeder>().SeedAsync().ConfigureAwait(true);
        var seedHabitIndex = Array.IndexOf(e.Args, "--seed-habit");
        if (seedHabitIndex >= 0 && seedHabitIndex + 1 < e.Args.Length)
            await _services.GetRequiredService<DemoDataSeeder>().SeedHabitAsync(e.Args[seedHabitIndex + 1]).ConfigureAwait(true);

        var theme = _services.GetRequiredService<IThemeService>();
        await theme.InitializeAsync().ConfigureAwait(true);
        ApplyTheme(theme.EffectiveTheme);
        theme.EffectiveThemeChanged += (_, mode) => Dispatcher.Invoke(() => ApplyTheme(mode));

        var font = _services.GetRequiredService<IAppFontService>();
        await font.InitializeAsync().ConfigureAwait(true);
        ApplyFont(font.CurrentFont);
        font.FontChanged += (_, f) => Dispatcher.Invoke(() => ApplyFont(f));

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

    private void ApplyFont(CoreAppFont font)
    {
        // Override the merged Font.Default at the app-resource level; DynamicResource references
        // (Window/Page in BaseStyles + the editor's font tripwire) pick the change up live.
        var key = font == CoreAppFont.Serif ? "Font.Serif" : "Font.System";
        // TryFindResource (not the Resources[] indexer) so it traverses the merged Tokens.xaml.
        if (TryFindResource(key) is FontFamily family)
            Resources["Font.Default"] = family;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
