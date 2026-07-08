using System.Windows;

namespace Recordum.WPF.Tests.Fixtures;

/// <summary>Bootstraps a single, process-wide WPF <see cref="Application"/> with App.xaml's full
/// resource set (converters, brushes, icons, styles) merged in, for the handful of tests that
/// construct a fully composed View leaning on StaticResource/DynamicResource lookups resolved
/// through <see cref="Application.Current"/> (e.g. NotesTabView's EnumToBool/BoolToVisible/Brush.*/
/// Icon.* references).
///
/// <para>WPF allows constructing <see cref="Application"/> at most once per process — a second
/// attempt throws <see cref="InvalidOperationException"/> even after the first instance's dispatcher
/// has shut down and reset <see cref="Application.Current"/> back to null (which happens between
/// per-test STA threads under Xunit.StaFact's <c>[WpfFact]</c>). A per-call-site
/// "if (Application.Current is null)" guard therefore races across test files/threads. This type's
/// static constructor is the single construction call site; the CLR guarantees it runs at most once
/// process-wide regardless of which thread touches it first, which a runtime null-check cannot.</para></summary>
public static class TestApp
{
    static TestApp()
    {
        if (Application.Current is null)
        {
            var app = new Recordum.WPF.App();
            app.InitializeComponent();
            // Default ShutdownMode is OnLastWindowClose: the first test in the whole assembly to
            // Show() then Close() a Window (e.g. any of the tests here, or MarkdownEditorControlTests
            // elsewhere) would otherwise trigger an implicit Application.Shutdown() — which
            // permanently blocks LoadComponent/GetResourcePackage for every later [WpfFact] test in
            // the process ("Application is shutting down"). Explicit shutdown means this Application
            // simply never shuts down for the lifetime of the test process, which is exactly what a
            // shared bootstrap needs.
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }
    }

    /// <summary>No-op call site whose only purpose is to force the static constructor above to have
    /// run before the caller touches anything that resolves resources through Application.Current.</summary>
    public static void EnsureLoaded()
    {
    }
}
