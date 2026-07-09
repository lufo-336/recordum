using Microsoft.EntityFrameworkCore;
using Recordum.Core.Models;

namespace Recordum.Data;

public sealed class RecordumDbContext(DbContextOptions<RecordumDbContext> options)
    : DbContext(options)
{
    public DbSet<HabitNode> HabitNodes => Set<HabitNode>();
    public DbSet<HabitConfig> HabitConfigs => Set<HabitConfig>();
    public DbSet<HabitConfigHistory> HabitConfigHistories => Set<HabitConfigHistory>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<ScaleTemplate> ScaleTemplates => Set<ScaleTemplate>();
    public DbSet<MetricDefinition> MetricDefinitions => Set<MetricDefinition>();
    public DbSet<SegmentDefinition> SegmentDefinitions => Set<SegmentDefinition>();
    public DbSet<Completion> Completions => Set<Completion>();
    public DbSet<CompletionValue> CompletionValues => Set<CompletionValue>();
    public DbSet<CompletionSegment> CompletionSegments => Set<CompletionSegment>();
    public DbSet<StreakPause> StreakPauses => Set<StreakPause>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteHistory> NoteHistories => Set<NoteHistory>();
    public DbSet<Models.Attachment> Attachments => Set<Models.Attachment>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<HabitNodeTag> HabitNodeTags => Set<HabitNodeTag>();
    public DbSet<HabitTemplate> HabitTemplates => Set<HabitTemplate>();
    public DbSet<ExportTemplate> ExportTemplates => Set<ExportTemplate>();
    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();
    public DbSet<Chart> Charts => Set<Chart>();
    public DbSet<ChartSeries> ChartSeries => Set<ChartSeries>();
    public DbSet<AIInsight> AIInsights => Set<AIInsight>();
    public DbSet<ActiveTimer> ActiveTimers => Set<ActiveTimer>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<BackupLog> BackupLogs => Set<BackupLog>();
    public DbSet<AbstinencePeriod> AbstinencePeriods => Set<AbstinencePeriod>();
    public DbSet<SyncMeta> SyncMeta => Set<SyncMeta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecordumDbContext).Assembly);
    }
}
