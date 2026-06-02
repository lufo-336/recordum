using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class ComposedHabitService(IDbContextFactory<RecordumDbContext> dbFactory) : IComposedHabitService
{
    public async ValueTask<Guid> CreateComposedHabitAsync(ComposedHabitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.HabitNodes.Add(request.Node);
        if (request.Config is not null) db.HabitConfigs.Add(request.Config);
        foreach (var m in request.Metrics) db.MetricDefinitions.Add(m);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return request.Node.Id;
    }
}
