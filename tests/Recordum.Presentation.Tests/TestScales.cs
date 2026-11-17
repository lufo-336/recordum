using Recordum.Core.Models;

namespace Recordum.Presentation.Tests;

/// <summary>Scaglioni con nome per posizione, a partire dal valore <paramref name="min"/> (un nome vuoto = scaglione senza nome).</summary>
internal static class TestScales
{
    public static List<ScaleAnchor> Anchors(int min, params string[] names) =>
        names.Select((n, i) => new ScaleAnchor { Id = Guid.NewGuid(), Value = min + i, Label = n }).ToList();
}
