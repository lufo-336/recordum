using Recordum.WPF.Bootstrap;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Data;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Xunit;

namespace Recordum.WPF.Tests.E2E;

/// <summary>
/// Onda 2.5 T10, the half a payload comparison cannot prove: composing in the builder, saving it as a
/// template and instantiating that template must produce the same habit that Launch would have — over
/// a real encrypted database, through the real services. <see cref="ViewModels.HabitBuilder.BuilderTemplateParityTests"/>
/// checks the two payloads say the same thing; this checks the rows they become.
/// </summary>
[Trait("Category", "E2E")]
public class BuilderTemplateRoundTripE2ETests
{
    [WpfFact]
    public async Task A_composition_saved_as_a_template_reinstantiates_as_the_same_habit()
    {
        var temp = Directory.CreateTempSubdirectory("rec-builder-e2e-");
        try
        {
            var options = new RecordumOptions
            {
                DatabasePath = Path.Combine(temp.FullName, "recordum.db"),
                KeyFilePath = Path.Combine(temp.FullName, "db.key"),
                SaltFilePath = Path.Combine(temp.FullName, "db.salt"),
                AttachmentsDirectory = Path.Combine(temp.FullName, "attachments"),
                BackupsDirectory = Path.Combine(temp.FullName, "backups"),
                AutoBackupKeyPath = Path.Combine(temp.FullName, "autobackup.key"),
                DataDirectory = temp.FullName,
                TempDirectory = Path.Combine(temp.FullName, "tmp"),
            };
            Directory.CreateDirectory(options.AttachmentsDirectory);
            Directory.CreateDirectory(options.BackupsDirectory);

            var services = new ServiceCollection().AddRecordumServices(options).BuildServiceProvider();
            await services.GetRequiredService<ApplicationInitializer>().InitializeAsync();
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;

            var vm = sp.GetRequiredService<HabitBuilderViewModel>();
            await vm.InitializeAsync();
            Compose(vm);

            // 1) Launch it.
            await vm.LaunchCommand.ExecuteAsync(null);
            var launchedId = vm.CreatedHabitNodeId!.Value;

            // 2) Save the same composition as a template, then instantiate it.
            await vm.SaveAsTemplateCommand.ExecuteAsync(null);
            var factory = sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>();
            Guid templateId;
            await using (var db = await factory.CreateDbContextAsync())
                templateId = (await db.HabitTemplates.SingleAsync(t => !t.IsOfficial && t.Name == "Tè della sera")).Id;
            var instantiatedId = await sp.GetRequiredService<IHabitTemplateService>()
                .InstantiateAsync(templateId, parentId: null);

            // 3) The two habits must be indistinguishable where it counts.
            await using var check = await factory.CreateDbContextAsync();
            var launched = await Snapshot(check, launchedId);
            var instantiated = await Snapshot(check, instantiatedId);
            instantiated.Should().BeEquivalentTo(launched,
                "a template is a saved composition: re-instantiating it must rebuild the very habit the " +
                "builder would have launched (E16)");

            // And the staged unit was created once, not once per instantiation.
            (await check.UnitsOfMeasure.CountAsync(u => u.Code == "tazze")).Should().Be(1);

            await services.DisposeAsync();
        }
        finally
        {
            try { Directory.Delete(temp.FullName, recursive: true); } catch { }
        }
    }

    private static void Compose(HabitBuilderViewModel vm)
    {
        vm.Name = "Tè della sera";
        vm.Color = "#123456";
        vm.AddFieldCommand.Execute(FieldNature.Quantity);
        vm.Fields[0].Name = "Tazze";
        vm.BeginAddCustomUnit(vm.Fields[0]);
        vm.UnitForm!.Name = "Tazze";
        vm.ConfirmCustomUnitCommand.Execute(null);
        vm.Repeatable = true;
        vm.AddSeriesLabelCommand.Execute(null);
        vm.SeriesLabels[0].Name = "Pomeriggio";
        vm.Schedule.ScheduleType = ScheduleType.SpecificDays;
        vm.Schedule.Days.Single(d => d.DayKey == "Mon").IsSelected = true;
        vm.Schedule.StreakRecovery = StreakRecovery.None;
        vm.SessionBrick.HasSession = true;
        vm.GoalBrick.HasGoal = true;
        vm.GoalBrick.Target = 3m;
    }

    /// <summary>Everything the composition claims to carry, in a shape two habits can be compared by
    /// (ids and timestamps excluded — they legitimately differ).</summary>
    private static async Task<object> Snapshot(RecordumDbContext db, Guid habitId)
    {
        var node = await db.HabitNodes.AsNoTracking().SingleAsync(n => n.Id == habitId);
        var config = await db.HabitConfigs.AsNoTracking().SingleAsync(c => c.HabitNodeId == habitId);
        var metrics = await db.MetricDefinitions.AsNoTracking()
            .Where(m => m.HabitNodeId == habitId).OrderBy(m => m.SortOrder).ToListAsync();
        var segments = await db.SegmentDefinitions.AsNoTracking()
            .Where(s => s.HabitNodeId == habitId).OrderBy(s => s.SortOrder).ToListAsync();
        var goal = await db.Goals.AsNoTracking().SingleOrDefaultAsync(g => g.HabitNodeId == habitId);
        var units = await db.UnitsOfMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Code);

        return new
        {
            node.Name,
            node.HabitType,
            node.NodeType,
            node.Color,
            node.UiPrefsJson,
            config.ScheduleType,
            config.ScheduleDaysJson,
            config.StreakMode,
            config.StreakRecovery,
            config.Repeatable,
            config.SeriesLabelsJson,
            config.HasIntegratedTimer,
            config.TimerMode,
            config.SessionBaseTimer,
            Metrics = metrics.Select(m => new
            {
                m.Name,
                m.Type,
                m.SortOrder,
                Unit = m.UnitOfMeasureId is { } uid ? units[uid] : null,
                m.LabelsJson,
            }).ToList(),
            Segments = segments.Select(s => new { s.Name, s.IsRest, s.SortOrder }).ToList(),
            Goal = goal is null ? null : new { goal.Name, goal.TargetValue, goal.PeriodType },
        };
    }
}
