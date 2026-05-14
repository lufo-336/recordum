namespace Recordum.Core.Models;

public sealed class Note
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }
    public string BodyMd { get; set; } = string.Empty;
    public bool IsSensitive { get; set; }
    public DateTime CreatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
    public List<Attachment> Attachments { get; set; } = [];
}
