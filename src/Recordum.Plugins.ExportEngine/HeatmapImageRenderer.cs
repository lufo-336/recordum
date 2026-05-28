using Recordum.Plugins.Contracts.Export;
using SkiaSharp;

namespace Recordum.Plugins.ExportEngine;

/// <summary>Headless SkiaSharp renderer of the 53x7 year heatmap to a PNG. 5-step GitHub light ramp.</summary>
public static class HeatmapImageRenderer
{
    private const int Cols = 53, Rows = 7, Cell = 11, Gap = 2, Pad = 6;

    private static readonly SKColor[] Ramp =
    [
        SKColor.Parse("#EBEDF0"), SKColor.Parse("#9BE9A8"), SKColor.Parse("#40C463"),
        SKColor.Parse("#30A14E"), SKColor.Parse("#216E39"),
    ];

    public static byte[] RenderPng(IReadOnlyList<ExportHeatmapCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        int w = Pad * 2 + Cols * (Cell + Gap);
        int h = Pad * 2 + Rows * (Cell + Gap);

        using var bmp = new SKBitmap(w, h);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < Rows; r++)
                    DrawCell(canvas, paint, c, r, Ramp[0]);
            foreach (var cell in cells)
            {
                if (cell.Week is < 0 or >= Cols || cell.Weekday is < 0 or >= Rows) continue;
                var step = Math.Clamp(cell.Step, 0, Ramp.Length - 1);
                DrawCell(canvas, paint, cell.Week, cell.Weekday, Ramp[step]);
            }
        }
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawCell(SKCanvas canvas, SKPaint paint, int col, int row, SKColor color)
    {
        paint.Color = color;
        float x = Pad + col * (Cell + Gap);
        float y = Pad + row * (Cell + Gap);
        canvas.DrawRect(x, y, Cell, Cell, paint);
    }
}
