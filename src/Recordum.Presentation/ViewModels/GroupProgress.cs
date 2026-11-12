using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels;

/// <summary>Il "x di y" di un gruppo e il "fatta" di un giorno, scritti una volta: Home e Panoramica non possono divergere.</summary>
public static class GroupProgress
{
    /// <summary>Fatta oggi: registrata, oppure un'astinenza in corso (il giorno pulito è già fatto).</summary>
    public static bool IsDoneToday(HomeTreeRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.IsCompletedToday
            || (row.HabitType == HabitType.Resistance && row.AbstinenceStatus == AbstinenceStatus.Abstaining);
    }

    /// <summary>Di quante celle indietro, nella striscia dell'abitudine, sta il giorno scelto nella Home (che è un giorno
    /// dell'app). Un'abitudine col reset alle 05:00 in un'app a mezzanotte, alle 02:00, è ancora a ieri: "ieri" della Home
    /// è la sua ultima cella, ed è lì che cade la registrazione.</summary>
    public static int CellBack(HomeTreeRow row, int daysBack, IClock? clock)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (daysBack == 0 || clock is null || row.HabitDay is not { } habit || row.AppDay is not { } app) return daysBack;
        return Math.Max(0, daysBack - (app.Today(clock).DayNumber - habit.Today(clock).DayNumber));
    }

    /// <summary>Il conto di quel giorno (0 = oggi, l'ultima cella); 0 fuori dalla finestra.</summary>
    public static int CountOn(HomeTreeRow row, int cellBack)
    {
        ArgumentNullException.ThrowIfNull(row);
        var counts = row.CompletionsLast14Days.IsDefaultOrEmpty ? row.CompletionsLast7Days : row.CompletionsLast14Days;
        return !counts.IsDefault && cellBack >= 0 && cellBack < counts.Length ? counts[^(cellBack + 1)] : 0;
    }

    /// <summary>Il valore di quel giorno (somma del contatore, ultimo stato); null se manca o fuori dalla finestra.</summary>
    public static decimal? ValueOn(HomeTreeRow row, int cellBack)
    {
        ArgumentNullException.ThrowIfNull(row);
        var values = row.ValuesLast14Days;
        return !values.IsDefault && cellBack >= 0 && cellBack < values.Length ? values[^(cellBack + 1)] : null;
    }

    /// <summary>Quel giorno ha una registrazione: per oggi è <see cref="HomeTreeRow.IsCompletedToday"/>, per un giorno
    /// passato la sua cella ha un conto o un valore.</summary>
    public static bool HasEntryOn(HomeTreeRow row, int cellBack)
    {
        ArgumentNullException.ThrowIfNull(row);
        return cellBack == 0 ? row.IsCompletedToday : CountOn(row, cellBack) > 0 || ValueOn(row, cellBack) is not null;
    }

    /// <summary>Fatta in quel giorno: <see cref="IsDoneToday"/> per oggi, <see cref="HasEntryOn"/> per un giorno passato
    /// (la banda dell'astinenza copre già i giorni puliti).</summary>
    public static bool IsDoneOn(HomeTreeRow row, int cellBack) => cellBack == 0 ? IsDoneToday(row) : HasEntryOn(row, cellBack);

    /// <summary>Le abitudini discendenti del gruppo (null = dalla radice), a ogni profondità; un'astinenza mai iniziata resta
    /// fuori dal totale. <paramref name="isDone"/> null = fatta oggi.</summary>
    public static (int Done, int Total) Count(ILookup<Guid?, HomeTreeRow> byParent, Guid? groupId, Func<HomeTreeRow, bool>? isDone = null)
    {
        ArgumentNullException.ThrowIfNull(byParent);
        isDone ??= IsDoneToday;
        int done = 0, total = 0;
        foreach (var child in byParent[groupId])
        {
            if (child.HabitType is null)
            {
                var (d, t) = Count(byParent, child.HabitNodeId, isDone);
                done += d; total += t;
            }
            else if (child.HabitType == HabitType.Resistance && child.AbstinenceStatus == AbstinenceStatus.NotStarted)
            {
                continue;
            }
            else
            {
                total++;
                if (isDone(child)) done++;
            }
        }
        return (done, total);
    }
}
