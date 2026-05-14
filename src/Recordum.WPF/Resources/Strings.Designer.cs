#nullable enable
using System.Globalization;
using System.Resources;

namespace Recordum.WPF.Resources;

public static class Strings
{
    private static readonly ResourceManager Manager = new("Recordum.WPF.Resources.Strings", typeof(Strings).Assembly);

    public static CultureInfo? Culture { get; set; }

    public static string AppTitle => Get(nameof(AppTitle));
    public static string MainWindowPlaceholder => Get(nameof(MainWindowPlaceholder));
    public static string StartupErrorTitle => Get(nameof(StartupErrorTitle));
    public static string StartupErrorMessage => Get(nameof(StartupErrorMessage));

    private static string Get(string name) =>
        Manager.GetString(name, Culture) ?? throw new MissingManifestResourceException($"Resource '{name}' not found.");
}
