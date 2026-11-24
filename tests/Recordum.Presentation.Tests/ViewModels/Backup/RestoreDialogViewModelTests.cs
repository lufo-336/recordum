using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Backup;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Backup;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Backup;

public class RestoreDialogViewModelTests
{
    [Fact]
    public async Task PreviewCommand_populates_manifest_summary()
    {
        var restore = new FakeRestoreService();
        var vm = new RestoreDialogViewModel(restore, new FakeOpenFileDialog { Result = "x.recbak" });
        vm.BrowseCommand.Execute(null);
        vm.Passphrase = "a-passphrase-long";

        await vm.PreviewCommand.ExecuteAsync(null);

        vm.Manifest.Should().NotBeNull();
        vm.Manifest!.SchemaVersion.Should().Be("2.7");
        vm.Warnings.Should().BeEmpty();
        vm.IsPreviewLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task RunCommand_disabled_until_preview_loaded()
    {
        var vm = new RestoreDialogViewModel(new FakeRestoreService(), new FakeOpenFileDialog { Result = "x.recbak" });
        vm.BrowseCommand.Execute(null);
        vm.Passphrase = "a-passphrase-long";

        vm.RunCommand.CanExecute(null).Should().BeFalse();
        await vm.PreviewCommand.ExecuteAsync(null);
        vm.RunCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task RunCommand_surfaces_unavailable_plugins()
    {
        var restore = new FakeRestoreService { Unavailable = ["io.example.missing"] };
        var vm = new RestoreDialogViewModel(restore, new FakeOpenFileDialog { Result = "x.recbak" });
        vm.BrowseCommand.Execute(null);
        vm.Passphrase = "a-passphrase-long";
        await vm.PreviewCommand.ExecuteAsync(null);

        await vm.RunCommand.ExecuteAsync(null);

        vm.UnavailablePlugins.Should().ContainSingle().Which.Should().Be("io.example.missing");
        vm.IsCompleted.Should().BeTrue();
    }

    [Theory]
    [InlineData("busy")]
    [InlineData("swap_failed")]
    public async Task RunCommand_turns_a_busy_or_failed_swap_into_a_readable_message(string code)
    {
        var restore = new FakeRestoreService { RestoreThrows = new RestoreException(code, "raw detail") };
        var vm = new RestoreDialogViewModel(restore, new FakeOpenFileDialog { Result = "x.recbak" });
        vm.BrowseCommand.Execute(null);
        vm.Passphrase = "a-passphrase-long";
        await vm.PreviewCommand.ExecuteAsync(null);

        await vm.RunCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be(code == "busy" ? Strings.Restore_Errors_Busy : Strings.Restore_Errors_SwapFailed);
        vm.IsCompleted.Should().BeFalse();
    }

    [Theory]
    [InlineData(SyncJoinState.Joined, true)]
    [InlineData(SyncJoinState.NotConfigured, false)]
    public async Task RestoreConfirmationMentionsSyncWhenActive(SyncJoinState state, bool expectedMention)
    {
        var sync = new StubSyncService(state);
        var vm = new RestoreDialogViewModel(new FakeRestoreService(), new FakeOpenFileDialog { Result = "x.recbak" }, sync);
        vm.BrowseCommand.Execute(null);
        vm.Passphrase = "a-passphrase-long";

        await vm.PreviewCommand.ExecuteAsync(null);

        vm.IsPreviewLoaded.Should().BeTrue();
        if (expectedMention)
            vm.Warnings.Should().Contain(Strings.Restore_SyncActiveNote);
        else
            vm.Warnings.Should().NotContain(Strings.Restore_SyncActiveNote);
    }

    [Fact]
    public async Task SyncServiceFailureDoesNotBreakRestorePreview()
    {
        var vm = new RestoreDialogViewModel(new FakeRestoreService(), new FakeOpenFileDialog { Result = "x.recbak" }, new StubSyncService(SyncJoinState.Joined, throws: true));
        vm.BrowseCommand.Execute(null);
        vm.Passphrase = "a-passphrase-long";

        await vm.PreviewCommand.ExecuteAsync(null);

        vm.IsPreviewLoaded.Should().BeTrue();
        vm.ErrorMessage.Should().BeNullOrEmpty();
        vm.Warnings.Should().NotContain(Strings.Restore_SyncActiveNote);
    }

    private sealed class StubSyncService(SyncJoinState state, bool throws = false) : ISyncService
    {
        public Task<SyncRunResult> RunAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<SyncStatus> GetStatusAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SyncItemToFix>> ListItemsToFixAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task AcceptRepairAsync(Guid periodId, CancellationToken ct) => throw new NotSupportedException();
        public Task SetPassphraseAsync(string passphrase, CancellationToken ct) => throw new NotSupportedException();
        public Task<SyncJoinState> GetJoinStateAsync(CancellationToken ct) => throws ? throw new InvalidOperationException("boom") : Task.FromResult(state);
        public Task JoinAsync(SyncJoinChoice choice, CancellationToken ct) => throw new NotSupportedException();
        public Task DisconnectAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task ExportPlainAsync(Stream rcsync, Stream zipOut, string passphrase, CancellationToken ct) => throw new NotSupportedException();
        public Task ReencryptAsync(Stream zipIn, Stream rcsyncOut, string passphrase, CancellationToken ct) => throw new NotSupportedException();
    }
}
