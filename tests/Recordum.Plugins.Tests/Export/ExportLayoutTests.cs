using Recordum.Plugins.Contracts.Export;
using Xunit;

namespace Recordum.Plugins.Tests.Export;

public sealed class ExportLayoutTests
{
    [Fact]
    public void Parse_null_or_empty_returns_defaults()
    {
        var l = ExportLayout.Parse(null);
        Assert.True(l.Metadata);
        Assert.True(l.Summary);
        Assert.True(l.Heatmap);
        Assert.True(l.Completions);
        Assert.True(l.Notes);
        Assert.Equal("all", l.Range);
        Assert.Equal(l, ExportLayout.Parse("{}"));
        Assert.Equal(l, ExportLayout.Parse("   "));
    }

    [Fact]
    public void ToJson_then_Parse_round_trips()
    {
        var original = new ExportLayout(Metadata: true, Summary: false, Heatmap: false, Completions: true, Notes: false, Range: "90d");
        Assert.Equal(original, ExportLayout.Parse(original.ToJson()));
    }

    [Fact]
    public void Parse_malformed_json_returns_defaults()
        => Assert.Equal(new ExportLayout(), ExportLayout.Parse("{not json"));
}
