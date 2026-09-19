using System.Globalization;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.Tests;

public class StringsResourceTests
{
    /// <summary>Le stringhe traslocate con i ViewModel: se il nome del manifest embedded non coincide con
    /// quello che <c>Strings</c> chiede al ResourceManager, l'errore è un MissingManifestResourceException
    /// a runtime, non a compile time. Questo test lo rende un rosso qui.</summary>
    [Fact]
    public void Strings_resolve_from_the_embedded_resx_in_this_assembly()
    {
        var previous = Strings.Culture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo("en-US");
            var english = Strings.Diagnostic_NotAvailable;
            Assert.False(string.IsNullOrWhiteSpace(english));
            Assert.NotEqual("Diagnostic.NotAvailable", english);

            Strings.Culture = CultureInfo.GetCultureInfo("it-IT");
            var italian = Strings.Diagnostic_NotAvailable;
            Assert.NotEqual(english, italian); // il satellite it è incorporato e viene letto
        }
        finally
        {
            Strings.Culture = previous;
        }
    }
}
