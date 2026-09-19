namespace Recordum.Presentation.ViewModels.Settings;

/// <summary>One entry of the settings window's left-hand category list.</summary>
public sealed record SettingsCategory(SettingsCategoryId Id, string Name, string IconKey, bool IsEnabled, object Content);
