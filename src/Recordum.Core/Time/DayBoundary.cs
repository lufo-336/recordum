using Recordum.Core.Abstractions;

namespace Recordum.Core.Time;

/// <summary>
/// A quale giorno appartiene un istante, per una data abitudine: ora locale + orario di reset.
/// <para>Valore puro e sincrono — nessuna dipendenza, nessun I/O. Si risolve <b>una volta</b> in cima
/// a un'operazione (via <see cref="Abstractions.Services.IResetTimeResolver"/>) e poi si chiama dentro
/// i cicli: i siti di bucketing stanno dentro <c>foreach</c> e dentro VM dove una <c>await</c> per riga
/// diventerebbe una query per riga. Vedi spec R9 (2026-08-19) §3.</para>
/// <para>Il confine è <b>inclusivo in avanti</b>: con reset 04:00, le 04:00:00 in punto sono il primo
/// istante del giorno nuovo, le 03:59:59 l'ultimo del vecchio.</para>
/// </summary>
public sealed record DayBoundary(TimeZoneInfo TimeZone, TimeOnly ResetTime)
{
    /// <summary>Fuso UTC, reset a mezzanotte: l'identità, cioè il comportamento pre-R9.
    /// Usarlo solo come default esplicito nei test o dove non c'è (ancora) un'abitudine.</summary>
    public static DayBoundary Utc { get; } = new(TimeZoneInfo.Utc, TimeOnly.MinValue);

    public TimeZoneInfo TimeZone { get; } =
        TimeZone ?? throw new ArgumentNullException(nameof(TimeZone));

    /// <summary>Il giorno di appartenenza dell'istante.</summary>
    public DateOnly DayOf(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime;
        var candidate = DateOnly.FromDateTime(local - ResetTime.ToTimeSpan());

        // Durante l'ora ripetuta del cambio d'ora d'autunno l'ora locale non scorre monotona:
        // alla seconda passata l'orologio torna indietro (da 02:59:59 a 02:00:00), quindi se
        // l'orario di reset cade dentro l'ora ripetuta la sottrazione sopra può leggere un
        // orario di parete inferiore al reset anche quando, in assoluto, il giorno è già
        // cominciato (alla prima passata) — la candidata sbaglia di un giorno. Il sintomo esiste
        // solo dentro l'ora ambigua: lì, e solo lì, si convalida la candidata contro il confine
        // vero (StartOf/RangeOf, l'unica fonte di verità che già gestisce l'ambiguità).
        if (TimeZone.IsAmbiguousTime(local))
        {
            if (instant < StartOf(candidate)) return candidate.AddDays(-1);
            if (instant >= StartOf(candidate.AddDays(1))) return candidate.AddDays(1);
        }

        return candidate;
    }

    /// <summary>Il giorno di appartenenza di un timestamp Unix ms (<c>completed_at_ms</c>,
    /// <c>started_at_ms</c>, <c>ended_at_ms</c>). È la forma da preferire: il millisecondo è un
    /// istante non ambiguo, la colonna <c>DateTime</c> gemella no.</summary>
    public DateOnly DayOf(long unixMs) => DayOf(DateTimeOffset.FromUnixTimeMilliseconds(unixMs));

    /// <summary>Il giorno di appartenenza di un timestamp salvato come <c>DateTime</c> UTC
    /// (<c>HabitNode.CreatedAt</c>, <c>HabitConfig.StreakBrokenAt</c>: non hanno gemello <c>_ms</c>).
    /// EF li materializza con <c>Kind = Unspecified</c>, quindi lo <c>SpecifyKind</c> lo facciamo qui
    /// una volta invece di dimenticarcelo in venti chiamanti.</summary>
    public DateOnly DayOf(DateTime utcTimestamp)
        => DayOf(new DateTimeOffset(DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc)));

    /// <summary>L'unica definizione di "oggi" dell'applicazione.</summary>
    public DateOnly Today(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return DayOf(clock.UtcNow);
    }

    /// <summary>Il primo istante del giorno.</summary>
    public DateTimeOffset StartOf(DateOnly day)
    {
        var wall = day.ToDateTime(ResetTime);

        // Ora legale in avanti: l'orario di reset non esiste sull'orologio di parete (es. reset 02:00
        // il giorno in cui si salta da 02:00 a 03:00). Il giorno comincia all'istante della
        // transizione, cioè al primo orario di parete che esiste dopo il buco.
        while (TimeZone.IsInvalidTime(wall)) wall = wall.AddMinutes(1);

        // Ora legale all'indietro: l'orario di reset capita due volte. Vale la prima passata
        // (offset maggiore), così entrambe le occorrenze appartengono al giorno che comincia.
        var offset = TimeZone.IsAmbiguousTime(wall)
            ? TimeZone.GetAmbiguousTimeOffsets(wall).Max()
            : TimeZone.GetUtcOffset(wall);

        return new DateTimeOffset(wall, offset);
    }

    /// <summary>Mezzogiorno locale del giorno: l'istante a cui si stampigliano le registrazioni
    /// retroattive (spec R9 §5). Sta dentro il giorno per qualunque reset plausibile (00:00-06:00) e
    /// regge il cambio d'ora — le transizioni sono di notte, quindi il mezzogiorno dell'orologio
    /// esiste sempre e non serve il trattamento degli orari invalidi di <see cref="StartOf"/>.</summary>
    public DateTimeOffset NoonOf(DateOnly day)
    {
        var wall = day.ToDateTime(new TimeOnly(12, 0));
        return new DateTimeOffset(wall, TimeZone.GetUtcOffset(wall));
    }

    /// <summary>La finestra semiaperta [inizio, fine) del giorno — per i filtri di query, che devono
    /// pescare esattamente ciò che <see cref="DayOf(long)"/> poi bucketizza in quel giorno.</summary>
    public (DateTimeOffset From, DateTimeOffset ToExclusive) RangeOf(DateOnly day)
        => (StartOf(day), StartOf(day.AddDays(1)));

    /// <summary>La finestra semiaperta che copre da <paramref name="fromDay"/> a
    /// <paramref name="toDayInclusive"/> compreso.</summary>
    public (DateTimeOffset From, DateTimeOffset ToExclusive) RangeOf(DateOnly fromDay, DateOnly toDayInclusive)
        // I due estremi del tipo sono sentinelle, e vanno trattati come tali in ENTRAMBE le direzioni:
        // il chiamante reale è AbstinencePeriodListViewModel, che per "tutte le registrazioni" scrive
        // `from ?? DateOnly.MinValue, to ?? DateOnly.MaxValue`.
        // - MaxValue non ha un "giorno dopo" rappresentabile: AddDays esploderebbe.
        // - MinValue non ha un istante rappresentabile in un fuso a offset POSITIVO: la mezzanotte
        //   dell'anno 1 letta in UTC cade nell'anno 0, e DateTimeOffset lancia. Il difetto non si
        //   vedeva con DayBoundary.Utc (offset zero) — è per questo che il test lo prova in un fuso
        //   Rome-like, non in UTC.
        => (fromDay == DateOnly.MinValue ? DateTimeOffset.MinValue : StartOf(fromDay),
            toDayInclusive == DateOnly.MaxValue ? DateTimeOffset.MaxValue : StartOf(toDayInclusive.AddDays(1)));
}
