namespace Recordum.Presentation.Bootstrap;

/// <summary>Timing knobs for <see cref="BackupSchedulerHost"/>. Defaults favour responsiveness:
/// a short initial delay (clears startup DB activity) then a reactive recurring poll.</summary>
public sealed class BackupSchedulerOptions
{
    /// <summary>Delay before the first due-check after launch. Short enough that a backup due at
    /// startup runs within minutes, long enough to clear app-startup DB activity (seeded plugin
    /// activators, first-run home tree).</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Recurring poll interval. <c>RunDueAsync</c> is cheap (it only writes a backup when
    /// one is actually due per <c>backup.auto.frequency_days</c>), so polling often is harmless.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(30);
}
