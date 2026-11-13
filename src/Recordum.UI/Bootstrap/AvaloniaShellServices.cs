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

        // Le righe della Home a scelta (Impostazioni › Aspetto): sul computer la predefinita è Larga. WPF non la registra.
        services.AddSingleton(sp => new HomeRowsPreference(
            sp.GetRequiredService<IAppSettingService>(), Presentation.ViewModels.Home.HomeRows.Comfortable));

        // Stesso ragionamento della lambda sopra per l'owner: valutato alla risoluzione, quando
        // MainWindow esiste già (Home, che usa questa dialog per Elimina/Purge, non esiste prima).
        services.AddSingleton<IConfirmationDialog>(_ => new AvaloniaConfirmationDialog(TopmostWindow));

        services.AddSingleton<IHabitTemplatePickerLauncher>(sp => new AvaloniaHabitTemplatePickerLauncher(sp, MainWindow));
        services.AddSingleton<IHabitBuilderLauncher>(sp => new AvaloniaHabitBuilderLauncher(sp, MainWindow));
        services.AddSingleton<ISettingsWindowLauncher>(sp => new AvaloniaSettingsWindowLauncher(sp, MainWindow));
        // Impostazioni › Aspetto: il tema e le righe della Home (WPF registra il suo, senza preferenza).
        services.AddTransient(sp => new Presentation.ViewModels.SettingsViewModel(
            sp.GetRequiredService<IThemeService>(), sp.GetRequiredService<IAppFontService>(), sp.GetRequiredService<HomeRowsPreference>()));
        // La riga della chiave del dispositivo la decide la composizione: su Windows lo sblocco e' DPAPI, non c'e' portachiavi.
        services.AddTransient(sp => new DataAndBackupPageViewModel(
            sp.GetRequiredService<BackupSettingsViewModel>(),
            sp.GetRequiredService<BackupHistoryViewModel>(),
            sp.GetRequiredService<IBackupDialogLauncher>(),
            sp.GetRequiredService<ISecretStore>(),
            sp.GetRequiredService<IConfirmationDialog>(),
            sp.GetRequiredService<PhotoImportSettingsViewModel>(),
            showDeviceKey: !OperatingSystem.IsWindows()));
        services.AddSingleton<IExportDialogLauncher, NullExportDialogLauncher>();
        services.AddSingleton<IBackupDialogLauncher>(sp => new AvaloniaBackupDialogLauncher(sp, TopmostWindow));
        services.AddSingleton<ISaveFileDialog, NullSaveFileDialog>();
        services.AddSingleton<IBackupSaveFileDialog>(_ => new AvaloniaBackupSaveFileDialog(TopmostWindow));
        services.AddSingleton<IOpenFileDialog>(_ => new AvaloniaOpenFileDialog(TopmostWindow));
        services.AddSingleton<IFolderPicker>(_ => new AvaloniaFolderPicker(TopmostWindow));
        services.AddSingleton<IFolderOpener>(_ => new AvaloniaFolderOpener(TopmostWindow));
        services.AddSingleton<IClipboard, NullClipboard>();
        services.AddSingleton<IScreenColorPicker, NullScreenColorPicker>();
        services.AddSingleton<IEditorAvailability, NullEditorAvailability>();

        return services;
    }

    /// <summary>La finestra principale dell'app, owner delle finestre modali. Letta alla chiamata, non alla
    /// registrazione: in headless e prima dell'avvio il lifetime non c'e' e le finestre si aprono senza owner.</summary>
    private static Window? MainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    /// <summary>Il proprietario di una finestra modale e' la finestra in cui l'utente si trova: se la principale ha gia'
    /// una figlia modale (Impostazioni), una nuova dialog sulla principale si aprirebbe dietro o inutilizzabile.</summary>
    internal static Window? TopmostWindow() => Topmost(MainWindow());

    internal static Window? Topmost(Window? main)
    {
        while (main is { OwnedWindows.Count: > 0 }) main = main.OwnedWindows[^1];
        return main;
    }
}
