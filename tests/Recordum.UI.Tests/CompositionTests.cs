using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Enums;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Charts;
using Recordum.Presentation.ViewModels.DiagnosticInfo;
using Recordum.Presentation.ViewModels.Export;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.Settings;
using Recordum.Presentation.ViewModels.Startup;
using Recordum.Presentation.ViewModels.Timer;
using Recordum.Presentation.ViewModels.Workspace;
using Recordum.UI.Bootstrap;
using Recordum.UI.Diagnostics;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Startup;
using Xunit;

namespace Recordum.UI.Tests;

public class CompositionTests
{
    [AvaloniaFact]
    public void The_whole_view_model_graph_resolves_from_the_real_container()
    {
        var services = ShellFixture.Shared.Services;

        // I ViewModel della Home: se uno solo di questi non si risolve, la finestra non si apre.
        services.GetRequiredService<MainWindowViewModel>().Should().NotBeNull();
        services.GetRequiredService<HomeViewModel>().Should().NotBeNull();
        services.GetRequiredService<WorkspaceViewModel>().Should().NotBeNull();
        services.GetRequiredService<TimerStatusBarViewModel>().Should().NotBeNull();
        services.GetRequiredService<ChartsWorkspaceViewModel>().Should().NotBeNull();
        services.GetRequiredService<MainWindow>().Should().NotBeNull();

        // I ViewModel che F1 non mostra ma che il grafo costruisce comunque. HabitScreenViewModel ha
        // 26 dipendenze e nasce al primo clic su una riga: deve risolvere gia' ora, anche se la sua
        // view arriva in F2 (D12).
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<HabitEditorViewModel>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<SettingsWindowViewModel>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<HabitBuilderViewModel>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ExportDialogViewModel>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<DiagnosticInfoViewModel>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<TimerRecoveryService>().Should().NotBeNull();
    }

    [AvaloniaFact]
    public void Every_deferred_service_has_a_double_so_nothing_throws_when_it_is_touched()
    {
        var services = ShellFixture.Shared.Services;

        // Le interfacce che in F1 non hanno una finestra vera dietro: devono esistere, perche' il
        // costruttore di MainWindowViewModel e di HabitScreenViewModel le pretende.
        services.GetRequiredService<IConfirmationDialog>().Should().NotBeNull();
        services.GetRequiredService<ISettingsWindowLauncher>().Should().NotBeNull();
        services.GetRequiredService<IExportDialogLauncher>().Should().NotBeNull();
        services.GetRequiredService<IOpenFileDialog>().Should().NotBeNull();
        services.GetRequiredService<IClipboard>().Should().NotBeNull();
        services.GetRequiredService<IEditorAvailability>().Should().NotBeNull();
    }

    [AvaloniaTheory]
    [InlineData("it-IT", "2,5", 2.5, "2.5")]
    [InlineData("en-US", "2.5", 2.5, "2,5")]
    public void A_numeric_field_follows_the_thread_culture_and_refuses_the_other_separator(
        string culture, string accepted, double expected, string refused)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureSetup.Apply(CultureInfo.GetCultureInfo(culture), CultureInfo.GetCultureInfo(culture));

            var model = new CultureProbeModel();
            var view = new CultureProbeView { DataContext = model };
            var window = new Window { Width = 300, Height = 200, Content = view };
            window.Show();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            var box = view.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "Amount");

            box.Text = accepted;
            Dispatcher.UIThread.RunJobs();
            ((double)model.Amount).Should().Be(expected);
            DataValidationErrors.GetHasErrors(box).Should().BeFalse();

            box.Text = refused;
            Dispatcher.UIThread.RunJobs();
            ((double)model.Amount).Should().Be(expected,
                "con i binding compilati Avalonia RIFIUTA il separatore sbagliato invece di leggere "
                + "25 in silenzio, che e' la trappola W2V-17c di WPF");
            DataValidationErrors.GetHasErrors(box).Should().BeTrue();

            // La data segue la stessa cultura: e' il difetto W2V-1 che su Avalonia non esiste.
            var day = view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Day");
            day.Text.Should().Be(new DateTime(2026, 8, 14).ToString("d", CultureInfo.GetCultureInfo(culture)));
        }
        finally
        {
            CultureSetup.Apply(previous, previous);
        }
    }

    [Fact]
    public void Culture_setup_sets_both_the_current_and_the_default_thread_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureSetup.Apply(AppCulture.Italian, AppCulture.English);
            CultureInfo.CurrentCulture.Name.Should().Be("it-IT");
            CultureInfo.DefaultThreadCurrentCulture!.Name.Should().Be("it-IT",
                "i thread di lavoro devono formattare come quello dell'interfaccia");
            CultureInfo.CurrentUICulture.Name.Should().Be("en-US");
            CultureInfo.DefaultThreadCurrentUICulture!.Name.Should().Be("en-US");
        }
        finally
        {
            CultureSetup.Apply(previous, previousUi);
        }
    }

    [AvaloniaFact]
    public void The_unlock_window_asks_for_a_confirmation_only_when_it_creates_the_database()
    {
        var service = new FakeUnlockService();

        var creating = new UnlockWindow(new UnlockViewModel(service, DbUnlockState.NeedsCreation, canRememberOnDevice: true, storedKeyRejected: false));
        creating.Show();
        Dispatcher.UIThread.RunJobs();
        creating.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "ConfirmBlock")
            .IsVisible.Should().BeTrue();
        creating.Close();

        var entering = new UnlockWindow(new UnlockViewModel(service, DbUnlockState.NeedsPassphrase, canRememberOnDevice: true, storedKeyRejected: false));
        entering.Show();
        Dispatcher.UIThread.RunJobs();
        entering.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "ConfirmBlock")
            .IsVisible.Should().BeFalse();
        entering.Close();
    }

    [AvaloniaFact]
    public async Task The_unlock_window_cannot_be_closed_while_an_unlock_is_in_flight()
    {
        var service = new GatedUnlockService();
        var viewModel = new UnlockViewModel(service, DbUnlockState.NeedsPassphrase, canRememberOnDevice: true, storedKeyRejected: false) { Passphrase = "una-passphrase-lunga" };
        var window = new UnlockWindow(viewModel);
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var submit = viewModel.SubmitCommand.ExecuteAsync(null);
        viewModel.IsBusy.Should().BeTrue("lo sblocco e' in volo");

        window.Close();
        Dispatcher.UIThread.RunJobs();
        closed.Should().BeFalse("chiudere a meta' di uno sblocco spegnerebbe il processo dentro CreateAsync");

        service.Release();
        await submit;
        viewModel.IsBusy.Should().BeFalse();

        window.Close();
        Dispatcher.UIThread.RunJobs();
        closed.Should().BeTrue("a sblocco finito la finestra si chiude come sempre");
    }

    [AvaloniaFact]
    public void The_fatal_window_shows_the_message_and_the_log_path()
    {
        var window = new FatalErrorWindow("qualcosa e' andato storto", "/tmp/recordum.log");
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "MessageText")
            .Text.Should().Be("qualcosa e' andato storto");
        var path = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "LogPathText");
        path.IsVisible.Should().BeTrue();
        path.Text.Should().Be("/tmp/recordum.log");
        window.Close();
    }

    [AvaloniaFact]
    public void The_crash_logger_writes_what_it_is_given()
    {
        // Mai la cartella vera dell'utente: una cartella dei log nella temp del test.
        var root = Path.Combine(Path.GetTempPath(), $"recordum-crashlog-{Guid.NewGuid():N}");
        try
        {
            CrashLogger.ResetForTests();
            CrashLogger.WireUp(Path.Combine(root, "logs"));
            CrashLogger.LogPath.Should().NotBeNull("senza un percorso il log e' disattivato in silenzio");

            var marker = $"sonda-{Guid.NewGuid():N}";
            CrashLogger.Log(marker, new InvalidOperationException("motivo"));

            File.ReadAllText(CrashLogger.LogPath!).Should().Contain(marker).And.Contain("motivo");
        }
        finally
        {
            CrashLogger.ResetForTests();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class GatedUnlockService : IDbUnlockService
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _gate.SetResult();

        public OrphanReconciliation ReconcileOrphans() => OrphanReconciliation.None;

        public DbUnlockState DetectState() => DbUnlockState.NeedsPassphrase;

        public bool CanRememberOnDevice => false;
        public Task<StoredKeyResult> TryUnlockWithStoredKeyAsync(CancellationToken ct = default) =>
            Task.FromResult(new StoredKeyResult(StoredKeyOutcome.NoStoredKey, null));

        public async Task<UnlockResult> UnlockAsync(DbUnlockState state, string passphrase, RememberChoice remember, CancellationToken ct = default)
        {
            await _gate.Task;
            return new UnlockResult(UnlockOutcome.WrongPassphrase, null);
        }
    }

    private sealed class FakeUnlockService : IDbUnlockService
    {
        public OrphanReconciliation ReconcileOrphans() => OrphanReconciliation.None;   // errata 2026-09-30

        public DbUnlockState DetectState() => DbUnlockState.NeedsPassphrase;

        public bool CanRememberOnDevice => false;
        public Task<StoredKeyResult> TryUnlockWithStoredKeyAsync(CancellationToken ct = default) =>
            Task.FromResult(new StoredKeyResult(StoredKeyOutcome.NoStoredKey, null));

        public Task<UnlockResult> UnlockAsync(DbUnlockState state, string passphrase, RememberChoice remember, CancellationToken ct = default)
            => Task.FromResult(new UnlockResult(UnlockOutcome.WrongPassphrase, null));
    }
}
