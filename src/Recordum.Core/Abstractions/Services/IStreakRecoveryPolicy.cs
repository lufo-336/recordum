using Recordum.Core.Enums;

namespace Recordum.Core.Abstractions.Services;

public interface IStreakRecoveryPolicy
{
    StreakRecovery Recovery { get; }
    bool TryAbsorb(RecoveryContext ctx);
}

public sealed record RecoveryContext(
    DateOnly Date,
    int MissesAbsorbedInWindow,
    DateTime? StreakBrokenAt,
    DateOnly RunStart);
