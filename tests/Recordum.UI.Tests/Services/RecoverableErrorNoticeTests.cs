using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using Recordum.Core.Errors;
using Recordum.Presentation.Resources;
using Recordum.UI.Diagnostics;
using Recordum.UI.Services;
using Recordum.UI.Views.Startup;
using Xunit;

namespace Recordum.UI.Tests.Services;

/// <summary>F2a-11: un errore di dominio gestito si vede — un avviso con solo "OK", uno alla volta.</summary>
public class RecoverableErrorNoticeTests
{
    [AvaloniaFact]
    public void A_domain_error_shows_one_ok_notice_and_a_second_one_while_it_is_open_does_not_stack()
    {
        var root = Path.Combine(Path.GetTempPath(), $"recordum-notice-{Guid.NewGuid():N}");
        var owner = new Window { Width = 400, Height = 300 };
        var shown = new List<ConfirmationWindow>();
        try
        {
            owner.Show();
            CrashLogger.ResetForTests();
            CrashLogger.WireUp(root);
            using var notice = RecoverableErrorNotice.Attach(() => owner);
            notice.NoticeShownForTests += shown.Add;

            Dispatcher.UIThread.Post(() => throw new ActiveTimerNotFoundException(Guid.NewGuid()));
            Dispatcher.UIThread.Post(() => throw new HabitNodeNotFoundException(Guid.NewGuid()));
            Dispatcher.UIThread.RunJobs();

            shown.Should().ContainSingle("un secondo errore mentre l'avviso è aperto non ne apre un altro");
            var window = shown[0];
            window.Title.Should().Be(Strings.Error_Recoverable_Title);
            window.NoButton.IsVisible.Should().BeFalse();
            window.YesButton.Content.Should().Be(Strings.Dialog_Ok);

            window.Close();
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.Post(() => throw new ActiveTimerNotFoundException(Guid.NewGuid()));
            Dispatcher.UIThread.RunJobs();
            shown.Should().HaveCount(2, "chiuso il primo, un errore nuovo si avvisa di nuovo");
            shown[1].Close();
        }
        finally
        {
            CrashLogger.ResetForTests();
            owner.Close();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
