using System.Windows;
using FluentAssertions;
using Recordum.WPF.Services;
using Xunit;

namespace Recordum.WPF.Tests.Resources;

public class IconResourceAlignmentTests
{
    [WpfFact]
    public void Every_catalog_key_resolves_to_a_geometry()
    {
        // The "pack://" URI scheme is only registered once a WPF Application exists; in a bare
        // test host there is none, so instantiate one (sets Application.Current as a side effect).
        if (Application.Current is null) _ = new Application();

        var dict = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Recordum.WPF;component/Resources/Icons.xaml")
        };

        foreach (var key in IconCatalog.All)
            dict.Contains(key).Should().BeTrue($"icon resource '{key}' must exist in Icons.xaml");
    }
}
