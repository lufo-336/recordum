using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Data.Services.Policies;

public sealed class GraceDayStreakRecoveryPolicy : IStreakRecoveryPolicy
{
    public StreakRecovery Recovery => StreakRecovery.GraceDay;
    public bool TryAbsorb(RecoveryContext ctx) => ctx.MissesAbsorbedInWindow < 1;
}
