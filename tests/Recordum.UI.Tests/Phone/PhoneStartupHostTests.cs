using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Phone;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Startup;
using Xunit;

namespace Recordum.UI.Tests.Phone;

public class PhoneStartupHostTests
{
    private sealed class FakeSingleView
    {
        public Control? MainView { get; set; }
    }

    [AvaloniaFact]
    public void The_constructor_puts_a_root_in_the_main_view()
    {
        var fake = new FakeSingleView();
        _ = new PhoneStartupHost(root => fake.MainView = root);
        fake.MainView.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void Waiting_then_fatal_shows_the_message_and_the_log_path()
    {
        var fake = new FakeSingleView();
        var host = new PhoneStartupHost(root => fake.MainView = root);
        var window = new Window { Width = 360, Height = 780, Content = fake.MainView };
        window.Show();
        try
        {
            host.ShowWaiting("x");
            host.ShowFatal("boom", "/tmp/log.txt");
            Dispatcher.UIThread.RunJobs();

            var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            texts.Should().Contain("boom");
            texts.Should().Contain("/tmp/log.txt");
            window.GetVisualDescendants().OfType<SelectableTextBlock>().Should().ContainSingle(t => t.Text == "/tmp/log.txt");
            window.GetVisualDescendants().OfType<WaitingView>().Should().BeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShowMain_puts_the_home_view_in_the_main_view()
    {
        var fake = new FakeSingleView();
        var host = new PhoneStartupHost(root => fake.MainView = root);
        var db = new PrivateDbHost("phonehost");
        var window = new Window { Width = 360, Height = 780, Content = fake.MainView };
        window.Show();
        try
        {
            host.ShowMain(db.Provider);
            PrivateDbHost.Pump();

            var home = window.GetVisualDescendants().OfType<HomeView>().Should().ContainSingle().Subject;
            home.DataContext.Should().BeOfType<HomeViewModel>();
        }
        finally
        {
            window.Close();
        }
    }
}
