using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.ExtensionPoints;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Export;

namespace Recordum.WPF.Tests.ViewModels.Export;

public sealed class ExportDialogViewModelTests
{
    private static ExportDialogViewModel Build(FakeSaveFileDialog save, FakeExportJobService jobs, FakeExportFormatter formatter, FakeExportQueryService? query = null)
    {
        var registry = new ExtensionPointRegistry();
        registry.Register(new ExtensionRegistration(formatter.PluginId, formatter.PointId, formatter));
        return new ExportDialogViewModel(query ?? new FakeExportQueryService(), jobs, registry, save, new FakeExportTemplateService(), new FakeClock());
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
