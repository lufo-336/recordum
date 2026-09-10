using System.Reflection;
using Recordum.Presentation.Formatting;

namespace Recordum.Presentation.Tests;

public class NoWpfReferenceTests
{
    /// <summary>Il confine dell'estrazione non è una convenzione, è una proprietà dell'assembly:
    /// Recordum.Presentation è net10.0 senza -windows, quindi non PUÒ referenziare WPF. Questo test
    /// lo rende esplicito e lo tiene vero anche se un domani qualcuno cambiasse il TFM.</summary>
    [Fact]
    public void Presentation_references_no_WPF_assembly()
    {
        var referenced = typeof(DurationFormat).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.DoesNotContain("PresentationFramework", referenced);
        Assert.DoesNotContain("PresentationCore", referenced);
        Assert.DoesNotContain("WindowsBase", referenced);
    }

    /// <summary>Il TFM stesso: se qualcuno aggiungesse -windows, il test sopra continuerebbe a
    /// passare finché nessuno USA WPF. Questo scatta subito.</summary>
    [Fact]
    public void Presentation_targets_a_platform_neutral_framework()
    {
        var tfm = typeof(DurationFormat).Assembly
            .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()!
            .FrameworkName;

        Assert.Equal(".NETCoreApp,Version=v10.0", tfm);
    }
}
