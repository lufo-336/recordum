using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels.Quick;

public sealed class QuickActionHandlerSelector
{
    private readonly Dictionary<HabitType, IQuickActionHandler> _byType;

    public QuickActionHandlerSelector(IEnumerable<IQuickActionHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _byType = handlers.ToDictionary(h => h.HabitType);
    }

    public IQuickActionHandler? For(HabitType type)
        => _byType.GetValueOrDefault(type);

    public IReadOnlyDictionary<HabitType, IQuickActionHandler> All => _byType;
}
