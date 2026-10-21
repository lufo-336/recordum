using SkiaSharp;

namespace Recordum.Data.Tests.Fixtures.Photos;

/// <summary>Builds photo fixtures in memory; see README.md in this folder.</summary>
internal static class PhotoFixtures
{
    public static byte[] Encode(int w, int h, SKEncodedImageFormat format)
    {
        using var bmp = new SKBitmap(w, h);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(SKColors.Blue);
            using var red = new SKPaint { Color = SKColors.Red };
            c.DrawRect(0, 0, w / 4f, h / 4f, red);   // top-left marker: orientation can be read from pixels
        }
        using var img = SKImage.FromBitmap(bmp);
        return img.Encode(format, 90).ToArray();
    }

    /// <summary>Inserts an APP1 Exif segment (Orientation + a GPS IFD with latitude) after SOI.</summary>
    public static byte[] WithExif(byte[] jpeg, ushort orientation)
    {
        var t = new List<byte>();
        void U16(int v) { t.Add((byte)v); t.Add((byte)(v >> 8)); }
        void U32(int v) { U16(v & 0xFFFF); U16(v >>> 16); }
        t.AddRange("II"u8.ToArray()); U16(42); U32(8);
        // IFD0 at 8: 2 entries -> 2 + 24 + 4 = 30 bytes, GPS IFD at 38
        U16(2);
        U16(0x0112); U16(3); U32(1); U16(orientation); U16(0);
        U16(0x8825); U16(4); U32(1); U32(38);
        U32(0);
        // GPS IFD at 38: 3 entries -> 2 + 24 + 4 = 30 bytes, rationals at 68
        U16(2);
        U16(1); U16(2); U32(2); t.AddRange("N\0\0\0"u8.ToArray());      // GPSLatitudeRef = "N"
        U16(2); U16(5); U32(3); U32(68);                                // GPSLatitude -> 3 rationals
        U32(0);
        foreach (var (n, d) in new[] { (45, 1), (4, 1), (0, 1) }) { U32(n); U32(d); }

        var payload = new List<byte>("Exif\0\0"u8.ToArray());
        payload.AddRange(t);
        var seg = new List<byte> { 0xFF, 0xE1, (byte)((payload.Count + 2) >> 8), (byte)(payload.Count + 2) };
        seg.AddRange(payload);
        return [jpeg[0], jpeg[1], .. seg, .. jpeg[2..]];
    }

    public static bool HasExifSegment(byte[] bytes) =>
        bytes.AsSpan().IndexOf("Exif\0\0"u8) >= 0;

    public static byte[] Heic() => [0, 0, 0, 24, .. "ftypheic"u8.ToArray(), .. new byte[64]];

    /// <summary>A PNG whose header declares w x h but which holds no real pixel data (never decoded in full).</summary>
    public static byte[] PngDeclaring(int w, int h)
    {
        var o = new List<byte> { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };
        void Chunk(string type, byte[] body)
        {
            var tb = System.Text.Encoding.ASCII.GetBytes(type).Concat(body).ToArray();
            o.AddRange(BitConverter.GetBytes(body.Length).Reverse());
            o.AddRange(tb);
            uint crc = 0xFFFFFFFF;
            foreach (var b in tb) { crc ^= b; for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1; }
            o.AddRange(BitConverter.GetBytes(~crc).Reverse());
        }
        Chunk("IHDR", [.. BitConverter.GetBytes(w).Reverse(), .. BitConverter.GetBytes(h).Reverse(), 8, 6, 0, 0, 0]);
        Chunk("IDAT", [0x78, 0x9C, 0, 0, 0, 0, 0, 0]);
        return [.. o];
    }
}
