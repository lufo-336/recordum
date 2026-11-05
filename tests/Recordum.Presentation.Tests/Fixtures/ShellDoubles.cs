using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Theming;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Export;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.ViewModels.HabitEditor;

namespace Recordum.Presentation.Tests.Fixtures;

/// <summary>Ciò che una shell (WPF, Avalonia) deve aggiungere a <c>AddRecordumCore</c> per avere un
/// grafo completo: i servizi che toccano la piattaforma. Nei test sono doppi inerti. L'elenco è
/// quello di <c>RecordumServiceCollectionExtensions</c> "cosa NON registra": se un giorno core ne
/// registra uno, <c>CoreCompositionTests.Core_does_not_register_the_platform_services</c> scatta.</summary>
public static class ShellDoubles
{
    public static IServiceCollection AddShellDoubles(this IServiceCollection services, IDbKeyProvider? keyProvider = null)
    {
        services.AddSingleton<IDbKeyProvider>(keyProvider ?? new FixedKeyProvider(new byte[KeyMaterial.LengthBytes]));
        services.AddSingleton<IThemeService, FakeThemeService>();
        services.AddSingleton<ITickSourceFactory>(NullTickSourceFactory.Instance);
        services.AddSingleton<IApplicationLifetime, FakeApplicationLifetime>();
        services.AddSingleton<IConfirmationDialog, FakeConfirmationDialog>();
        services.AddSingleton<IHabitTemplatePickerLauncher, FakeHabitTemplatePickerLauncher>();
        services.AddSingleton<IHabitBuilderLauncher, StubHabitBuilderLauncher>();
        services.AddSingleton<ISettingsWindowLauncher, FakeSettingsWindowLauncher>();
        services.AddSingleton<IExportDialogLauncher, FakeExportDialogLauncher>();
        services.AddSingleton<IBackupDialogLauncher, FakeBackupDialogLauncher>();
        services.AddSingleton<ISaveFileDialog, FakeSaveFileDialog>();
        services.AddSingleton<IBackupSaveFileDialog, FakeBackupSaveFileDialog>();
        services.AddSingleton<IOpenFileDialog, FakeOpenFileDialog>();
        services.AddSingleton<IFolderPicker, FakeFolderPicker>();
        services.AddSingleton<IFolderOpener, FakeFolderOpener>();
        services.AddSingleton<IClipboard, StubClipboard>();
        services.AddSingleton<IScreenColorPicker, FakeScreenColorPicker>();
        services.AddSingleton<IAutoBackupKeyStore, FakeAutoBackupKeyStore>();
        services.AddSingleton<IEditorAvailability>(new FakeEditorAvailability(available: false));
        return services;
    }

    private sealed class StubHabitBuilderLauncher : IHabitBuilderLauncher
    {
        public Task<Guid?> LaunchAsync(Guid? parentId = null, CancellationToken ct = default) =>
            Task.FromResult<Guid?>(null);

        public Task<Guid?> LaunchFromTemplateAsync(Guid templateId, Guid? parentId = null, CancellationToken ct = default) =>
            Task.FromResult<Guid?>(null);
    }

    private sealed class StubClipboard : IClipboard
    {
        public void SetText(string text) { }
    }
}
