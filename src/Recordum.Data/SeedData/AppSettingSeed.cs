using Recordum.Core.Models;

namespace Recordum.Data.SeedData;

public static class AppSettingSeed
{
    private static readonly DateTime SeedDate = new(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeedDateV27 = new(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc);

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
        new AppSetting { Key = "backup.auto.enabled",         Value = "true",     UpdatedAt = SeedDateV27 },
        new AppSetting { Key = "backup.auto.frequency_days",  Value = "7",        UpdatedAt = SeedDateV27 },
        new AppSetting { Key = "backup.auto.retention_count", Value = "8",        UpdatedAt = SeedDateV27 },
        new AppSetting { Key = "backup.auto.last_run_utc",    Value = "",         UpdatedAt = SeedDateV27 },
        new AppSetting { Key = "backup.auto.path",            Value = "",         UpdatedAt = SeedDateV27 },
        new AppSetting { Key = "schema_version",              Value = "2.7",      UpdatedAt = SeedDateV27 },
    ];
}
