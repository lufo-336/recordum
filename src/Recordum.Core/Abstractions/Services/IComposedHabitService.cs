namespace Recordum.Core.Abstractions.Services;

public interface IComposedHabitService
{
    /// <summary>Crea nodo + config + metriche in UNA transazione. Ritorna l'id del nodo.</summary>
    ValueTask<Guid> CreateComposedHabitAsync(ComposedHabitRequest request, CancellationToken ct = default);
}
