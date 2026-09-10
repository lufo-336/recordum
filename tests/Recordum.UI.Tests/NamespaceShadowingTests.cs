using Xunit;

namespace Recordum.UI.Tests;

public class NamespaceShadowingTests
{
    [Fact]
    public void Root_Avalonia_namespace_resolves_without_a_global_prefix()
    {
        // Dentro un tipo di un namespace Recordum.*, il nome `Avalonia` deve risolvere al namespace
        // radice del framework. Finché il progetto si chiamava Recordum.Avalonia, la ricerca trova
        // PRIMA quel namespace, che non ha un membro `Threading`, e questa riga non compila:
        // serve `global::Avalonia.Threading.Dispatcher`. Dopo la rinomina compila da sé.
        // L'assert è deliberatamente su typeof e non su Dispatcher.UIThread: la prova che questo
        // test porta è di COMPILAZIONE (prima della rinomina il file non compilava affatto), e un
        // assert a runtime potrebbe fallire per una ragione diversa — nessuna piattaforma Avalonia
        // inizializzata in un [Fact] semplice — rendendo il rosso ambiguo.
        Assert.Equal("Avalonia.Threading.Dispatcher", typeof(Avalonia.Threading.Dispatcher).FullName);
    }
}
