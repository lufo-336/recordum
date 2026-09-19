namespace Recordum.Presentation.ViewModels;

/// <summary>Dove sta il cursore rispetto alla riga sotto di esso.</summary>
public enum DropZone
{
    Before,
    Onto,
    After,
}

/// <summary>Intenzione di rilascio risolta: nuovo genitore, il fratello a cui ancorarsi
/// (null = in coda), e da che lato.</summary>
public readonly record struct DropPlan(Guid? Parent, Guid? Anchor, bool PlaceAfter);

/// <summary>La mappatura pura da zona+bersaglio a piano di rilascio. Viveva dentro
/// TreeDragDropBehavior, che è pieno di System.Windows, ma non ha mai avuto niente di WPF dentro:
/// è uno switch su un enum. Il behavior resta nella shell perché maneggia eventi del mouse; questo
/// va con i ViewModel, che è dove viene consumato.</summary>
public static class DropResolver
{
    /// <summary>Su un gruppo → in coda dentro di esso. Su un'abitudine → fratello subito dopo.
    /// Before/After → riordino attorno al bersaglio.</summary>
    public static DropPlan ResolveDrop(DropZone zone, bool targetIsGroup, Guid targetId, Guid? targetParentId) => zone switch
    {
        DropZone.Onto when targetIsGroup => new DropPlan(targetId, null, false),
        DropZone.Before => new DropPlan(targetParentId, targetId, false),
        // After, e Onto-su-un'abitudine, mettono entrambi la riga subito dopo il bersaglio fra i suoi fratelli.
        _ => new DropPlan(targetParentId, targetId, true),
    };
}
