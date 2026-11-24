using System.Globalization;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.Settings;

/// <summary>Riga dell'elenco "voci da sistemare" in Impostazioni › Unisci.</summary>
public sealed class SyncItemToFixViewModel(SyncItemToFix item)
{
    public SyncItemToFix Item { get; } = item;
    public SyncItemKind Kind => Item.Kind;
    public Guid EntityId => Item.EntityId;
    public Guid HabitNodeId => Item.HabitNodeId;
    public string HabitName => Item.HabitName;
    public string HabitColorHex => string.IsNullOrWhiteSpace(Item.HabitColor) ? "#808080" : Item.HabitColor;
    public DateOnly Day => Item.Day;
    public string DayDisplay => Item.Day.ToString("ddd d MMM", CultureInfo.CurrentCulture);

    public string KindLabel => Item.Kind switch
    {
        SyncItemKind.Note => Strings.Sync_Items_Kind_Note,
        SyncItemKind.DiaryEntry => Strings.Sync_Items_Kind_DiaryEntry,
        SyncItemKind.RecordNote => Strings.Sync_Items_Kind_RecordNote,
        SyncItemKind.RepairedPeriod => Strings.Sync_Items_Kind_RepairedPeriod,
        _ => string.Empty,
    };

    public bool IsRepairedPeriod => Item.Kind == SyncItemKind.RepairedPeriod;
    public bool CanAccept => IsRepairedPeriod;
    public bool ShowChevron => !IsRepairedPeriod;
    public string DetailText => IsRepairedPeriod ? Strings.Sync_Items_RepairedPeriod_Detail : Item.Excerpt;
    public bool HasExcerpt => !IsRepairedPeriod && !string.IsNullOrEmpty(Item.Excerpt);
    public bool HasRepairedDetail => IsRepairedPeriod;

    public string IconKey => Item.Kind switch
    {
        SyncItemKind.DiaryEntry or SyncItemKind.RecordNote => "Icon.BookOpen",
        SyncItemKind.Note => "Icon.FileText",
        SyncItemKind.RepairedPeriod => "Icon.Hourglass",
        _ => "Icon.FileText",
    };
}
