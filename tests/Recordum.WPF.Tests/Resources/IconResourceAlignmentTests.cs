using System.Windows;
using FluentAssertions;
using Recordum.Presentation.Services;
using Recordum.WPF.Tests.Fixtures;
using Xunit;

namespace Recordum.WPF.Tests.Resources;

public class IconResourceAlignmentTests
{
    [WpfFact]
    public void Every_catalog_key_resolves_to_a_geometry()
    {
        // The "pack://" URI scheme is only registered once a WPF Application exists; in a bare
        // test host there is none. Routed through TestApp (not a local "if Current is null, new
        // Application()") so this is the one process-wide construction site shared with any other
        // test that needs a real Application — WPF permits constructing Application at most once
        // per process, and a per-call-site null-check races across [WpfFact] tests' separate STA
        // threads (Application.Current can go null again between tests even though the "already
        // constructed one" constraint persists for the whole process).
        TestApp.EnsureLoaded();

        var dict = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Recordum.WPF;component/Resources/Icons.xaml")
        };

        foreach (var key in IconCatalog.All)
            dict.Contains(key).Should().BeTrue($"icon resource '{key}' must exist in Icons.xaml");
    }
}
