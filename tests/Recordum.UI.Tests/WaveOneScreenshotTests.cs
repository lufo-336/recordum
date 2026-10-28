using System.Collections.Immutable;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Quick;
using Recordum.Presentation.ViewModels.Startup;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Startup;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>I PNG dell'onda 1 della direzione visiva, in chiaro e in scuro, per le schermate che la suite non fotografava:
/// la Home stretta e vuota, lo sblocco (primo avvio e normale), l'errore fatale. Un PNG da solo non prova niente: ognuno
/// ha la sua asserzione strutturale.</summary>
public class WaveOneScreenshotTests
{
    public static TheoryData<string> Themes => new() { "Light", "Dark" };

    private static ThemeVariant Variant(string theme) => theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

    private static void Pump()
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs();
    }

    private static HomeTreeRow Row(string name, HabitType type) =>
        new(Guid.NewGuid(), null, name, "#2F7FC1", type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(1, 0, 1, 1, 0, 1, 1), ImmutableArray<Guid>.Empty);

    private static (Window W, HomeView V) Home(double width, string theme, params HomeTreeRow[] rows)
    {
        var query = new FakeHabitTreeQueryService();
        query.Rows.AddRange(rows);
        var vm = new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService(),
            quickActions: new QuickActionHandlerSelector([]),
            clock: new PrivateDbHost.FakeClock { Now = new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero) });
        var view = new HomeView { DataContext = vm };
        var w = new Window { Width = width, Height = 420, Content = view, RequestedThemeVariant = Variant(theme) };
        w.Show();
        vm.LoadCommand.Execute(null);
        Pump();
        return (w, view);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Narrow_home(string theme)
    {
        using var culture = TestCulture.Pin();
        var (w, view) = Home(280, theme, Row("Una camminata lunga dopo cena", HabitType.Action), Row("Acqua", HabitType.Counter));
        view.FindControl<TextBlock>("TodayDate")!.IsVisible.Should().BeFalse();
        view.GetVisualDescendants().OfType<Views.Rows.HabitRow>().Should().HaveCount(2);
        ScreenshotProbeTests.Capture(w, $"home-narrow-{theme.ToLowerInvariant()}");
        w.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Empty_home(string theme)
    {
        using var culture = TestCulture.Pin();
        var (w, view) = Home(520, theme);
        view.GetVisualDescendants().OfType<EmptyState>().Should().ContainSingle()
            .Which.GetVisualDescendants().OfType<Button>().Should().ContainSingle(b => b.Classes.Contains("primary"));
        ScreenshotProbeTests.Capture(w, $"home-empty-{theme.ToLowerInvariant()}");
        w.Close();
    }

    private sealed class NoopService : IDbUnlockService
    {
        public OrphanReconciliation ReconcileOrphans() => OrphanReconciliation.None;
        public DbUnlockState DetectState() => DbUnlockState.NeedsPassphrase;
        public Task<UnlockResult> UnlockAsync(DbUnlockState state, string passphrase, RememberChoice remember, CancellationToken ct = default) =>
            Task.FromResult(new UnlockResult(UnlockOutcome.Failed, null));
        public Task<StoredKeyResult> TryUnlockWithStoredKeyAsync(CancellationToken ct = default) =>
            Task.FromResult(new StoredKeyResult(StoredKeyOutcome.NoStoredKey, null));
        public bool CanRememberOnDevice => true;
    }

    [AvaloniaTheory]
    [InlineData("Light", DbUnlockState.NeedsPassphrase, "unlock")]
    [InlineData("Dark", DbUnlockState.NeedsPassphrase, "unlock")]
    [InlineData("Light", DbUnlockState.NeedsCreation, "unlock-first-run")]
    [InlineData("Dark", DbUnlockState.NeedsCreation, "unlock-first-run")]
    public void Unlock(string theme, DbUnlockState state, string name)
    {
        using var culture = TestCulture.Pin();
        var w = new UnlockWindow(new UnlockViewModel(new NoopService(), state, true, false)) { RequestedThemeVariant = Variant(theme) };
        w.Show();
        Pump();
        w.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "ConfirmBlock").IsVisible
            .Should().Be(state == DbUnlockState.NeedsCreation, "al primo avvio la passphrase si conferma");
        ScreenshotProbeTests.Capture(w, $"{name}-{theme.ToLowerInvariant()}");
        w.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Fatal_error(string theme)
    {
        using var culture = TestCulture.Pin();
        var w = new FatalErrorWindow("Il database non si apre.", null) { RequestedThemeVariant = Variant(theme) };
        w.Show();
        Pump();
        w.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Text == "Il database non si apre.");
        ScreenshotProbeTests.Capture(w, $"fatal-error-{theme.ToLowerInvariant()}");
        w.Close();
    }
}
