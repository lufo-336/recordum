using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.Contracts.Manifest;
using Recordum.Plugins.Contracts.Permissions;

namespace Recordum.Plugins.Management;

public sealed class PluginManager(
    IPluginHost host,
    IPermissionBroker broker,
    IExtensionPointRegistry registry) : IPluginManager
{
    public async Task EnableAsync(PluginManifest manifest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        // Permission gate: ask broker for at minimum the implicit storage permission.
        var decision = broker.Check(new PermissionRequest(manifest.Id, WellKnownPermissions.StorageRead));
        if (decision.Outcome != PermissionOutcome.Allowed)
        {
            throw new InvalidOperationException(
                $"Plugin '{manifest.Id}' denied by broker: {decision.Reason ?? "(no reason)"}");
        }

        await host.LoadAsync(manifest, ct).ConfigureAwait(false);
    }

    public async Task DisableAsync(string pluginId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        registry.UnregisterAll(pluginId);
        await host.UnloadAsync(pluginId, ct).ConfigureAwait(false);
    }

    public IReadOnlyCollection<string> ListEnabled()
    {
        // The host is the source of truth.
        // In Phase 3 we don't have an "enumerate loaded" API on IPluginHost,
        // but we can track it via the manager's own state if needed.
        // For the stub: return empty when no API exists; ListEnabled is more of a
        // Phase 7 concern once the manifest registry surfaces it.
        return [];
    }
}
