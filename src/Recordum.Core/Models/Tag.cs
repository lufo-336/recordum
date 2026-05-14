namespace Recordum.Core.Models;

public sealed class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<HabitNodeTag> HabitNodeTags { get; set; } = [];
}
