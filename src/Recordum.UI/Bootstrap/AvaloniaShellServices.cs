using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
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

namespace Recordum.UI.Bootstrap;

/// <summary>Le registrazioni Avalonia della shell divise in due parti: ciò che non apre finestre
/// (<see cref="AddRecordumAvaloniaCommon"/>, condiviso fra computer e telefono) e ciò che vive in
/// finestre o nel lifetime classico (<see cref="AddRecordumDesktopShell"/>).</summary>
public static class AvaloniaShellServices
{
    /// <summary>Servizi Avalonia indipendenti dalla testa: tick, tema, impostazioni comuni e segnaposto Null.</summary>
    public static IServiceCollection AddRecordumAvaloniaCommon(this IServiceCollection services)
    {
        services.AddSingleton<ITickSourceFactory, DispatcherTickSourceFactory>();
        services.AddSingleton<IThemeService>(sp =>
            new AvaloniaThemeService(sp.GetRequiredService<IAppSettingService>(), Application.Current!));

        // Impostazioni › Aspetto: il tema e le righe della Home (HomeRowsPreference lo registra la composizione dalla testa).
        services.AddTransient(sp => new Presentation.ViewModels.SettingsViewModel(
            sp.GetRequiredService<IThemeService>(), sp.GetRequiredService<IAppFontService>(), sp.GetRequiredService<HomeRowsPreference>()));

        services.AddSingleton<IExportDialogLauncher, NullExportDialogLauncher>();
        services.AddSingleton<ISaveFileDialog, NullSaveFileDialog>();
        services.AddSingleton<IClipboard, NullClipboard>();
        services.AddSingleton<IScreenColorPicker, NullScreenColorPicker>();
        services.AddSingleton<IEditorAvailability, NullEditorAvailability>();

        return services;
    }

    /// <summary>Servizi specifici della testa desktop: lifetime classico, dialoghi modali e finestre.</summary>
    public static IServiceCollection AddRecordumDesktopShell(this IServiceCollection services)
    {
        // Questo metodo gira in App.StartAsync (via AppComposition.BuildServices), cioe' dopo
        // OnFrameworkInitializationCompleted: il lifetime c'e' gia'. La lambda resta comunque una fabbrica
        // valutata alla RISOLUZIONE, non alla registrazione, cosi' non dipende dall'ordine. Il cast a IControlledApplicationLifetime su un lifetime diverso
        // dal classic-desktop ritorna null (non InvalidCastException), e il successivo Shutdown() su
        // null lancia NRE — che è il timeout che avverte il developer (meglio di una silenzio a runtime).
        services.AddSingleton<Core.Abstractions.IApplicationLifetime>(_ =>
            new AvaloniaApplicationLifetime((IControlledApplicationLifetime)Application.Current!.ApplicationLifetime!));

        // Stesso ragionamento della lambda sopra per l'owner: valutato alla risoluzione, quando
        // MainWindow esiste già (Home, che usa questa dialog per Elimina/Purge, non esiste prima).
        services.AddSingleton<IConfirmationDialog>(_ => new AvaloniaConfirmationDialog(TopmostWindow));

        services.AddSingleton<IHabitTemplatePickerLauncher>(sp => new AvaloniaHabitTemplatePickerLauncher(sp, MainWindow));
        services.AddSingleton<IHabitBuilderLauncher>(sp => new AvaloniaHabitBuilderLauncher(sp, MainWindow));
        services.AddSingleton<ISettingsWindowLauncher>(sp => new AvaloniaSettingsWindowLauncher(sp, MainWindow));
        services.AddSingleton<IHabitNavigator>(_ => new AvaloniaHabitNavigator(MainWindow));

        services.AddSingleton<IBackupDialogLauncher>(sp => new AvaloniaBackupDialogLauncher(sp, TopmostWindow));
        services.AddSingleton<IBackupSaveFileDialog>(_ => new AvaloniaBackupSaveFileDialog(TopmostWindow));
        services.AddSingleton<IOpenFileDialog>(_ => new AvaloniaOpenFileDialog(TopmostWindow));
        services.AddSingleton<IFolderPicker>(_ => new AvaloniaFolderPicker(TopmostWindow));
        services.AddSingleton<IFolderOpener>(_ => new AvaloniaFolderOpener(TopmostWindow));

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
