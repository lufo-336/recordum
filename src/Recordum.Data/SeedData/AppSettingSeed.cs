using Recordum.Core.Models;

namespace Recordum.Data.SeedData;

/// <summary>
/// Official seed data for AppSetting. Keys are PK — values must be stable string literals.
/// Matches the registry defined in CLAUDE.md §6.7 with schema_version bumped to 2.3
/// per the provenance delta in the plugin system spec §2.4.
/// Timestamps are literal constants — TimestampInterceptor does not run during HasData INSERT generation.
/// </summary>
public static class AppSettingSeed
{
    private static readonly DateTime SeedDate = new(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<AppSetting> All { get; } =
    [
        new AppSetting { Key = "language",                    Value = "en",       UpdatedAt = SeedDate },
        new AppSetting { Key = "daily_reset_time",            Value = "00:00",    UpdatedAt = SeedDate },
        new AppSetting { Key = "theme",                       Value = "system",   UpdatedAt = SeedDate },
        new AppSetting { Key = "onboarding_complete",         Value = "false",    UpdatedAt = SeedDate },
        new AppSetting { Key = "ai_mode",                     Value = "disabled", UpdatedAt = SeedDate },
        new AppSetting { Key = "ai_default_sharing",          Value = "none",     UpdatedAt = SeedDate },
        new AppSetting { Key = "notifications_enabled",       Value = "true",     UpdatedAt = SeedDate },
        new AppSetting { Key = "app_lock_enabled",            Value = "false",    UpdatedAt = SeedDate },
        new AppSetting { Key = "app_lock_type",               Value = "pin",      UpdatedAt = SeedDate },
        new AppSetting { Key = "inactivity_timeout_minutes",  Value = "5",        UpdatedAt = SeedDate },
        new AppSetting { Key = "monthly_summary_day",         Value = "1",        UpdatedAt = SeedDate },
        new AppSetting { Key = "monthly_summary_time",        Value = "09:00",    UpdatedAt = SeedDate },
        new AppSetting { Key = "schema_version",              Value = "2.3",      UpdatedAt = SeedDate },
    ];
}
