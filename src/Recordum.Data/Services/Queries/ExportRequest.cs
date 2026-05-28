using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Data.Services.Queries;

public sealed record ExportRequest(
    ExportSubject Subject,
    Guid? HabitNodeId,
    DateOnly From,
    DateOnly To,
    ExportLayout Layout);
