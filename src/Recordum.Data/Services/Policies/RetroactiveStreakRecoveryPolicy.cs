using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Data.Services.Policies;

public sealed class RetroactiveStreakRecoveryPolicy : IStreakRecoveryPolicy
{
    public StreakRecovery Recovery => StreakRecovery.Retroactive;
    public bool TryAbsorb(RecoveryContext ctx) => false;
}
