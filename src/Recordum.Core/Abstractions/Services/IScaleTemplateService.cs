using Recordum.Core.Enums;

namespace Recordum.Core.Abstractions.Services;

public sealed record ScaleAnchorDraft(int Value, string? Label);

public sealed record ScaleDraft(string Name, int MinValue, int MaxValue, ScalePrecision Precision,
    IReadOnlyList<ScaleAnchorDraft> Anchors);

/// <summary>Quanto è usata una scala: abitudini che la leggono e dove cadono i valori registrati.</summary>
public sealed record ScaleUsage(int HabitCount, decimal? RecordedMin, decimal? RecordedMax, bool RecordedHasDecimals);

public interface IScaleTemplateService
{
    ValueTask<Guid> CreateAsync(ScaleDraft draft, CancellationToken ct = default);

    /// <summary>Le scale ufficiali e le modifiche che contraddicono i valori registrati lanciano
    /// <see cref="InvalidOperationException"/> (col primo codice di <c>ScaleRules</c> nel messaggio).</summary>
    ValueTask UpdateAsync(Guid id, ScaleDraft draft, CancellationToken ct = default);

    /// <summary>Copia una scala (anche ufficiale) con i nomi già tradotti da chi chiama; ripunta solo
    /// <paramref name="repointMetricId"/> sulla copia.</summary>
    ValueTask<Guid> DuplicateAsync(Guid sourceId, string name, IReadOnlyList<ScaleAnchorDraft> anchors,
        Guid? repointMetricId, CancellationToken ct = default);

    ValueTask<ScaleUsage> GetUsageAsync(Guid id, CancellationToken ct = default);
}
