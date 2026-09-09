using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions.Services;

namespace Recordum.Data.Maintenance;

/// <summary>
/// Passo di manutenzione una tantum introdotto con R9: ri-stampiglia le registrazioni
/// <b>retroattive</b> esistenti al mezzogiorno locale del giorno che già dichiarano.
/// <para>Erano salvate a mezzanotte UTC del giorno scelto dall'utente; col confine locale +
/// <c>reset_time</c> quell'istante può cadere nel giorno precedente. Spostarle non cambia il loro
/// significato, lo preserva — quel giorno l'utente l'aveva scelto a mano. Le registrazioni
/// <b>non</b> retroattive non si toccano: l'istante è l'istante, e la rilettura le colloca da sé.</para>
/// <para>Non è una migration: "mezzogiorno locale" richiede TimeZoneInfo e la storia dell'ora legale,
/// che <c>datetime(…,'localtime')</c> di SQLite non sa fare sulle date passate. Gira
/// nell'initializer dopo le migrazioni, è idempotente, ed è guardato da un AppSetting.</para>
/// </summary>
public sealed partial class RetroactiveNoonBackfill(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IResetTimeResolver resetTimes,
    IAppSettingService settings,
    ILogger<RetroactiveNoonBackfill> logger)
{
    /// <summary>Il guard del passo. Deliberatamente <b>fuori</b> da <c>AppSettingSeed.All</c>: quel
    /// registro è <c>HasData</c>, cioè parte del modello EF, e aggiungerci una riga pretende una
    /// migration — che R9 non ha (nessun cambio di schema, schema_version resta 2.17). La chiave
    /// nasce quindi alla prima scrittura, e "assente" vale "da fare", che è esattamente il
    /// comportamento richiesto su ogni DB già esistente.</summary>
    internal const string DoneKey = "maintenance.retro_noon_backfill_done";

    /// <summary>Ri-stampiglia le retroattive; torna quante righe ha spostato. Zero se il passo è già
    /// stato fatto, o se non c'era niente da spostare.</summary>
    public async ValueTask<int> RunAsync(CancellationToken ct = default)
    {
        // Su un DB creato prima di questa chiave il valore è assente: assente = "da fare".
        if (await settings.GetAsync(DoneKey, ct).ConfigureAwait(false) == "true") return 0;

        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.Completions
            .Where(c => c.IsRetroactive)
            .Select(c => new { c.Id, c.HabitNodeId, c.CompletedAtMs })
            .ToListAsync(ct).ConfigureAwait(false);

        if (rows.Count == 0)
        {
            await settings.SetAsync(DoneKey, "true", ct).ConfigureAwait(false);
            LogBackfilled(0);
            return 0;
        }

        var boundaries = await resetTimes
            .GetDayBoundariesAsync(rows.Select(r => r.HabitNodeId).Distinct().ToList(), ct)
            .ConfigureAwait(false);

        var moved = 0;
        foreach (var r in rows)
        {
            var day = boundaries[r.HabitNodeId];
            // Il giorno DICHIARATO dalla riga è quello del suo istante letto in UTC: è così che è
            // stato scritto (mezzanotte UTC del giorno scelto). Non rileggerlo col confine nuovo,
            // o si sposterebbe proprio ciò che stiamo cercando di tenere fermo.
            var declared = DateOnly.FromDateTime(
                DateTimeOffset.FromUnixTimeMilliseconds(r.CompletedAtMs).UtcDateTime);
            var noon = day.NoonOf(declared);
            var newMs = noon.ToUnixTimeMilliseconds();
            if (newMs == r.CompletedAtMs) continue;

            var entity = await db.Completions.FirstAsync(c => c.Id == r.Id, ct).ConfigureAwait(false);
            // Entrambe le colonne, sempre: lasciarne indietro una è il modo classico di rompere la
            // sincronizzazione last-write-wins, che si regge su CompletedAtMs.
            entity.CompletedAt = noon.UtcDateTime;
            entity.CompletedAtMs = newMs;
            moved++;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await settings.SetAsync(DoneKey, "true", ct).ConfigureAwait(false);
        LogBackfilled(moved);
        return moved;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Retroactive noon backfill: {Moved} completions restamped")]
    private partial void LogBackfilled(int moved);
}
