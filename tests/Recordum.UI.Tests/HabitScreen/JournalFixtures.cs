using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Recordum.Core.Abstractions;
using Recordum.Presentation.Services;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>Un orologio finto in un fuso diverso da UTC (+02:00): prova che l'ora delle voci e' quella locale.</summary>
internal sealed class ZonedClock : IClock
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("test+2", TimeSpan.FromHours(2), "test", "test");
    public DateTimeOffset Now { get; set; } = new(2026, 3, 15, 11, 42, 0, TimeSpan.Zero);
    public DateTimeOffset UtcNow => Now;
    public long UtcNowMs => Now.ToUnixTimeMilliseconds();
    public TimeZoneInfo LocalTimeZone => Zone;
}

/// <summary>La finestra "Aggiungi foto": risponde con i percorsi che il test le ha dato.</summary>
internal sealed class FakeOpenFileDialog : IOpenFileDialog
{
    public string[] Paths { get; set; } = [];
    public Task<string?> PromptAsync(string filter) => Task.FromResult(Paths.FirstOrDefault());
    public Task<string[]> PromptMultipleAsync(string filter) => Task.FromResult(Paths);
}

internal static class JournalFixtures
{
    /// <summary>Un PNG vero (rosso) scritto nella cartella temporanea del test.</summary>
    public static string WritePng(int w = 64, int h = 48)
    {
        var path = Path.Combine(Path.GetTempPath(), $"recordum-ui-photo-{Guid.NewGuid():N}.png");
        using var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var fb = bmp.Lock())
        {
            var px = new byte[fb.RowBytes * h];
            for (var i = 0; i < px.Length; i += 4) { px[i] = 0x30; px[i + 1] = 0x30; px[i + 2] = 0xE0; px[i + 3] = 0xFF; }
            System.Runtime.InteropServices.Marshal.Copy(px, 0, fb.Address, px.Length);
        }
        #pragma warning disable CS0618 // l'overload con BitmapEncoderOptions non esiste in 12.1.2
        bmp.Save(path);
#pragma warning restore CS0618
        return path;
    }
}
