using Recordum.Plugins.Contracts.Permissions;

namespace Recordum.Plugins.Contracts.Hosting;

public interface IPermissionBroker
{
    PermissionDecision Check(PermissionRequest request);
}
