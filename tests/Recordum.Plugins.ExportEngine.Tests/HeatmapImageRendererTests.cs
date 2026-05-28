using Recordum.Plugins.Contracts.Export;

namespace Recordum.Plugins.ExportEngine.Tests;

public sealed class HeatmapImageRendererTests
{
    [Fact]
    public void Render_returns_a_png_byte_array()
    {
        var cells = new List<ExportHeatmapCell> { new(0, 0, 1), new(10, 3, 4), new(52, 6, 2) };
        var png = HeatmapImageRenderer.RenderPng(cells);
        Assert.NotEmpty(png);
        Assert.Equal(0x89, png[0]);
        Assert.Equal(0x50, png[1]);
        Assert.Equal(0x4E, png[2]);
        Assert.Equal(0x47, png[3]);
    }

    [Fact]
    public void Render_empty_cells_still_returns_a_png()
        => Assert.NotEmpty(HeatmapImageRenderer.RenderPng([]));
}
