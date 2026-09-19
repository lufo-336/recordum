using FluentAssertions;
using Recordum.Core.Backup;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Backup;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Backup;

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
}
