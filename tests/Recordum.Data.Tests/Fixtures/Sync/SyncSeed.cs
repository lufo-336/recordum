using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Models;
using Recordum.Data.Sync;

namespace Recordum.Data.Tests.Fixtures.Sync;

/// <summary>
/// Popola un <see cref="DbFixture"/> con almeno una riga per ciascuna delle 26 entità che viaggiano
/// in <see cref="SyncCatalog.Entities"/>, con valori non di default in ogni colonna, foto e miniatura di pochi KB,
/// più una lapide in <c>sync_tombstone</c>.
/// </summary>
public static class SyncSeed
{
    public static readonly Guid UnitOfMeasureId = new("ABCDEF01-0000-0000-0000-000000000001");
    public static readonly Guid TagId = new("ABCDEF01-0000-0000-0000-000000000002");
    public static readonly Guid ScaleTemplateId = new("ABCDEF01-0000-0000-0000-000000000003");
    public static readonly Guid ScaleAnchorId = new("ABCDEF01-0000-0000-0000-000000000004");
    public static readonly Guid HabitNodeId = new("ABCDEF01-0000-0000-0000-000000000005");
    public static readonly Guid HabitConfigId = new("ABCDEF01-0000-0000-0000-000000000006");
    public static readonly Guid HabitConfigHistoryId = new("ABCDEF01-0000-0000-0000-000000000007");
    public static readonly Guid MetricDefinitionId = new("ABCDEF01-0000-0000-0000-000000000008");
    public static readonly Guid SegmentDefinitionId = new("ABCDEF01-0000-0000-0000-000000000009");
    public static readonly Guid GoalId = new("ABCDEF01-0000-0000-0000-000000000010");
    public static readonly Guid NotificationId = new("ABCDEF01-0000-0000-0000-000000000011");
    public static readonly Guid StreakPauseId = new("ABCDEF01-0000-0000-0000-000000000012");
    public static readonly Guid AbstinencePeriodId = new("ABCDEF01-0000-0000-0000-000000000013");
    public static readonly Guid CompletionId = new("ABCDEF01-0000-0000-0000-000000000014");
    public static readonly Guid CompletionSegmentId = new("ABCDEF01-0000-0000-0000-000000000015");
    public static readonly Guid CompletionValueId = new("ABCDEF01-0000-0000-0000-000000000016");
    public static readonly Guid NoteId = new("ABCDEF01-0000-0000-0000-000000000017");
    public static readonly Guid NoteHistoryId = new("ABCDEF01-0000-0000-0000-000000000018");
    public static readonly Guid AttachmentId = new("ABCDEF01-0000-0000-0000-000000000019");
    public static readonly Guid HabitTemplateId = new("ABCDEF01-0000-0000-0000-000000000020");
    public static readonly Guid ExportTemplateId = new("ABCDEF01-0000-0000-0000-000000000021");
    public static readonly Guid ChartId = new("ABCDEF01-0000-0000-0000-000000000022");
    public static readonly Guid ChartSeriesId = new("ABCDEF01-0000-0000-0000-000000000023");

    /// <summary>Forma salvata da EF/SQLite per un GUID: "D" maiuscolo (misurata).</summary>
    public static string Stored(Guid g) => g.ToString("D").ToUpperInvariant();

    public static byte[] CreateSamplePhotoBytes(int sizeBytes = 4096)
    {
        var bytes = new byte[sizeBytes];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((i * 31 + 17) & 0xFF);
        }

        return bytes;
    }

    public static byte[] CreateSampleThumbnailBytes(int sizeBytes = 1536)
    {
        var bytes = new byte[sizeBytes];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((i * 73 + 41) & 0xFF);
        }

        return bytes;
    }

    public static async Task SeedAsync(DbFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        fixture.PluginContext.PluginId = "recordum.plugin.syncseed";
        fixture.DeviceIdentity.DeviceName = "Fedora Studio";

        await using var db = await fixture.Factory.CreateDbContextAsync();

        var t0 = new DateTime(2026, 2, 10, 14, 30, 0, DateTimeKind.Utc);
        var t1 = new DateTime(2026, 2, 10, 15, 0, 0, DateTimeKind.Utc);
        var t0Ms = new DateTimeOffset(t0).ToUnixTimeMilliseconds();
        var t1Ms = new DateTimeOffset(t1).ToUnixTimeMilliseconds();

        // 1. app_setting: le chiavi che viaggiano con valori e timestamp non-default (sync.accepted_repairs non ha una
        // riga di partenza: la crea il primo "Va bene così", qui la fixture)
        foreach (var key in SyncCatalog.TravellingSettingKeys)
        {
            var setting = await db.AppSettings.SingleOrDefaultAsync(s => s.Key == key);
            if (setting is null)
            {
                setting = new AppSetting { Key = key };
                db.AppSettings.Add(setting);
            }
            setting.Value = key switch
            {
                "daily_reset_time" => "04:00",
                "monthly_summary_day" => "15",
                "monthly_summary_time" => "20:30",
                _ => "non-default",
            };
            setting.UpdatedAt = t1;
            setting.UpdatedAtMs = t1Ms;
        }

        // 2. unit_of_measure
        db.UnitsOfMeasure.Add(new UnitOfMeasure
        {
            Id = UnitOfMeasureId,
            Code = "custom-bars",
            Name = "Custom Bars",
            Symbol = "bar",
            IsOfficial = true,
            CreatedAt = t0,
            UpdatedAtMs = t1Ms,
        });

        // 3. tag
        db.Tags.Add(new Tag
        {
            Id = TagId,
            Name = "deep-work",
            Color = "#336699",
            CreatedAt = t0,
            UpdatedAtMs = t1Ms,
        });

        // 4. scale_template
        db.ScaleTemplates.Add(new ScaleTemplate
        {
            Id = ScaleTemplateId,
            Code = "custom-energy",
            Name = "Custom Energy Scale",
            MinValue = 1,
            MaxValue = 10,
            Precision = ScalePrecision.Decimal,
            IsOfficial = true,
            CreatedAt = t0,
            UpdatedAtMs = t1Ms,
        });

        // 5. scale_anchor
        db.ScaleAnchors.Add(new ScaleAnchor
        {
            Id = ScaleAnchorId,
            ScaleTemplateId = ScaleTemplateId,
            Value = 7,
            Label = "High energy",
            TagId = TagId,
            CreatedAt = t0,
            UpdatedAtMs = t1Ms,
        });

        // 6. habit_node (ParentId viene impostato a HabitNodeId via SQL subito dopo per avere 1 riga con tutte le colonne non-null)
        db.HabitNodes.Add(new HabitNode
        {
            Id = HabitNodeId,
            ParentId = null,
            NodeType = NodeType.Habit,
            HabitType = HabitType.Counter,
            Name = "Rowing Session",
            Description = "Indoor rowing ergometer intervals",
            Color = "#1f7a8c",
            Icon = "activity",
            SortOrder = 4,
            Priority = 2,
            IsArchived = true,
            IsLocked = true,
            PinScope = PinScope.Global,
            ResetTime = new TimeOnly(4, 30),
            AiDataSharing = AiDataSharing.LocalOnly,
            UiPrefsJson = "{\"compact\":true}",
            CreatedByPluginId = "recordum.plugin.syncseed",
            CreatedAt = t0,
            UpdatedAt = t1,
            UpdatedAtMs = t1Ms,
            DeletedAt = t1,
        });

        // 7. habit_config
        db.HabitConfigs.Add(new HabitConfig
        {
            Id = HabitConfigId,
            HabitNodeId = HabitNodeId,
            ScheduleType = ScheduleType.SpecificDays,
            ScheduleDaysJson = "[1,3,5]",
            ScheduleMonthlyDaysJson = "[10,20]",
            ScheduleEveryXDays = 2,
            ScheduleWeeklyTarget = 3,
            StreakMode = StreakMode.Weekly,
            StreakRecovery = StreakRecovery.GraceDay,
            StreakToleranceMinutes = 45,
            StreakBrokenAt = t0,
            HasIntegratedTimer = true,
            TimerMode = TimerMode.Session,
            TimerTargetSeconds = 1800,
            TimerAutoPrompt = true,
            RestTimerSeconds = 60,
            SessionBaseTimer = true,
            SessionCountdownSeconds = 300,
            SessionRepeatCount = 4,
            Repeatable = true,
            SeriesLabelsJson = "[\"Warmup\",\"Sprint\"]",
            QuickComplete = true,
            NoteMarkerColor = "#e07a5f",
            AiDataSharing = AiDataSharing.LocalOnly,
            CreatedAt = t0,
            UpdatedAt = t1,
            UpdatedAtMs = t1Ms,
        });

        // 8. habit_config_history
        db.HabitConfigHistories.Add(new HabitConfigHistory
        {
            Id = HabitConfigHistoryId,
            HabitNodeId = HabitNodeId,
            ScheduleType = ScheduleType.SpecificDays,
            ScheduleDaysJson = "[1,3,5]",
            ScheduleMonthlyDaysJson = "[10,20]",
            ScheduleEveryXDays = 2,
            ScheduleWeeklyTarget = 3,
            StreakMode = StreakMode.Weekly,
            StreakRecovery = StreakRecovery.GraceDay,
            StreakToleranceMinutes = 45,
            HasIntegratedTimer = true,
            TimerMode = TimerMode.Session,
            TimerTargetSeconds = 1800,
            TimerAutoPrompt = true,
            RestTimerSeconds = 60,
            SessionBaseTimer = true,
            SessionCountdownSeconds = 300,
            SessionRepeatCount = 4,
            Repeatable = true,
            SeriesLabelsJson = "[\"Warmup\",\"Sprint\"]",
            ValidFrom = t0,
            ValidUntil = t1,
            UpdatedAtMs = t1Ms,
        });

        // 9. metric_definition
        db.MetricDefinitions.Add(new MetricDefinition
        {
            Id = MetricDefinitionId,
            HabitNodeId = HabitNodeId,
            Name = "Distance",
            Type = MetricType.Decimal,
            UnitOfMeasureId = UnitOfMeasureId,
            ScaleTemplateId = ScaleTemplateId,
            SortOrder = 2,
            UpdatedAtMs = t1Ms,
        });

        // 10. segment_definition
        db.SegmentDefinitions.Add(new SegmentDefinition
        {
            Id = SegmentDefinitionId,
            HabitNodeId = HabitNodeId,
            Name = "Sprint Interval",
            DurationSeconds = 120,
            IsRest = true,
            Color = "#f4a261",
            SortOrder = 3,
            UpdatedAtMs = t1Ms,
        });

        // 11. goal
        db.Goals.Add(new Goal
        {
            Id = GoalId,
            HabitNodeId = HabitNodeId,
            MetricDefinitionId = MetricDefinitionId,
            Name = "February Rowing Target",
            TargetValue = 125.75m,
            PeriodType = GoalPeriodType.Fixed,
            PeriodStart = new DateOnly(2026, 2, 1),
            PeriodEnd = new DateOnly(2026, 2, 28),
            RollingDays = 28,
            IsClosed = true,
            ClosedAt = t1,
            CreatedAt = t0,
            UpdatedAtMs = t1Ms,
        });

        // 12. notification
        db.Notifications.Add(new Notification
        {
            Id = NotificationId,
            HabitNodeId = HabitNodeId,
            NotifyType = NotifyType.StreakMilestone,
            NotifyTime = new TimeOnly(18, 15),
            IsEnabled = true,
            CreatedAt = t0,
            UpdatedAtMs = t1Ms,
        });

        // 13. streak_pause
        db.StreakPauses.Add(new StreakPause
        {
            Id = StreakPauseId,
            HabitNodeId = HabitNodeId,
            PauseFrom = new DateOnly(2026, 2, 3),
            PauseTo = new DateOnly(2026, 2, 6),
            Reason = "Mountain trip",
            CreatedAt = t0,
            UpdatedAtMs = t1Ms,
        });

        // 14. abstinence_period
        db.AbstinencePeriods.Add(new AbstinencePeriod
        {
            Id = AbstinencePeriodId,
            HabitNodeId = HabitNodeId,
            StartedAtMs = t0Ms,
            EndedAtMs = t1Ms,
            CreatedAt = t0,
            UpdatedAt = t1,
            UpdatedAtMs = t1Ms,
        });

        // 15. habit_node_tag
        db.HabitNodeTags.Add(new HabitNodeTag
        {
            HabitNodeId = HabitNodeId,
            TagId = TagId,
            CreatedAt = t0,
            UpdatedAtMs = t1Ms,
        });

        // 16. completion
        db.Completions.Add(new Completion
        {
            Id = CompletionId,
            HabitNodeId = HabitNodeId,
            CompletedAt = t1,
            CompletedAtMs = t1Ms,
            StartedAt = t0,
            StartedAtMs = t0Ms,
            EndedAt = t1,
            EndedAtMs = t1Ms,
            DurationSeconds = 1800,
            IsRetroactive = true,
            Notes = "Felt strong on the third interval.",
            LastEditedDeviceName = "Fedora Studio",
            UpdatedAtMs = t1Ms,
        });

        // 17. completion_segment
        db.CompletionSegments.Add(new CompletionSegment
        {
            Id = CompletionSegmentId,
            CompletionId = CompletionId,
            SegmentDefinitionId = SegmentDefinitionId,
            SegmentIndex = 1,
            StartedAt = t0,
            StartedAtMs = t0Ms,
            EndedAt = t1,
            EndedAtMs = t1Ms,
            DurationSeconds = 120,
            IsManualEdit = true,
            Note = "Maintained 1:52/500m pace",
            UpdatedAtMs = t1Ms,
        });

        // 18. completion_value
        db.CompletionValues.Add(new CompletionValue
        {
            Id = CompletionValueId,
            CompletionId = CompletionId,
            MetricDefinitionId = MetricDefinitionId,
            CompletionSegmentId = CompletionSegmentId,
            SeriesIndex = 1,
            ValueInteger = 500L,
            ValueDecimal = 12.345678m,
            ValueSeconds = 112,
            ValueText = " negative split ",
            UpdatedAtMs = t1Ms,
        });

        // 19. note
        db.Notes.Add(new Note
        {
            Id = NoteId,
            HabitNodeId = HabitNodeId,
            CompletionId = CompletionId,
            Category = NoteCategory.Diario,
            TargetDate = new DateOnly(2026, 2, 10),
            BodyMd = "# Rowing Log\n steady cadence at 26 spm.",
            IsSensitive = true,
            CreatedAt = t0,
            LastEditedDeviceName = "Fedora Studio",
            UpdatedAtMs = t1Ms,
        });

        // 20. note_history
        db.NoteHistories.Add(new NoteHistory
        {
            Id = NoteHistoryId,
            NoteId = NoteId,
            BodyMd = "# Rowing Log (draft)\ninitial notes.",
            CreatedAt = t0,
            UpdatedAtMs = t1Ms,
        });

        // 21. note_tag
        db.NoteTags.Add(new NoteTag
        {
            NoteId = NoteId,
            TagId = TagId,
            Source = NoteTagSource.Property,
            UpdatedAtMs = t1Ms,
        });

        // 22. attachment (con foto e miniatura di pochi KB)
        var photoBytes = CreateSamplePhotoBytes();
        var thumbBytes = CreateSampleThumbnailBytes();
        db.Attachments.Add(new Recordum.Data.Models.Attachment
        {
            Id = AttachmentId,
            NoteId = NoteId,
            Filename = "erg-screen.jpg",
            MimeType = "image/jpeg",
            FileSizeBytes = photoBytes.Length,
            Content = photoBytes,
            Thumbnail = thumbBytes,
            IsMissing = true,
            OriginDeviceName = "Fedora Studio",
            CreatedAt = t0,
            UpdatedAt = t1,
            UpdatedAtMs = t1Ms,
        });

        // 23. habit_template
        db.HabitTemplates.Add(new HabitTemplate
        {
            Id = HabitTemplateId,
            Name = "Custom Erg Template",
            Category = "Endurance",
            Description = "Interval rowing template",
            PayloadJson = "{\"node\":{\"name\":\"Rowing\"}}",
            IsOfficial = true,
            CreatedByPluginId = "recordum.plugin.syncseed",
            CreatedAt = t0,
            UpdatedAt = t1,
            UpdatedAtMs = t1Ms,
        });

        // 24. export_template
        db.ExportTemplates.Add(new ExportTemplate
        {
            Id = ExportTemplateId,
            Name = "Custom Training Export",
            Description = "Detailed interval export",
            LayoutJson = "{\"blocks\":[{\"type\":\"title\"}]}",
            IsOfficial = true,
            CreatedByPluginId = "recordum.plugin.syncseed",
            CreatedAt = t0,
            UpdatedAt = t1,
            UpdatedAtMs = t1Ms,
        });

        // 25. chart
        db.Charts.Add(new Chart
        {
            Id = ChartId,
            Name = "Rowing Pace Trend",
            Description = "500m split progression",
            ChartType = "bar",
            ConfigJson = "{\"show_grid\":true}",
            CreatedByPluginId = "recordum.plugin.syncseed",
            CreatedAt = t0,
            UpdatedAt = t1,
            UpdatedAtMs = t1Ms,
        });

        // 26. chart_series
        db.ChartSeries.Add(new ChartSeries
        {
            Id = ChartSeriesId,
            ChartId = ChartId,
            HabitNodeId = HabitNodeId,
            MetricDefinitionId = MetricDefinitionId,
            Label = "Split 500m",
            Color = "#2a9d8f",
            SortOrder = 2,
            UpdatedAtMs = t1Ms,
        });

        // Lapide per tombstones.jsonl
        db.SyncTombstones.Add(new SyncTombstone
        {
            Entity = "tag",
            EntityId = "9ABCDEF9-0000-0000-0000-000000000099",
            DeletedAtMs = t1Ms,
            DeviceId = "device-seed-a",
            AliasOf = Stored(TagId),
        });

        await db.SaveChangesAsync();

        // Impostiamo parent_id sulla riga di habit_nodes così anche parent_id è non-null senza aggiungere una seconda riga
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"habit_nodes\" SET \"parent_id\" = {Stored(HabitNodeId)} WHERE \"id\" = {Stored(HabitNodeId)}");
    }
}
