namespace Recordum.Core.Scales;

public sealed record NamedAnchor(int Value, string Name, Guid? TagId);

public sealed record ZoneReading(NamedAnchor? Exact, NamedAnchor? Nearest, NamedAnchor? Below, NamedAnchor? Above);

public static class ScaleZone
{
    /// <summary>Dove cade <paramref name="value"/> fra gli scaglioni (in qualunque ordine); a parità vince quello sotto.</summary>
    public static ZoneReading Read(IReadOnlyList<NamedAnchor> named, decimal value)
    {
        NamedAnchor? exact = null, below = null, above = null;
        foreach (var a in named)
        {
            if (a.Value == value) exact = a;
            else if (a.Value < value) { if (below is null || a.Value > below.Value) below = a; }
            else if (above is null || a.Value < above.Value) above = a;
        }
        var nearest = exact
            ?? (below is null ? above : above is null ? below : value - below.Value <= above.Value - value ? below : above);
        return new ZoneReading(exact, nearest, below, above);
    }
}
