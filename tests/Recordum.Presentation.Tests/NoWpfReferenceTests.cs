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

    /// <summary>Il qualificatore di piattaforma, non il TFM testuale: "net10.0" e "net10.0-windows"
    /// producono entrambi FrameworkName == ".NETCoreApp,Version=v10.0" (verificato sugli assembly
    /// veri del repo — <see cref="System.Runtime.Versioning.TargetFrameworkAttribute"/> da solo non
    /// li distingue), quindi un confronto sul solo FrameworkName non sarebbe scattato se qualcuno
    /// avesse aggiunto -windows. Ciò che li distingue davvero è TargetPlatformAttribute: il
    /// compilatore lo emette solo per un TFM con qualificatore di piattaforma (confrontato con
    /// Recordum.WPF.AssemblyInfo.cs, che lo porta con "Windows7.0"). La sua assenza è la garanzia
    /// reale che manca a Recordum.Presentation, ed è quella che deve scattare subito. Provato rosso
    /// mettendo -windows nel csproj ed eseguendo solo questa suite, poi ripristinato — v.
    /// task-2-report.md.</summary>
    [Fact]
    public void Presentation_targets_a_platform_neutral_framework()
    {
        var assembly = typeof(DurationFormat).Assembly;

        Assert.Null(assembly.GetCustomAttribute<System.Runtime.Versioning.TargetPlatformAttribute>());

        // Guardia aggiuntiva, non equivalente a quella sopra: pin sulla versione major del TFM
        // (.NET 10), indifferente al qualificatore di piattaforma — copre una regressione diversa
        // (es. downgrade a net8.0), non quella di -windows.
        var tfm = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()!.FrameworkName;
        Assert.Equal(".NETCoreApp,Version=v10.0", tfm);
    }
}
