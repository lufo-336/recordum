using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.Contracts.Permissions;

namespace Recordum.Plugins.Permissions;

/// <summary>
/// Phase 3 broker for first-party in-process consumers. Always grants. Phase 7 introduces
/// the manifest-aware broker that enforces permissions, host whitelists, and runtime prompts.
/// </summary>
public sealed class AlwaysAllowPermissionBroker : IPermissionBroker
{
    public PermissionDecision Check(PermissionRequest request) =>
        new(PermissionOutcome.Allowed);
}
