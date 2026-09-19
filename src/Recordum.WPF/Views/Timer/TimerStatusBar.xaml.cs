using System.Windows.Controls;
using System.Windows.Threading;
using Recordum.Presentation.ViewModels.Timer;

namespace Recordum.WPF.Views.Timer;

public partial class TimerStatusBar : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public TimerStatusBar()
    {
        InitializeComponent();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
        _timer.Tick += (_, _) =>
        {
            if (DataContext is TimerStatusBarViewModel vm) vm.Tick();
        };
    }
}
