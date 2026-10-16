using Avalonia;
using Avalonia.Controls;
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

        // Questo metodo gira in App.StartAsync (via AppComposition.BuildServices), cioe' dopo
        // OnFrameworkInitializationCompleted: il lifetime c'e' gia'. La lambda resta comunque una fabbrica
        // valutata alla RISOLUZIONE, non alla registrazione, cosi' non dipende dall'ordine. Il cast a IControlledApplicationLifetime su un lifetime diverso
        // dal classic-desktop ritorna null (non InvalidCastException), e il successivo Shutdown() su
        // null lancia NRE — che è il timeout che avverte il developer (meglio di una silenzio a runtime).
        // Oggi è l'unico che questa shell registra (Program.cs).
        services.AddSingleton<Core.Abstractions.IApplicationLifetime>(_ =>
            new AvaloniaApplicationLifetime((IControlledApplicationLifetime)Application.Current!.ApplicationLifetime!));

        services.AddSingleton<IThemeService>(sp =>
            new AvaloniaThemeService(sp.GetRequiredService<IAppSettingService>(), Application.Current!));

        // Stesso ragionamento della lambda sopra per l'owner: valutato alla risoluzione, quando
        // MainWindow esiste già (Home, che usa questa dialog per Elimina/Purge, non esiste prima).
        services.AddSingleton<IConfirmationDialog>(_ => new AvaloniaConfirmationDialog(MainWindow));

        services.AddSingleton<IHabitTemplatePickerLauncher>(sp => new AvaloniaHabitTemplatePickerLauncher(sp, MainWindow));
        services.AddSingleton<IHabitBuilderLauncher>(sp => new AvaloniaHabitBuilderLauncher(sp, MainWindow));
        services.AddSingleton<ISettingsWindowLauncher, NullSettingsWindowLauncher>();
        services.AddSingleton<IExportDialogLauncher, NullExportDialogLauncher>();
        services.AddSingleton<IBackupDialogLauncher>(sp => new AvaloniaBackupDialogLauncher(sp, MainWindow));
        services.AddSingleton<ISaveFileDialog, NullSaveFileDialog>();
        services.AddSingleton<IBackupSaveFileDialog>(_ => new AvaloniaBackupSaveFileDialog(MainWindow));
        services.AddSingleton<IOpenFileDialog>(_ => new AvaloniaOpenFileDialog(MainWindow));
        services.AddSingleton<IClipboard, NullClipboard>();
        services.AddSingleton<IScreenColorPicker, NullScreenColorPicker>();
        services.AddSingleton<IEditorAvailability, NullEditorAvailability>();

        return services;
    }

    /// <summary>La finestra principale dell'app, owner delle finestre modali. Letta alla chiamata, non alla
    /// registrazione: in headless e prima dell'avvio il lifetime non c'e' e le finestre si aprono senza owner.</summary>
    private static Window? MainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
