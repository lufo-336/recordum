using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Data.Services.Policies;

public sealed class ManualStreakRecoveryPolicy : IStreakRecoveryPolicy
{
    public StreakRecovery Recovery => StreakRecovery.Manual;
    public bool TryAbsorb(RecoveryContext ctx) => false;
}
