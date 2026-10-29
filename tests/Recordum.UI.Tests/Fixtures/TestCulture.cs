using System.Globalization;

namespace Recordum.UI.Tests.Fixtures;

/// <summary>Fissa it-IT (la griglia segue il primo giorno della settimana della cultura) e la ripristina al Dispose:
/// l'assembly UI gira in serie su un solo thread, una cultura non ripristinata contagia i test successivi (CLAUDE.md 7).</summary>
internal static class TestCulture
{
    public static IDisposable Pin()
    {
        var (c, u) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        var (dc, du) = (CultureInfo.DefaultThreadCurrentCulture, CultureInfo.DefaultThreadCurrentUICulture);
        var it = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = it;
        CultureInfo.CurrentUICulture = it;
        // Anche il default, come fa l'app (CultureSetup): i job del dispatcher accodati prima del Pin non portano la
        // cultura del flusso asincrono, e Configura usciva mezza in inglese nei PNG (UX-P1).
        CultureInfo.DefaultThreadCurrentCulture = it;
        CultureInfo.DefaultThreadCurrentUICulture = it;
        return new Restore(() =>
        {
            CultureInfo.CurrentCulture = c;
            CultureInfo.CurrentUICulture = u;
            CultureInfo.DefaultThreadCurrentCulture = dc;
            CultureInfo.DefaultThreadCurrentUICulture = du;
        });
    }

    private sealed class Restore(Action undo) : IDisposable { public void Dispose() => undo(); }
}
