using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;
using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.ExtensionPoints;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Export;
using Recordum.Core.Time;

namespace Recordum.Presentation.Tests.ViewModels.Export;

public sealed class ExportDialogViewModelTests
{
    private static ExportDialogViewModel Build(FakeSaveFileDialog save, FakeExportJobService jobs, FakeExportFormatter formatter, FakeExportQueryService? query = null)
    {
        var registry = new ExtensionPointRegistry();
        registry.Register(new ExtensionRegistration(formatter.PluginId, formatter.PointId, formatter));
        return new ExportDialogViewModel(query ?? new FakeExportQueryService(), jobs, registry, save, new FakeExportTemplateService(), new FakeClock(), new FakeResetTimeResolver());
    }

    [Fact]
    public async Task Export_resolves_formatter_writes_file_and_records_job()
    {
        var save = new FakeSaveFileDialog { Result = "C:\\tmp\\out.json" };
        var jobs = new FakeExportJobService();
        var formatter = new FakeExportFormatter(ExportFormat.Json);
        var vm = Build(save, jobs, formatter);
        vm.LoadForHabit(Guid.NewGuid(), "Run");
        vm.SelectedFormat = ExportFormat.Json;

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.True(formatter.WriteCalled);
        Assert.Equal("C:\\tmp\\out.json", save.LastSavedPath);
        Assert.Single(jobs.Recorded);
        Assert.Equal(ExportFormat.Json, jobs.Recorded[0].Format);
    }

    [Fact]
    public async Task Export_with_accessory_files_writes_a_folder_with_document_and_images()
    {
        var root = Path.Combine(Path.GetTempPath(), $"recordum-export-{Guid.NewGuid():N}");
        var save = new FakeSaveFileDialog { Result = Path.Combine(root, "diary.md") };
        var formatter = new FakeExportFormatter(ExportFormat.Markdown)
        {
            Output = new ExportOutput([7], [new ExportFile("images/a.jpg", [1, 2])]),
        };
        var vm = Build(save, new FakeExportJobService(), formatter);
        vm.LoadForHabit(Guid.NewGuid(), "Run");
        vm.SelectedFormat = ExportFormat.Markdown;

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal(
            [Path.Combine(root, "diary", "diary.md"), Path.Combine(root, "diary", "images", "a.jpg")],
            save.Writes.Select(w => w.Path));
    }

    [Fact]
    public async Task Export_with_accessory_files_does_not_reuse_an_existing_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"recordum-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "diary"));
        try
        {
            var save = new FakeSaveFileDialog { Result = Path.Combine(root, "diary.md") };
            var formatter = new FakeExportFormatter(ExportFormat.Markdown)
            {
                Output = new ExportOutput([7], [new ExportFile("images/a.jpg", [1])]),
            };
            var vm = Build(save, new FakeExportJobService(), formatter);
            vm.LoadForHabit(Guid.NewGuid(), "Run");
            vm.SelectedFormat = ExportFormat.Markdown;

            await vm.ExportCommand.ExecuteAsync(null);

            Assert.Equal(Path.Combine(root, "diary (2)", "diary.md"), save.Writes[0].Path);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Export_with_an_unsafe_relative_path_writes_nothing_and_fails()
    {
        var save = new FakeSaveFileDialog { Result = Path.Combine("dir", "diary.md") };
        var formatter = new FakeExportFormatter(ExportFormat.Markdown)
        {
            Output = new ExportOutput([7], [new ExportFile("../x.jpg", [1])]),
        };
        var jobs = new FakeExportJobService();
        var vm = Build(save, jobs, formatter);
        vm.LoadForHabit(Guid.NewGuid(), "Run");
        vm.SelectedFormat = ExportFormat.Markdown;

        await Assert.ThrowsAsync<InvalidDataException>(() => vm.ExportCommand.ExecuteAsync(null));

        Assert.Empty(save.Writes);
        Assert.Empty(jobs.Recorded);
    }

    [Fact]
    public async Task Export_cancelled_save_dialog_does_nothing()
    {
        var save = new FakeSaveFileDialog { Result = null };
        var jobs = new FakeExportJobService();
        var formatter = new FakeExportFormatter(ExportFormat.Json);
        var vm = Build(save, jobs, formatter);
        vm.LoadForHabit(Guid.NewGuid(), "Run");

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Empty(jobs.Recorded);
    }

    [Fact]
    public async Task Section_toggles_propagate_into_the_request_layout()
    {
        var save = new FakeSaveFileDialog { Result = "C:\\tmp\\out.json" };
        var query = new FakeExportQueryService();
        var vm = Build(save, new FakeExportJobService(), new FakeExportFormatter(ExportFormat.Json), query);
        vm.LoadForHabit(Guid.NewGuid(), "Run");
        vm.SelectedFormat = ExportFormat.Json;
        vm.IncludeNotes = false;
        vm.IncludeHeatmap = false;

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.NotNull(query.LastRequest);
        Assert.False(query.LastRequest!.Layout.Notes);
        Assert.False(query.LastRequest.Layout.Heatmap);
        Assert.True(query.LastRequest.Layout.Completions);
    }
}
