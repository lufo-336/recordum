using System.Text.Json;

namespace Recordum.Presentation.ViewModels.Workspace;

/// <summary>Whether an open tab points at a habit or a group node.</summary>
public enum WorkspaceTabKind { Habit, Group }

/// <summary>One persisted (pinned) tab: which node and its kind.</summary>
public sealed record WorkspaceTabState(Guid SubjectId, WorkspaceTabKind Kind);

/// <summary>Light session state for the workspace shell (spec §6). Persisted in the
/// <c>ui.workspace</c> AppSetting. Only pinned tabs are stored; the preview tab, the per-tab
/// open-pane set, pane widths, and focus state are intentionally NOT persisted.</summary>
public sealed record WorkspaceState
{
    /// <summary>Sentinel <see cref="ActiveTabId"/> value meaning "the always-present Panoramica tab".</summary>
    public const string PanoramicaId = "panoramica";

    public bool SidebarCollapsed { get; init; }
    public double SidebarWidth { get; init; }
    /// <summary>A pinned tab's SubjectId as string, or <see cref="PanoramicaId"/>.</summary>
    public string ActiveTabId { get; init; } = PanoramicaId;
    public IReadOnlyList<WorkspaceTabState> Tabs { get; init; } = [];

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Parses persisted JSON; returns null for null/empty/malformed input (caller falls back to defaults).</summary>
    public static WorkspaceState? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<WorkspaceState>(json, Options); }
        catch (JsonException) { return null; }
    }
}
