namespace Recordum.Core.Abstractions.Services.Dtos;

public sealed record HeatmapCell(DateOnly Date, int Count, int RetroactiveCount = 0);
