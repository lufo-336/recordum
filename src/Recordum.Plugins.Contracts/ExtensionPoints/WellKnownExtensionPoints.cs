namespace Recordum.Plugins.Contracts.ExtensionPoints;

public static class WellKnownExtensionPoints
{
    public static readonly ExtensionPointId DashboardWidget = new("dashboard.widget");
    public static readonly ExtensionPointId ChartRenderer = new("chart.renderer");
    public static readonly ExtensionPointId HabitDetailTab = new("habit.detail.tab");
    public static readonly ExtensionPointId CompletionInput = new("completion.input");
    public static readonly ExtensionPointId SettingsPanel = new("settings.panel");
    public static readonly ExtensionPointId CommandPalette = new("command-palette");
    public static readonly ExtensionPointId ExportFormat = new("export.format");
    public static readonly ExtensionPointId SyncProvider = new("sync.provider");
    public static readonly ExtensionPointId DataImporter = new("data.importer");
}
