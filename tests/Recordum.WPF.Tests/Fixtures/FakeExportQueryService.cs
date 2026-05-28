using Recordum.Data.Services.Queries;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeExportQueryService : IExportQueryService
{
    public ExportRequest? LastRequest { get; private set; }

    public ValueTask<ExportModel> BuildAsync(ExportRequest request, CancellationToken ct = default)
    {
        LastRequest = request;
        return ValueTask.FromResult(new ExportModel(request.Subject, "Run", request.From, request.To,
            new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero), []));
    }
}
