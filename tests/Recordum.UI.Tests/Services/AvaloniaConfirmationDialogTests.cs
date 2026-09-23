using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FluentAssertions;
using Recordum.UI.Services;
using Recordum.UI.Views.Startup;
using Xunit;

namespace Recordum.UI.Tests.Services;

/// <summary>La dialog di conferma vera (Home Elimina/Purge non può fidarsi di un Null che risponde
/// sempre <c>true</c>). I test cliccano DAVVERO i due bottoni via <c>Avalonia.Headless</c>
/// (MouseDown/MouseUp su una coordinata reale, non <c>button.RaiseEvent(ClickEvent)</c>): se il
/// cablaggio Click→Close si rompesse, un test che invocasse Click a mano non se ne accorgerebbe.</summary>
public class AvaloniaConfirmationDialogTests
{
    [AvaloniaFact]
    public async Task Clicking_Yes_resolves_true()
    {
        var owner = new Window { Width = 200, Height = 100 };
        owner.Show();
        var sut = new AvaloniaConfirmationDialog(() => owner);
        ConfirmationWindow? dialog = null;
        sut.DialogCreatedForTests += w => dialog = w;

        var task = sut.ConfirmAsync("Elimina", "Sei sicuro?");
        Dispatcher.UIThread.RunJobs();

        Click(dialog!, dialog!.YesButton);

        (await task).Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task Clicking_No_resolves_false()
    {
        var owner = new Window { Width = 200, Height = 100 };
        owner.Show();
        var sut = new AvaloniaConfirmationDialog(() => owner);
        ConfirmationWindow? dialog = null;
        sut.DialogCreatedForTests += w => dialog = w;

        var task = sut.ConfirmAsync("Elimina", "Sei sicuro?");
        Dispatcher.UIThread.RunJobs();

        Click(dialog!, dialog!.NoButton);

        (await task).Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task With_no_owner_it_still_shows_and_resolves()
    {
        // Fase di avvio senza MainWindow ancora assegnata, o un test headless: ShowDialog non è
        // disponibile (vuole un owner), quindi la classe ripiega su Show()+attesa della chiusura —
        // v. commento in AvaloniaConfirmationDialog.ShowWithoutOwnerAsync.
        var sut = new AvaloniaConfirmationDialog(() => null);
        ConfirmationWindow? dialog = null;
        sut.DialogCreatedForTests += w => dialog = w;

        var task = sut.ConfirmAsync("Elimina", "Sei sicuro?");
        Dispatcher.UIThread.RunJobs();

        Click(dialog!, dialog!.YesButton);

        (await task).Should().BeTrue();
    }

    [AvaloniaFact]
    public void The_dialog_shows_the_title_and_message_it_was_given()
    {
        var window = new ConfirmationWindow("Elimina abitudine", "Non si può annullare.");
        window.Show(); // v. commento nel finally: senza Show() la pulizia di fine test corre su un altro thread
        try
        {
            window.Title.Should().Be("Elimina abitudine");
            // Il messaggio vive nel primo TextBlock del contenuto: lo stesso testo passato al ctor,
            // non un placeholder — se qualcuno lo dimenticasse di passare, questo test lo vedrebbe.
            var text = ((StackPanel)window.Content!).Children.OfType<TextBlock>().Single().Text;
            text.Should().Be("Non si può annullare.");
        }
        finally
        {
            // Una Window Avalonia mai mostrata NÉ chiusa esplicitamente sopravvive al termine del
            // test e la sua pulizia finisce su un thread diverso da quello del test (misurato:
            // "InvalidOperationException: The calling thread cannot access this object because a
            // different thread owns it" — un errore di CLEANUP del test, non un fallimento
            // dell'asserzione, ma xunit lo conta come rosso lo stesso). Chiuderla qui evita il
            // problema per qualunque Window costruita e mai mostrata in questo progetto di test.
            window.Close();
        }
    }

    private static void Click(Window dialog, Button button)
    {
        Dispatcher.UIThread.RunJobs();
        var center = new Point(button.Bounds.Width / 2, button.Bounds.Height / 2);
        var inWindow = button.TranslatePoint(center, dialog) ?? throw new InvalidOperationException(
            "il bottone non è ancora nell'albero visivo della finestra: manca un giro di Dispatcher");
        dialog.MouseDown(inWindow, MouseButton.Left);
        dialog.MouseUp(inWindow, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
