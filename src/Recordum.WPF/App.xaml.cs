using System.Globalization;
using System.Text;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Recordum.WPF.Bootstrap;
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

        var collection = new ServiceCollection();
        collection.AddRecordumServices(RecordumOptions.Default());
        collection.AddSingleton<MainWindow>();
        _services = collection.BuildServiceProvider();

        try
        {
            await _services.GetRequiredService<ApplicationInitializer>().InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(CultureInfo.CurrentCulture, StartupErrorMessageFormat, ex.Message),
                Strings.StartupErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(exitCode: 1);
            return;
        }

        var window = _services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
