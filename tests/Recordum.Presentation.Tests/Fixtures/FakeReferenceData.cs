using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeReferenceData : IReferenceDataQueryService
{
    public List<UnitOfMeasure> Units { get; } = new()
    {
        new UnitOfMeasure { Id = Guid.NewGuid(), Code = "l", Name = "Litre" },
        new UnitOfMeasure { Id = Guid.NewGuid(), Code = "hours", Name = "Hours" },
        new UnitOfMeasure { Id = Guid.NewGuid(), Code = "minutes", Name = "Minutes" },
        new UnitOfMeasure { Id = Guid.NewGuid(), Code = "seconds", Name = "Seconds" },
    };
    public List<ScaleTemplate> Scales { get; } = new() { new ScaleTemplate { Id = Guid.NewGuid(), Code = "mood-5", Name = "Mood (1-5)" } };
    public ValueTask<IReadOnlyList<UnitOfMeasure>> ListUnitsAsync(CancellationToken ct = default) => new((IReadOnlyList<UnitOfMeasure>)Units);
    public ValueTask<IReadOnlyList<ScaleTemplate>> ListScalesAsync(CancellationToken ct = default) => new((IReadOnlyList<ScaleTemplate>)Scales);
}
