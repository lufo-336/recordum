using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Recordum.Core.Abstractions;
using Recordum.UI.Services;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>
/// La metà vera dello Step 6-bis (review Task 4, giro 1): <c>Recordum.Core.Tests</c> esercita solo
/// <c>FakeTickSourceFactory</c>, che non referenzia mai questa classe — quel test prova il contratto
/// del fake, non la produzione. Questo progetto gira su questa macchina e referenzia
/// <c>Recordum.UI</c>, quindi è il posto giusto per la sorgente reale.
/// </summary>
public class DispatcherTickSourceTests
{
    [AvaloniaFact]
    public void A_disposed_source_does_not_restart()
    {
        var factory = new DispatcherTickSourceFactory();
        var ticks = 0;
        var source = factory.Create(TimeSpan.FromMilliseconds(20));
        source.Tick += (_, _) => ticks++;

        source.Dispose();
        source.Start();      // misuso: dopo Dispose non deve riaccendere niente
        Thread.Sleep(100);
        Dispatcher.UIThread.RunJobs();

        // Non osservabile dal solo evento pubblico: Dispose() ha già staccato l'handler interno
        // (_timer.Tick -= OnTick) prima che il misuso possa avvenire, quindi il consumatore non
        // vedrebbe comunque nulla anche SENZA la guardia su Start() — verificato togliendo la
        // guardia e osservando che questa sola asserzione resta verde (v. task-4-report.md).
        // La guardia protegge una cosa diversa: se il DispatcherTimer nativo torna "acceso" dopo
        // Dispose, cioè esattamente il leak descritto nel commento accanto a Dispose(). Quella è
        // l'unica asserzione che va rossa togliendo `if (_disposed) return;`.
        Assert.Equal(0, ticks);
        Assert.False(NativeTimerIsEnabled(source), "il timer nativo non deve restare acceso dopo Dispose()");
    }

    /// <summary>Riflette nel campo privato <c>_timer</c> di <c>DispatcherTickSource</c> (classe
    /// annidata privata: nessuna API pubblica espone lo stato del <see cref="DispatcherTimer"/>
    /// nativo, di proposito — è un dettaglio implementativo). È l'unico modo per osservare da qui
    /// se la guardia ha davvero impedito il riavvio, dato che l'evento pubblico non lo rivela mai
    /// (v. commento sopra).</summary>
    private static bool NativeTimerIsEnabled(ITickSource source)
    {
        var timerField = source.GetType().GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "DispatcherTickSource non ha più un campo _timer: aggiornare questo test riflesso.");
        var timer = timerField.GetValue(source)
            ?? throw new InvalidOperationException("_timer è null.");
        var isEnabledProp = timer.GetType().GetProperty("IsEnabled")
            ?? throw new InvalidOperationException("DispatcherTimer non espone più IsEnabled.");
        return (bool)isEnabledProp.GetValue(timer)!;
    }
}
