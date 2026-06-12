using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

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
    ITagService tags,
    IAppSettingService settings,
    IHabitTreeQueryService tree,
    IClock clock)
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
        var mVoglie = await MetricAsync(nienteFumo, "Voglie resistite", ct).ConfigureAwait(false);
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
            if (Chance(0.70))
                await ValueAsync(nienteFumo, mVoglie, _rng.Next(0, 5), day, 20, 22, ct).ConfigureAwait(false);
            if (Chance(0.35))
                await completions.RecordAsync(
                    Draft(diario, day, 22, 23, notesBody: JournalEntries[_rng.Next(JournalEntries.Length)]),
                    ct: ct).ConfigureAwait(false);
        }

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

    /// <summary>Populates an EXISTING habit (looked up by name, case-insensitive) with ~90 days
    /// of plausible completions: ~5 days a week, values with a slight upward trend for measured
    /// types. Creates a "Ripetizioni" metric when the type needs values and none exists.
    /// Triggered by <c>--seed-habit &lt;name&gt;</c>; no marker — repeated runs add more data.</summary>
    public async Task SeedHabitAsync(string name, CancellationToken ct = default)
    {
        var rows = await tree.GetHomeTreeAsync(ct).ConfigureAwait(false);
        var row = rows.FirstOrDefault(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase) && r.HabitType is not null)
            ?? throw new InvalidOperationException($"Nessuna habit attiva chiamata '{name}'.");
        var type = row.HabitType!.Value;

        Guid? metricId = null;
        if (type is HabitType.Counter or HabitType.Resistance or HabitType.State)
        {
            var existing = await metrics.ListByHabitAsync(row.HabitNodeId, ct).ConfigureAwait(false);
            metricId = existing.Count > 0
                ? existing[0].Id
                : await MetricAsync(row.HabitNodeId, "Ripetizioni", ct).ConfigureAwait(false);
        }

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow.UtcDateTime, clock.LocalTimeZone));
        var start = today.AddDays(-90);

        for (var day = start; day <= today; day = day.AddDays(1))
        {
            if (!Chance(0.72)) continue;
            // Slight progression: later days trend higher (e.g. push-up counts growing).
            var progress = (day.DayNumber - start.DayNumber) / 90.0;
            var value = (int)(12 + progress * 20) + _rng.Next(0, 14);

            if (type == HabitType.Action)
                await ActionAsync(row.HabitNodeId, day, 7, 21, ct).ConfigureAwait(false);
            else if (type == HabitType.Journal)
                await completions.RecordAsync(
                    Draft(row.HabitNodeId, day, 7, 21, notesBody: JournalEntries[_rng.Next(JournalEntries.Length)]),
                    ct: ct).ConfigureAwait(false);
            else
                await ValueAsync(row.HabitNodeId, metricId!.Value, value, day, 7, 21, ct).ConfigureAwait(false);
        }
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
