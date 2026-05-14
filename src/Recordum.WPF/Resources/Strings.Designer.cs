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

    private static string Get(string name) =>
        Manager.GetString(name, Culture) ?? throw new MissingManifestResourceException($"Resource '{name}' not found.");
}
