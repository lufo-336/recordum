using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
using Recordum.UI.Services;
using Recordum.UI.Services.Null;
using Recordum.UI.Views.Startup;

namespace Recordum.UI.Bootstrap;

/// <summary>Il set W della shell Avalonia (§2 dell'indagine di composizione): tutto ciò che
/// <c>AddRecordumCore</c> (Task 3, Recordum.Presentation) lascia fuori perché non ha un'unica
/// implementazione portabile. Gemella concettuale di <c>ServiceRegistrar</c> in Recordum.WPF, ma con
/// molte più righe Null che WPF non ha mai avuto bisogno di scrivere — F1 non porta ancora le view
/// dietro quelle interfacce.</summary>
public static class AvaloniaShellServices
{
    public static IServiceCollection AddRecordumAvaloniaShell(this IServiceCollection services)
    {
        services.AddSingleton<ITickSourceFactory, DispatcherTickSourceFactory>();

        // Application.Current.ApplicationLifetime non esiste ancora quando questo metodo gira
        // (App.axaml.cs lo chiama prima di OnFrameworkInitializationCompleted, v. Task 11): la
        // lambda è quindi una fabbrica valutata alla RISOLUZIONE, non alla registrazione, quando il
        // framework l'ha già assegnata. Il cast a IControlledApplicationLifetime su un lifetime diverso
        // dal classic-desktop ritorna null (non InvalidCastException), e il successivo Shutdown() su
        // null lancia NRE — che è il timeout che avverte il developer (meglio di una silenzio a runtime).
        // Oggi è l'unico che questa shell registra (Program.cs).
        services.AddSingleton<Core.Abstractions.IApplicationLifetime>(_ =>
            new AvaloniaApplicationLifetime((IControlledApplicationLifetime)Application.Current!.ApplicationLifetime!));

        services.AddSingleton<IThemeService>(sp =>
            new AvaloniaThemeService(sp.GetRequiredService<IAppSettingService>(), Application.Current!));

        // Stesso ragionamento della lambda sopra per l'owner: valutato alla risoluzione, quando
        // MainWindow esiste già (Home, che usa questa dialog per Elimina/Purge, non esiste prima).
        services.AddSingleton<IConfirmationDialog>(_ => new AvaloniaConfirmationDialog(
            () => (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow));

        services.AddSingleton<IHabitTemplatePickerLauncher, NullHabitTemplatePickerLauncher>();
        services.AddSingleton<IHabitBuilderLauncher, NullHabitBuilderLauncher>();
        services.AddSingleton<ISettingsWindowLauncher, NullSettingsWindowLauncher>();
        services.AddSingleton<IExportDialogLauncher, NullExportDialogLauncher>();
        services.AddSingleton<IBackupDialogLauncher, NullBackupDialogLauncher>();
        services.AddSingleton<ISaveFileDialog, NullSaveFileDialog>();
        services.AddSingleton<IBackupSaveFileDialog, NullBackupSaveFileDialog>();
        services.AddSingleton<IOpenFileDialog, NullOpenFileDialog>();
        services.AddSingleton<IClipboard, NullClipboard>();
        services.AddSingleton<IScreenColorPicker, NullScreenColorPicker>();
        services.AddSingleton<IAutoBackupKeyStore, NullAutoBackupKeyStore>();
        services.AddSingleton<IEditorAvailability, NullEditorAvailability>();

        return services;
    }
}
