using Avalonia.Headless.XUnit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Export;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.UI.Bootstrap;
using Recordum.UI.Services;
using Recordum.UI.Tests.Services;
using Xunit;

namespace Recordum.UI.Tests.Bootstrap;

/// <summary>Ogni interfaccia registrata come Null da <see cref="AvaloniaShellServices.AddRecordumAvaloniaShell"/>
/// risolve dal container e si comporta come "annullato"/"non disponibile" — mai come una conferma
/// silenziosa. I quattro servizi reali (<c>ITickSourceFactory</c>, <c>IApplicationLifetime</c>,
/// <c>IThemeService</c>, <c>IConfirmationDialog</c>) si testano insieme in
/// <see cref="All_four_real_services_resolve_in_headless"/> via <c>[AvaloniaFact]</c> — in headless
/// <c>Application.Current.ApplicationLifetime</c> è null, e il cast a <c>IControlledApplicationLifetime</c>
/// non lancia: la registrazione funziona anyway (il NRE verrebbe solo a Shutdown se il lifetime fosse
/// davvero null). Le singole implementazioni sono provate altrove (AvaloniaThemeServiceTests,
/// AvaloniaConfirmationDialogTests, AvaloniaApplicationLifetimeTests) costruendole a mano.</summary>
public class AvaloniaShellServicesTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAppSettingService, FakeAppSettingService>();
        services.AddRecordumAvaloniaShell();
        return services.BuildServiceProvider();
    }

    [AvaloniaFact]
    public void All_four_real_services_resolve_in_headless()
    {
        // La registrazione dei quattro servizi reali richiede Application.Current.ApplicationLifetime
        // (disponibile in un [AvaloniaFact]). Provare che tutti e quattro si risolvono è cruciale:
        // cancellare uno (es. ITickSourceFactory) causa NRE in OnStartup.
        var sp = BuildProvider();

        // Se uno di questi GetRequiredService fallisce, il test dirà quale manca
        sp.GetRequiredService<ITickSourceFactory>().Should().NotBeNull();
        sp.GetRequiredService<IApplicationLifetime>().Should().NotBeNull();
        sp.GetRequiredService<IThemeService>().Should().NotBeNull();
        sp.GetRequiredService<IConfirmationDialog>().Should().NotBeNull();
    }

    [Fact]
    public void Habit_template_picker_launcher_resolves_to_the_real_one_not_the_null()
    {
        var sp = BuildProvider();
        sp.GetRequiredService<IHabitTemplatePickerLauncher>().Should().BeOfType<AvaloniaHabitTemplatePickerLauncher>();
    }

    [Fact]
    public void Habit_builder_launcher_resolves_to_the_real_one_not_the_null()
    {
        var sp = BuildProvider();
        sp.GetRequiredService<IHabitBuilderLauncher>().Should().BeOfType<AvaloniaHabitBuilderLauncher>();
    }

    [Fact]
    public void Settings_window_launcher_resolves_to_the_real_one_not_the_null()
    {
        var sp = BuildProvider();
        sp.GetRequiredService<ISettingsWindowLauncher>().Should().BeOfType<AvaloniaSettingsWindowLauncher>();
    }

    [Fact]
    public async Task Export_dialog_launcher_resolves_and_completes_both_members()
    {
        var sp = BuildProvider();
        var launcher = sp.GetRequiredService<IExportDialogLauncher>();
        await launcher.ShowForHabitAsync(Guid.NewGuid(), "Reading");
        await launcher.ShowForLibraryAsync();
    }

    [AvaloniaFact]
    public void Backup_launcher_and_file_dialogs_resolve_to_the_real_ones_not_the_null()
    {
        var sp = BuildProvider();
        sp.GetRequiredService<IBackupDialogLauncher>().Should().BeOfType<AvaloniaBackupDialogLauncher>();
        sp.GetRequiredService<IBackupSaveFileDialog>().Should().BeOfType<AvaloniaBackupSaveFileDialog>();
        sp.GetRequiredService<IOpenFileDialog>().Should().BeOfType<AvaloniaOpenFileDialog>();
        sp.GetRequiredService<IFolderPicker>().Should().BeOfType<AvaloniaFolderPicker>();
    }

    [Fact]
    public void Save_file_dialog_resolves_and_returns_no_path()
    {
        var sp = BuildProvider();
        var dialog = sp.GetRequiredService<ISaveFileDialog>();
        dialog.Prompt("export.pdf", Recordum.Core.Enums.ExportFormat.Pdf).Should().BeNull();
    }

    [AvaloniaFact]
    public async Task File_dialogs_without_a_main_window_answer_as_cancelled()
    {
        var sp = BuildProvider();
        (await sp.GetRequiredService<IBackupSaveFileDialog>().PromptAsync("backup.recbak", "Backup|*.recbak")).Should().BeNull();
        var open = sp.GetRequiredService<IOpenFileDialog>();
        (await open.PromptAsync("Images|*.png")).Should().BeNull();
        (await open.PromptMultipleAsync("Images|*.png")).Should().NotBeNull().And.BeEmpty();
        (await sp.GetRequiredService<IFolderPicker>().PickFolderAsync(null)).Should().BeNull();
    }

    [Fact]
    public void Clipboard_resolves_and_does_not_throw()
    {
        var sp = BuildProvider();
        var act = () => sp.GetRequiredService<IClipboard>().SetText("hello");
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Screen_color_picker_resolves_and_returns_no_color()
    {
        var sp = BuildProvider();
        var result = await sp.GetRequiredService<IScreenColorPicker>().PickAsync();
        result.Should().BeNull();
    }

    [Fact]
    public void Editor_availability_resolves_and_reports_unavailable()
    {
        var sp = BuildProvider();
        var availability = sp.GetRequiredService<IEditorAvailability>();
        availability.IsAvailable.Should().BeFalse();
    }
}
