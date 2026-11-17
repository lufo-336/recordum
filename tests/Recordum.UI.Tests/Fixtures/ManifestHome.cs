using System.Collections.Immutable;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Time;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Home;
using Recordum.Presentation.ViewModels.Quick;

namespace Recordum.UI.Tests.Fixtures;

/// <summary>La Home "manifesto" (onda Giornata): le abitudini di <c>docs/DOGFOODING.md</c> come nel mockup
/// <c>home-giorno.html</c> — un gruppo "Routine del mattino" con tre azioni e sei abitudini sciolte (nove righe di
/// abitudine + una di gruppo), 14 giorni di dati, oggi a metà (4 fatte su 9), ieri lasciato a metà, colori diversi
/// della palette dell'app, un'astinenza in corso da 23 giorni. L'orologio è fisso sul 2026-11-10 12:00 (fuso dell'app: UTC,
/// come la Home senza resolver).</summary>
internal static class ManifestHome
{
    /// <summary>L'istante fisso del test: mai l'orologio vero.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    public static DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);

    public static readonly Guid GroupId = Guid.Parse("00000000-0000-0000-0000-0000000000a0");

    // Palette dell'app (ColorSwatchPicker): tutti diversi.
    private const string Teal = "#2F9C8C", Green = "#5F9A3C", Cyan = "#2F8FB0", Amber = "#C98A1E",
        Indigo = "#4F63C4", Pink = "#C2508A", Orange = "#D0603A", Olive = "#9A9A2A", Violet = "#7A5BC4", Grey = "#6B7280";

    /// <summary>Le righe nell'ordine della Home: il gruppo, le sue tre azioni, poi le altre sei. Indice 13 dei 14 giorni = oggi.</summary>
    public static IReadOnlyList<HomeTreeRow> Rows(DateOnly today)
    {
        var open = new DateTimeOffset(today.AddDays(-23).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var order = 0;

        HomeTreeRow Make(string name, string color, HabitType? type, Guid? parent, int[] counts, bool doneToday,
            decimal?[]? values = null, int streak = 0, int childCount = 0, Action<Builder>? more = null)
        {
            var b = new Builder();
            more?.Invoke(b);
            return new HomeTreeRow(Guid.NewGuid(), parent, name, color, type, null, streak, doneToday, PinScope.None,
                childCount, false, ImmutableArray.Create(counts[^7..]), ImmutableArray<Guid>.Empty,
                CompletionsLast14Days: ImmutableArray.Create(counts),
                TodayCounterTotal: b.CounterTotal,
                TodayCounterUnitCode: b.UnitCode,
                TodayCounterUnitName: b.UnitCode,
                TodayCounterUnitIsOfficial: b.UnitCode is not null,
                TodayStateValue: b.StateValue,
                AbstinenceStatus: b.Abstinence,
                AbstinenceOpenSinceMs: b.Abstinence == AbstinenceStatus.Abstaining ? open : null,
                JournalEntriesThisWeek: b.JournalThisWeek,
                SortOrder: order++,
                ValuesLast14Days: values is null ? default : ImmutableArray.Create(values),
                HabitDay: type is null ? null : DayBoundary.Utc,
                AppDay: type is null ? null : DayBoundary.Utc);
        }

        var group = Make("Routine del mattino", Grey, null, null, new int[14], false, childCount: 3) with { HabitNodeId = GroupId };
        return
        [
            group,
            Make("Stretching", Teal, HabitType.Action, GroupId, [1, 1, 0, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1], true, streak: 2),
            Make("Vitamine", Green, HabitType.Action, GroupId, [1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 1, 0, 1], true, streak: 1),
            Make("Letto rifatto", Cyan, HabitType.Action, GroupId, [1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, 0], false),
            Make("Senza fumo", Amber, HabitType.Resistance, null, [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1], true, streak: 23,
                more: b => b.Abstinence = AbstinenceStatus.Abstaining),
            Make("Lettura", Indigo, HabitType.Counter, null, [1, 0, 1, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0], false,
                values: [20, null, 35, 12, 40, null, null, 25, null, 30, 15, null, 30, null],
                more: b => { b.CounterTotal = 0m; b.UnitCode = "pages"; }),
            Make("Umore", Pink, HabitType.State, null, [1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 1], true,
                values: [4, 3, null, 4, 5, null, null, 3, 4, null, 4, 3, null, 4],
                more: b => { b.StateValue = 4; b.StateLabel = "Bene"; }),
            Make("Allenamento", Orange, HabitType.Counter, null, [0, 1, 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0], false,
                values: [null, 12, null, 14, null, null, null, 12, null, 14, null, null, null, null],
                more: b => { b.CounterTotal = 0m; b.UnitCode = "sets"; }),
            Make("Diario del cibo", Olive, HabitType.Journal, null, [3, 3, 2, 3, 1, 0, 1, 2, 3, 2, 3, 1, 0, 0], false,
                more: b => b.JournalThisWeek = 6),
            Make("Diario della giornata", Violet, HabitType.Journal, null, [1, 1, 1, 0, 1, 0, 0, 1, 1, 1, 0, 1, 1, 0], false,
                more: b => b.JournalThisWeek = 4),
        ];
    }

    /// <summary>Una Home sul manifesto con l'orologio fisso. Chi la mostra chiama <c>LoadCommand</c>.</summary>
    public static HomeViewModel Home(IReadOnlyList<HomeTreeRow>? rows = null, HomeRows? style = null)
    {
        var query = new FakeHabitTreeQueryService();
        query.Rows.AddRange(rows ?? Rows(Today));
        return new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService(),
            quickActions: new QuickActionHandlerSelector([]),
            clock: new PrivateDbHost.FakeClock { Now = Now },
            rows: style is { } s ? new HomeRowsPreference(new Services.FakeAppSettingService(), s) : null);
    }

    private sealed class Builder
    {
        public decimal? CounterTotal;
        /// <summary>Codice di un'unità ufficiale senza simbolo: la riga ne scrive il nome tradotto.</summary>
        public string? UnitCode;
        public int? StateValue;
        public string? StateLabel;
        public AbstinenceStatus Abstinence = AbstinenceStatus.NotStarted;
        public int? JournalThisWeek;
    }
}
