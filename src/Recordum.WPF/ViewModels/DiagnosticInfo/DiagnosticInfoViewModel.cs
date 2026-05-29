using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Resources;
using Recordum.WPF.Services;

namespace Recordum.WPF.ViewModels.DiagnosticInfo;

/// <summary>A single key-value row in the diagnostic dialog.</summary>
public sealed record DiagnosticRow(string Label, string Value);

public sealed partial class DiagnosticInfoViewModel : ObservableObject
{
    private readonly IAppSettingService _appSettings;
    private readonly IThemeService _themeService;
    private readonly IWebView2Availability _webView2;
    private readonly IBackupStoragePaths _storagePaths;
    private readonly IBackupLogService _backupLog;
    private readonly IPluginManager _pluginManager;
    private readonly IClipboard _clipboard;

    public ObservableCollection<DiagnosticRow> Rows { get; } = [];

    [ObservableProperty] private bool _copyConfirmed;

    public DiagnosticInfoViewModel(
        IAppSettingService appSettings,
        IThemeService themeService,
        IWebView2Availability webView2,
        IBackupStoragePaths storagePaths,
        IBackupLogService backupLog,
        IPluginManager pluginManager,
        IClipboard clipboard)
    {
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(webView2);
        ArgumentNullException.ThrowIfNull(storagePaths);
        ArgumentNullException.ThrowIfNull(backupLog);
        ArgumentNullException.ThrowIfNull(pluginManager);
        ArgumentNullException.ThrowIfNull(clipboard);
        _appSettings = appSettings;
        _themeService = themeService;
        _webView2 = webView2;
        _storagePaths = storagePaths;
        _backupLog = backupLog;
        _pluginManager = pluginManager;
        _clipboard = clipboard;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Rows.Clear();

        // App version
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? Strings.Diagnostic_NotAvailable;
        Rows.Add(new DiagnosticRow("App version", version));

        // Schema version
        var schemaVersion = await _appSettings.GetAsync("schema_version", ct).ConfigureAwait(true)
            ?? Strings.Diagnostic_NotAvailable;
        Rows.Add(new DiagnosticRow("Schema version", schemaVersion));

        // OS version
        var osVersion = $"{Environment.OSVersion.VersionString} / {RuntimeInformation.OSDescription}";
        Rows.Add(new DiagnosticRow("OS version", osVersion));

        // .NET runtime
        Rows.Add(new DiagnosticRow(".NET runtime", RuntimeInformation.FrameworkDescription));

        // Theme
        var theme = $"{_themeService.CurrentMode} (effective: {_themeService.EffectiveTheme})";
        Rows.Add(new DiagnosticRow("Theme", theme));

        // Culture
        Rows.Add(new DiagnosticRow("Culture", CultureInfo.CurrentUICulture.Name));

        // WebView2 runtime installed
        Rows.Add(new DiagnosticRow("WebView2 runtime installed", _webView2.IsAvailable ? "yes" : "no"));

        // WebView2 runtime version
        var webView2Version = GetWebView2Version();
        Rows.Add(new DiagnosticRow("WebView2 runtime version", webView2Version));

        // WebView2 editor assets path
        var editorAssetsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "EditorAssets");
        Rows.Add(new DiagnosticRow("Editor assets path", editorAssetsPath));

        // DB path
        Rows.Add(new DiagnosticRow("DB path", _storagePaths.DatabasePath));

        // Backups directory
        Rows.Add(new DiagnosticRow("Backups directory", _storagePaths.BackupsDirectory));

        // Last backup
        var lastBackupRows = await _backupLog.ListAsync(take: 1, ct).ConfigureAwait(true);
        var lastBackup = lastBackupRows.Count > 0
            ? $"{lastBackupRows[0].CreatedAt:yyyy-MM-dd HH:mm:ss} UTC ({lastBackupRows[0].Kind})"
            : Strings.Diagnostic_NoBackups;
        Rows.Add(new DiagnosticRow("Last backup", lastBackup));

        // Plugins activated
        var manifests = _pluginManager.ListActivatedManifests();
        var pluginsValue = manifests.Count > 0
            ? string.Join(", ", manifests.Select(m => $"{m.Id} {m.Version}"))
            : "(none)";
        Rows.Add(new DiagnosticRow("Plugins activated", pluginsValue));

        // Git branch + commit
        var (branch, sha) = await GetGitInfoAsync().ConfigureAwait(true);
        Rows.Add(new DiagnosticRow("Git branch", branch));
        Rows.Add(new DiagnosticRow("Git commit", sha));
    }

#pragma warning disable CA1031 // non-fatal probe — runtime or git missing is expected
    private static string GetWebView2Version()
    {
        try
        {
            return Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch
        {
            return Strings.Diagnostic_NotAvailable;
        }
    }
#pragma warning restore CA1031

#pragma warning disable CA1031 // git is best-effort — user may not have git in PATH
    private static async Task<(string Branch, string Sha)> GetGitInfoAsync()
    {
        try
        {
            var branch = await RunGitAsync("rev-parse --abbrev-ref HEAD").ConfigureAwait(false);
            var sha = await RunGitAsync("rev-parse --short HEAD").ConfigureAwait(false);
            if (branch is null || sha is null)
                return (Strings.Diagnostic_NotGitCheckout, Strings.Diagnostic_NotGitCheckout);
            return (branch, sha);
        }
        catch
        {
            return (Strings.Diagnostic_NotGitCheckout, Strings.Diagnostic_NotGitCheckout);
        }
    }
#pragma warning restore CA1031

    private static async Task<string?> RunGitAsync(string arguments)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is null) return null;

        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        return process.ExitCode == 0 ? output.Trim() : null;
    }

    [RelayCommand]
    private async Task CopyToClipboardAsync()
    {
        var text = string.Join("\n", Rows.Select(r => $"{r.Label}: {r.Value}"));
        _clipboard.SetText(text);
        CopyConfirmed = true;
        await Task.Delay(2000).ConfigureAwait(true);
        CopyConfirmed = false;
    }
}
