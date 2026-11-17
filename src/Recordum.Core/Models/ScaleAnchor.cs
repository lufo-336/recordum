namespace Recordum.Core.Models;

/// <summary>Uno scaglione: un valore intero della scala con un nome e, se serve, il tag che ne deriva.</summary>
public sealed class ScaleAnchor
{
    public Guid Id { get; set; }
    public Guid ScaleTemplateId { get; set; }
    public int Value { get; set; }
    public string? Label { get; set; }
    public Guid? TagId { get; set; }
    public DateTime CreatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public ScaleTemplate ScaleTemplate { get; set; } = null!;
    public Tag? Tag { get; set; }
}
