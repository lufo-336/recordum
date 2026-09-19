namespace Recordum.Presentation.ViewModels.Settings;

/// <summary>Content of a not-yet-available settings category, shown as a "coming soon" panel.
/// Visible-but-disabled future categories are an approved deviation from the no-placeholder rule
/// (spec 2026-06-11-ui-modernization-plan-design.md, decisione 3).</summary>
public sealed record ComingSoonContent(string Description);
