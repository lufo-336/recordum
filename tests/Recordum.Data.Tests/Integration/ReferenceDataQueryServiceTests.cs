using FluentAssertions;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class ReferenceDataQueryServiceTests
{
    [Fact]
    public async Task Lists_seeded_units_and_scales()
    {
        await using var fx = new DbFixture();
        var svc = new ReferenceDataQueryService(fx.Factory);
        (await svc.ListUnitsAsync()).Should().Contain(u => u.Code == "l");
        (await svc.ListScalesAsync()).Should().Contain(s => s.Code == "mood-5");
    }
}
