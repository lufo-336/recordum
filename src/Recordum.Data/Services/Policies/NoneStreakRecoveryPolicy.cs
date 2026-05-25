using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Data.Services.Policies;

public sealed class NoneStreakRecoveryPolicy : IStreakRecoveryPolicy
{
    public StreakRecovery Recovery => StreakRecovery.None;
    public bool TryAbsorb(RecoveryContext ctx) => false;
}
