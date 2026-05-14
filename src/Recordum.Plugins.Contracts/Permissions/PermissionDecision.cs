namespace Recordum.Plugins.Contracts.Permissions;

public enum PermissionOutcome { Allowed, Denied, RuntimePrompt }

public sealed record PermissionDecision(PermissionOutcome Outcome, string? Reason = null);
