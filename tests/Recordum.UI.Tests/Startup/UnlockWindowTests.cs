using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.Startup;
using Recordum.UI.Views.Startup;
using Xunit;

namespace Recordum.UI.Tests.Startup;

public class UnlockWindowTests
{
    private sealed class NoopService : IDbUnlockService
    {
        public OrphanReconciliation ReconcileOrphans() => OrphanReconciliation.None;
        public DbUnlockState DetectState() => DbUnlockState.NeedsPassphrase;
        public Task<UnlockResult> UnlockAsync(DbUnlockState state, string passphrase, bool rememberOnDevice, CancellationToken ct = default) =>
            Task.FromResult(new UnlockResult(UnlockOutcome.Failed, null));
        public Task<StoredKeyResult> TryUnlockWithStoredKeyAsync(CancellationToken ct = default) =>
            Task.FromResult(new StoredKeyResult(StoredKeyOutcome.NoStoredKey, null));
        public bool CanRememberOnDevice => true;
    }

    private static UnlockWindow Show(bool canRemember, bool rejected, DbUnlockState state = DbUnlockState.NeedsPassphrase)
    {
        var window = new UnlockWindow(new UnlockViewModel(new NoopService(), state, canRemember, rejected));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Named<T>(UnlockWindow w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaFact]
    public void The_remember_checkbox_is_there_checked_and_named()
    {
        var window = Show(canRemember: true, rejected: false);
        try
        {
            var box = Named<CheckBox>(window, "RememberBox");
            box.IsVisible.Should().BeTrue();
            box.IsEnabled.Should().BeTrue();
            box.IsChecked.Should().BeTrue();
            AutomationProperties.GetName(box).Should().Be(Strings.Unlock_RememberOnDevice);
            Named<TextBlock>(window, "RememberUnavailableText").IsVisible.Should().BeFalse();
            Named<TextBlock>(window, "StoredKeyRejectedText").IsVisible.Should().BeFalse();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Without_a_keyring_the_checkbox_is_off_and_the_explanation_is_visible()
    {
        var window = Show(canRemember: false, rejected: false);
        try
        {
            var box = Named<CheckBox>(window, "RememberBox");
            box.IsEnabled.Should().BeFalse();
            box.IsChecked.Should().BeFalse();
            var hint = Named<TextBlock>(window, "RememberUnavailableText");
            hint.IsVisible.Should().BeTrue();
            hint.Text.Should().Be(Strings.Unlock_RememberUnavailable);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void A_rejected_stored_key_shows_its_line_above_the_field()
    {
        var window = Show(canRemember: true, rejected: true);
        try
        {
            var line = Named<TextBlock>(window, "StoredKeyRejectedText");
            line.IsVisible.Should().BeTrue();
            line.Text.Should().Be(Strings.Unlock_StoredKeyRejected);
            line.Bounds.Y.Should().BeLessThan(Named<TextBox>(window, "PassphraseBox").Bounds.Y);
        }
        finally { window.Close(); }
    }
}
