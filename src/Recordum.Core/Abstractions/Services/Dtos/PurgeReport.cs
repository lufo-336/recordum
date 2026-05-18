namespace Recordum.Core.Abstractions.Services.Dtos;

public sealed record PurgeError(Guid HabitNodeId, string Stage, string Message);

public sealed record PurgeReport(int Purged, int Skipped, IReadOnlyList<PurgeError> Errors);
