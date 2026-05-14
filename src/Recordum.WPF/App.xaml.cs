using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Recordum.WPF.Bootstrap;

namespace Recordum.WPF;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var collection = new ServiceCollection();
        collection.AddRecordumServices(RecordumOptions.Default());
        _services = collection.BuildServiceProvider();

        var window = _services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
