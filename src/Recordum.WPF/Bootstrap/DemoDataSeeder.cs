using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data;

namespace Recordum.WPF.Bootstrap;

/// <summary>Dev tool: populates the database with a believable habit library (groups, all five
/// habit types, ~100 days of completions, day notes, tags, a goal) for visual verification.
/// Triggered by the <c>--seed-demo</c> command-line flag; idempotent via the
/// <c>dev.demo_seeded</c> AppSetting marker. Deterministic (fixed Random seed).</summary>
public sealed class DemoDataSeeder(
    IHabitNodeService nodes,
    IHabitConfigService configs,
    IMetricDefinitionService metrics,
    IGoalService goals,
    ICompletionService completions,
    INoteService notes,
    IDiaryService diary,
    ITagService tags,
    IAppSettingService settings,
    IHabitTreeQueryService tree,
    IClock clock,
    IDbContextFactory<RecordumDbContext> dbFactory)
{
    private const string MarkerKey = "dev.demo_seeded";
    private const int Days = 100;

    private readonly Random _rng = new(42);

    private static readonly string[] DayNotePhrases =
    [
        "Giornata pesante, ma fatto il minimo.",
        "Ottima energia oggi.",
        "Poco sonno ieri, si sente.",
        "Ripresa dopo la pausa.",
        "Tutto liscio.",
        "Mezza giornata fuori casa.",
        "Voglia zero, fatto comunque.",
        "Weekend rilassante.",
        "Da rivedere gli orari serali.",
        "Buon ritmo questa settimana.",
        "Acciaccato, riposo quasi totale.",
        "Sessione migliore del solito.",
    ];

    private static readonly string[] JournalEntries =
    [
        "Oggi più sereno del solito. La camminata mattutina aiuta.",
        "Litigato col capo, poi passeggiata lunga per sbollire.",
        "Letto due capitoli di fila, non succedeva da mesi.",
        "Stanco ma soddisfatto: settimana piena chiusa bene.",
        "Pensieri confusi, scrivere aiuta a ordinare.",
        "Domenica lenta, zero sensi di colpa.",
        "Allenamento saltato, recupero domani.",
        "Piccola vittoria: niente caffè dopo pranzo.",
        "Serata con amici, umore alto.",
        "Giornata no. Domani si riparte.",
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await settings.GetAsync(MarkerKey, ct).ConfigureAwait(false) is not null) return;

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow.UtcDateTime, clock.LocalTimeZone));
        var start = today.AddDays(-Days);

        // ---- tags ----
        var tagMattina = await tags.CreateAsync(new Tag { Name = "mattina", Color = "#f0b429" }, ct).ConfigureAwait(false);
        var tagBenessere = await tags.CreateAsync(new Tag { Name = "benessere", Color = "#4dbd91" }, ct).ConfigureAwait(false);

        // ---- tree ----
        var salute = await GroupAsync("Salute", "#e05252", "Icon.HeartPulse", null, ct).ConfigureAwait(false);
        var mente = await GroupAsync("Mente", "#7c5cd6", "Icon.Brain", null, ct).ConfigureAwait(false);
        var sonno = await GroupAsync("Sonno", "#6f8bd6", "Icon.Moon", salute, ct).ConfigureAwait(false);

        var allenamento = await HabitAsync("Allenamento", HabitType.Counter, "#e07a3f", "Icon.Dumbbell", salute, ct).ConfigureAwait(false);
        var acqua = await HabitAsync("Acqua", HabitType.Counter, "#3f9fe0", "Icon.GlassWater", salute, ct).ConfigureAwait(false);
        var mood = await HabitAsync("Mood", HabitType.State, "#c9a227", "Icon.Smile", salute, ct).ConfigureAwait(false);
        var aLetto = await HabitAsync("A letto entro mezzanotte", HabitType.Action, "#6f8bd6", "Icon.Clock", sonno, ct).ConfigureAwait(false);
        var lettura = await HabitAsync("Lettura", HabitType.Counter, "#8a64e0", "Icon.BookOpen", mente, ct).ConfigureAwait(false);
        var diario = await HabitAsync("Diario", HabitType.Journal, "#d465a8", "Icon.PenLine", mente, ct).ConfigureAwait(false);
        var meditazione = await HabitAsync("Meditazione", HabitType.Action, "#4dbd91", "Icon.Leaf", mente, ct).ConfigureAwait(false);
        var camminata = await HabitAsync("Camminata", HabitType.Action, "#58b05c", "Icon.Footprints", null, ct).ConfigureAwait(false);
        var nienteFumo = await HabitAsync("Niente fumo", HabitType.Resistance, "#b05858", "Icon.Cigarette", null, ct).ConfigureAwait(false);

        await tags.SetTagsAsync(camminata, [tagMattina, tagBenessere], ct).ConfigureAwait(false);
        await tags.SetTagsAsync(meditazione, [tagMattina], ct).ConfigureAwait(false);
        await tags.SetTagsAsync(allenamento, [tagBenessere], ct).ConfigureAwait(false);

        // ---- configs ----
        await configs.UpsertAsync(allenamento, c =>
        {
            c.ScheduleType = ScheduleType.SpecificDays;
            c.ScheduleDaysJson = """["Mon","Wed","Fri"]""";
        }, ct).ConfigureAwait(false);
        foreach (var id in new[] { camminata, meditazione, aLetto })
            await configs.UpsertAsync(id, c => c.QuickComplete = true, ct).ConfigureAwait(false);

        // ---- metrics ----
        var mMinuti = await MetricAsync(allenamento, "Minuti", ct).ConfigureAwait(false);
        var mBicchieri = await MetricAsync(acqua, "Bicchieri", ct).ConfigureAwait(false);
        var mPagine = await MetricAsync(lettura, "Pagine", ct).ConfigureAwait(false);
        var mUmore = await metrics.CreateAsync(new MetricDefinition
        {
            HabitNodeId = mood,
            Name = "Umore",
            Type = MetricType.Integer,
            LabelsJson = """["Pessimo","Giù","Ok","Bene","Ottimo"]""",
        }, ct).ConfigureAwait(false);

        // ---- goal ----
        await goals.CreateAsync(new Goal
        {
            HabitNodeId = allenamento,
            MetricDefinitionId = mMinuti,
            Name = "150 minuti a settimana",
            TargetValue = 150,
            PeriodType = GoalPeriodType.Rolling,
            RollingDays = 7,
        }, ct).ConfigureAwait(false);

        // ---- completions over the window ----
        for (var day = start; day <= today; day = day.AddDays(1))
        {
            var weekday = day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            var isWorkoutDay = day.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday;

            if (Chance(0.80)) await ActionAsync(camminata, day, 7, 9, ct).ConfigureAwait(false);
            if (weekday && Chance(0.60)) await ActionAsync(meditazione, day, 6, 8, ct).ConfigureAwait(false);
            if (Chance(0.65)) await ActionAsync(aLetto, day, 23, 23, ct).ConfigureAwait(false);
            if (isWorkoutDay && Chance(0.85))
                await ValueAsync(allenamento, mMinuti, _rng.Next(25, 76), day, 18, 20, ct).ConfigureAwait(false);
            if (Chance(0.90))
                await ValueAsync(acqua, mBicchieri, _rng.Next(4, 11), day, 21, 22, ct).ConfigureAwait(false);
            if (Chance(0.55))
                await ValueAsync(lettura, mPagine, _rng.Next(8, 51), day, 22, 23, ct).ConfigureAwait(false);
            if (Chance(0.85))
                await ValueAsync(mood, mUmore, WeightedMood(), day, 21, 22, ct).ConfigureAwait(false);
            if (Chance(0.35))
                await diary.SaveEntryAsync(diario, day, JournalEntries[_rng.Next(JournalEntries.Length)], ct)
                    .ConfigureAwait(false);
        }

        // "Niente fumo" abstinence: 3 closed periods + 1 open → live ~3-week clean timer + relapse markers.
        await SeedAbstinencePeriodsAsync(nienteFumo, today, ct).ConfigureAwait(false);

        // ---- notes: a couple of general ones + scattered day notes ----
        await notes.CreateAsync(new Note { HabitNodeId = allenamento, BodyMd = "Programma del trainer: 3 sessioni a settimana, focus gambe il venerdì." }, ct).ConfigureAwait(false);
        await notes.CreateAsync(new Note { HabitNodeId = lettura, BodyMd = "In coda: *Il nome della rosa*, poi saggistica." }, ct).ConfigureAwait(false);
        await notes.CreateAsync(new Note { HabitNodeId = nienteFumo, BodyMd = "Giorno zero: 1 marzo. Le voglie calano dopo la terza settimana." }, ct).ConfigureAwait(false);

        var noteTargets = new[] { camminata, allenamento, mood, lettura, meditazione };
        for (var i = 0; i < 12; i++)
        {
            var day = start.AddDays(_rng.Next(Days));
            await notes.CreateAsync(new Note
            {
                HabitNodeId = noteTargets[_rng.Next(noteTargets.Length)],
                BodyMd = DayNotePhrases[_rng.Next(DayNotePhrases.Length)],
                TargetDate = day,
            }, ct).ConfigureAwait(false);
        }

        await settings.SetAsync(MarkerKey, today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
    }

    /// <summary>Populates an EXISTING habit (by name, case-insensitive) with ~90 days of plausible
    /// completions, filling ALL its metrics nature-aware (spec multivalue §6): Integer/Decimal =
    /// quantity with trend, Seconds = activity durations, scale = weighted. Counter with fields → 1–4
    /// series/day with ~10% of fields left empty; bare counter → N bare occurrences. No marker:
    /// repeated runs add more data. Triggered by <c>--seed-habit &lt;name&gt;</c>.</summary>
    public async Task SeedHabitAsync(string name, CancellationToken ct = default)
    {
        var rows = await tree.GetHomeTreeAsync(ct).ConfigureAwait(false);
        var row = rows.FirstOrDefault(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase) && r.HabitType is not null)
            ?? throw new InvalidOperationException($"Nessuna habit attiva chiamata '{name}'.");
        var type = row.HabitType!.Value;
        var defs = (await metrics.ListByHabitAsync(row.HabitNodeId, ct).ConfigureAwait(false))
            .OrderBy(m => m.SortOrder).ToList();

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow.UtcDateTime, clock.LocalTimeZone));
        var start = today.AddDays(-90);

        for (var day = start; day <= today; day = day.AddDays(1))
        {
            double progress = (day.DayNumber - start.DayNumber) / 90.0;
            switch (type)
            {
                case HabitType.Action when Chance(0.72):
                    await ActionAsync(row.HabitNodeId, day, 7, 21, ct).ConfigureAwait(false);
                    break;
                case HabitType.Journal when Chance(0.40):
                    await diary.SaveEntryAsync(row.HabitNodeId, day, JournalEntries[_rng.Next(JournalEntries.Length)], ct)
                        .ConfigureAwait(false);
                    break;
                case HabitType.Counter when defs.Count == 0 && Chance(0.85):
                    for (int i = _rng.Next(1, 6); i > 0; i--)
                        await completions.RecordAsync(Draft(row.HabitNodeId, day, 7, 21), ct: ct).ConfigureAwait(false);
                    break;
                case HabitType.Counter when defs.Count > 0 && Chance(0.72):
                    await SeriesDayAsync(row.HabitNodeId, defs, _rng.Next(1, 5), progress, day, ct).ConfigureAwait(false);
                    break;
                case HabitType.State when defs.Count > 0 && Chance(0.85):
                    await ValueAsync(row.HabitNodeId, defs[0].Id, WeightedMood(), day, 20, 22, ct).ConfigureAwait(false);
                    break;
            }
        }
    }

    /// <summary>One completion with N series rows, every cell populated by nature; ~10% of fields
    /// (never all in a row) left empty to exercise the "missing" case (spec D6).</summary>
    private async Task SeriesDayAsync(Guid habitId, List<MetricDefinition> defs, int series,
        double progress, DateOnly day, CancellationToken ct)
    {
        if (defs.Count == 0)
        {
            await completions.RecordAsync(Draft(habitId, day, 17, 21), ct: ct).ConfigureAwait(false);
            return;
        }
        var values = new List<CompletionValue>();
        for (int s = 0; s < series; s++)
        {
            var written = new List<CompletionValue>();
            for (int m = 0; m < defs.Count; m++)
            {
                bool skip = defs.Count > 1 && m > 0 && Chance(0.10);
                if (skip) continue;
                written.Add(MakeValue(defs[m], s, progress));
            }
            if (written.Count == 0) written.Add(MakeValue(defs[0], s, progress)); // row never fully empty
            values.AddRange(written);
        }
        await completions.RecordAsync(Draft(habitId, day, 17, 21), values, ct: ct).ConfigureAwait(false);
    }

    private CompletionValue MakeValue(MetricDefinition def, int seriesIndex, double progress)
    {
        var v = new CompletionValue { MetricDefinitionId = def.Id, SeriesIndex = seriesIndex };
        bool isScale = def.ScaleTemplateId is not null || def.LabelsJson is not null;
        if (isScale) v.ValueInteger = WeightedMood();                          // 1-based scale value
        else switch (def.Type)
        {
            case MetricType.Seconds: v.ValueSeconds = 20 + _rng.Next(0, 100) + (int)(progress * 30); break;
            case MetricType.Decimal: v.ValueDecimal = Math.Round((decimal)(0.5 + _rng.NextDouble() * 2.5 + progress), 2); break;
            default: v.ValueInteger = (int)(12 + progress * 20) + _rng.Next(0, 14); break;
        }
        return v;
    }

    private async Task SeedAbstinencePeriodsAsync(Guid habitId, DateOnly today, CancellationToken ct)
    {
        long Ms(DateOnly day, int hour)
        {
            var at = day.ToDateTime(new TimeOnly(hour, 0));
            return new DateTimeOffset(at, clock.LocalTimeZone.GetUtcOffset(at)).ToUnixTimeMilliseconds();
        }
        // 3 closed periods + 1 OPEN started ~3 weeks ago → live multi-week clean timer + relapse markers.
        var periods = new[]
        {
            (start: Ms(today.AddDays(-90), 9), end: (long?)Ms(today.AddDays(-67), 19)),
            (start: Ms(today.AddDays(-66), 9), end: (long?)Ms(today.AddDays(-40), 19)),
            (start: Ms(today.AddDays(-39), 9), end: (long?)Ms(today.AddDays(-21), 19)),
            (start: Ms(today.AddDays(-20), 9), end: (long?)null), // OPEN: current clean run (1-day gap after the -21 lapse)
        };
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        foreach (var p in periods)
            db.AbstinencePeriods.Add(new AbstinencePeriod
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                StartedAtMs = p.start,
                EndedAtMs = p.end,
            });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private bool Chance(double p) => _rng.NextDouble() < p;

    // Mostly 3-4, occasionally extremes.
    private int WeightedMood() => _rng.Next(10) switch { 0 => 1, 1 => 2, 2 or 3 => 5, 4 or 5 or 6 => 4, _ => 3 };

    private ValueTask<Guid> GroupAsync(string name, string color, string icon, Guid? parent, CancellationToken ct) =>
        nodes.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = name, Color = color, Icon = icon, ParentId = parent }, ct);

    private ValueTask<Guid> HabitAsync(string name, HabitType type, string color, string icon, Guid? parent, CancellationToken ct) =>
        nodes.CreateAsync(new HabitNode { NodeType = NodeType.Habit, HabitType = type, Name = name, Color = color, Icon = icon, ParentId = parent }, ct);

    private ValueTask<Guid> MetricAsync(Guid habitId, string name, CancellationToken ct) =>
        metrics.CreateAsync(new MetricDefinition { HabitNodeId = habitId, Name = name, Type = MetricType.Integer }, ct);

    private Completion Draft(Guid habitId, DateOnly day, int fromHour, int toHour, string? notesBody = null)
    {
        var at = day.ToDateTime(new TimeOnly(_rng.Next(fromHour, toHour + 1), _rng.Next(60)));
        return new Completion
        {
            HabitNodeId = habitId,
            CompletedAt = at,
            CompletedAtMs = new DateTimeOffset(at, clock.LocalTimeZone.GetUtcOffset(at)).ToUnixTimeMilliseconds(),
            Notes = notesBody,
            IsRetroactive = true,
        };
    }

    private async Task ActionAsync(Guid habitId, DateOnly day, int fromHour, int toHour, CancellationToken ct) =>
        await completions.RecordAsync(Draft(habitId, day, fromHour, toHour), ct: ct).ConfigureAwait(false);

    private async Task ValueAsync(Guid habitId, Guid metricId, int value, DateOnly day, int fromHour, int toHour, CancellationToken ct) =>
        await completions.RecordAsync(
            Draft(habitId, day, fromHour, toHour),
            values: [new CompletionValue { MetricDefinitionId = metricId, SeriesIndex = 0, ValueInteger = value }],
            ct: ct).ConfigureAwait(false);
}
