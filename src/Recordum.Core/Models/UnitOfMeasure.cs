namespace Recordum.Core.Models;

public sealed class UnitOfMeasure
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public bool IsOfficial { get; set; }
    public DateTime CreatedAt { get; set; }
}
