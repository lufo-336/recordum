using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels;

/// <summary>Il "x di y oggi" di un gruppo, scritto una volta: Home e Panoramica non possono divergere.</summary>
public static class GroupProgress
{
    /// <summary>Fatta oggi: registrata, oppure un'astinenza in corso (il giorno pulito è già fatto).</summary>
    public static bool IsDoneToday(HomeTreeRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.IsCompletedToday
            || (row.HabitType == HabitType.Resistance && row.AbstinenceStatus == AbstinenceStatus.Abstaining);
    }

    /// <summary>Le abitudini discendenti del gruppo, a ogni profondità; un'astinenza mai iniziata resta fuori dal totale.</summary>
    public static (int Done, int Total) Count(ILookup<Guid?, HomeTreeRow> byParent, Guid groupId)
    {
        ArgumentNullException.ThrowIfNull(byParent);
        int done = 0, total = 0;
        foreach (var child in byParent[groupId])
        {
            if (child.HabitType is null)
            {
                var (d, t) = Count(byParent, child.HabitNodeId);
                done += d; total += t;
            }
            else if (child.HabitType == HabitType.Resistance && child.AbstinenceStatus == AbstinenceStatus.NotStarted)
            {
                continue;
            }
            else
            {
                total++;
                if (IsDoneToday(child)) done++;
            }
        }
        return (done, total);
    }
}
