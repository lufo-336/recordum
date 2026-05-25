using Recordum.Data.Interceptors;
using Xunit;

namespace Recordum.Data.Tests.Interceptors;

public class HabitConfigHistoryColumnsTests
{
    private static readonly string[] ExpectedExcluded =
    {
        "Id","HabitNodeId","CreatedAt","UpdatedAt","UpdatedAtMs",
        "QuickComplete","AiDataSharing","StreakBrokenAt"
    };

    [Fact]
    public void Excluded_columns_match_spec()
    {
        Assert.Equal(
            ExpectedExcluded.OrderBy(s => s),
            HabitConfigHistoryColumns.Excluded.OrderBy(s => s));
    }

    [Fact]
    public void Versioned_plus_excluded_covers_HabitConfig_writable_props()
    {
        var habitConfigProps = typeof(Recordum.Core.Models.HabitConfig)
            .GetProperties()
            .Where(p => p.CanWrite && p.PropertyType.Namespace != "Recordum.Core.Models")
            .Select(p => p.Name)
            .ToHashSet();
        var union = HabitConfigHistoryColumns.Versioned
            .Concat(HabitConfigHistoryColumns.Excluded).ToHashSet();
        Assert.Equal(habitConfigProps.OrderBy(s => s), union.OrderBy(s => s));
    }

    [Fact]
    public void StreakBrokenAt_is_excluded_from_versioning()
    {
        Assert.Contains("StreakBrokenAt", HabitConfigHistoryColumns.Excluded);
        Assert.DoesNotContain("StreakBrokenAt", HabitConfigHistoryColumns.Versioned);
    }
}
