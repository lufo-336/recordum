namespace Recordum.Core.Abstractions.Services.Dtos;

public sealed record DailyValue(DateOnly Date, decimal Sum);
