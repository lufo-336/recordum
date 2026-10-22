using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using SkiaSharp;

namespace Recordum.Data.Services;

/// <summary>Photo import (D10). Decoding by <see cref="SKCodec.EncodedFormat"/>, never by extension. Skia's encoders
/// write no EXIF, so re-encoding is what strips GPS and the rest; the EXIF orientation is applied to the pixels first.</summary>
public sealed class SkiaPhotoImporter(IAppSettingService settings) : IPhotoImporter
{
    internal const long MaxBytes = 50L * 1024 * 1024;
    private const int MaxSide = 2048;
    private const int ThumbSide = 320;
    private const int JpegQuality = 85;
    // Decompression-bomb cap: a tiny file can declare gigapixels. Checked on the header, before any allocation.
    private const long MaxPixels = 100_000_000;
    private const int MaxDimension = 16384;

    public async Task<ImportedPhoto> ImportAsync(string sourcePath, CancellationToken ct)
    {
        var filename = Path.GetFileName(sourcePath);
        // Unknown or numeric-out-of-range values fall back to the default (Enum.TryParse accepts "99").
        var raw = await settings.GetEnumAsync<PhotoImportMode>(PhotoImportSettings.Key, ct);
        var mode = raw is { } m && Enum.IsDefined(m) ? m : PhotoImportMode.ResizeAndClean;
        return await Task.Run(() => Import(sourcePath, filename, mode), ct);
    }

    public byte[]? TryMakeThumbnail(byte[] content)
    {
        try
        {
            using var bmp = Decode(content, "", out var format);
            return FitAndEncode(bmp, ThumbSide, format == SKEncodedImageFormat.Png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg);
        }
        // Contract: null on any failure with these bytes (called at startup on legacy files, must never abort it).
        catch (Exception e) when (e is not OperationCanceledException) { return null; }
    }

    private static ImportedPhoto Import(string path, string filename, PhotoImportMode mode)
    {
        byte[] bytes;
        try
        {
            if (new FileInfo(path).Length > MaxBytes) throw new PhotoImportException(filename, PhotoImportFailure.TooLarge);
            bytes = File.ReadAllBytes(path);   // read-only; nothing is written back
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new PhotoImportException(filename, PhotoImportFailure.Unreadable);
        }

        using var oriented = Decode(bytes, filename, out var format);
        var isPng = format == SKEncodedImageFormat.Png;
        var outFormat = isPng ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        var thumb = FitAndEncode(oriented, ThumbSide, outFormat);

        if (mode == PhotoImportMode.KeepOriginal)
        {
            var mime = format switch
            {
                SKEncodedImageFormat.Png => "image/png",
                SKEncodedImageFormat.Webp => "image/webp",
                _ => "image/jpeg",
            };
            return new ImportedPhoto(Guid.NewGuid(), filename, mime, bytes, thumb);   // original name: the bytes are the original
        }

        var content = FitAndEncode(oriented, mode == PhotoImportMode.ResizeAndClean ? MaxSide : int.MaxValue, outFormat);
        // The bytes were re-encoded: the extension must say what they are (export links derive it from the name).
        var name = Path.ChangeExtension(filename, isPng ? ".png" : ".jpg");
        return new ImportedPhoto(Guid.NewGuid(), name, isPng ? "image/png" : "image/jpeg", content, thumb);
    }

    /// <summary>Decodes to a bitmap with the EXIF orientation applied. Throws Unsupported / Unreadable.</summary>
    private static SKBitmap Decode(byte[] bytes, string filename, out SKEncodedImageFormat format)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            throw new PhotoImportException(filename, PhotoImportFailure.Unsupported);
        format = codec.EncodedFormat;
        var (cw, ch) = (codec.Info.Width, codec.Info.Height);
        if (cw <= 0 || ch <= 0 || cw > MaxDimension || ch > MaxDimension || (long)cw * ch > MaxPixels)
            throw new PhotoImportException(filename, PhotoImportFailure.TooManyPixels);

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bmp = new SKBitmap(info);
        // Success only: a truncated file reports IncompleteInput and must not become a half-grey photo.
        if (codec.GetPixels(info, bmp.GetPixels()) != SKCodecResult.Success)
        {
            bmp.Dispose();
            throw new PhotoImportException(filename, PhotoImportFailure.Unreadable);
        }
        var rotated = ApplyOrigin(bmp, codec.EncodedOrigin);
        if (!ReferenceEquals(rotated, bmp)) bmp.Dispose();
        return rotated;
    }

    private static SKBitmap ApplyOrigin(SKBitmap src, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default) return src;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int w = src.Width, h = src.Height;
        var dst = new SKBitmap(swap ? h : w, swap ? w : h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var canvas = new SKCanvas(dst);
        canvas.SetMatrix(origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            _ /* LeftBottom */ => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        });
        canvas.DrawBitmap(src, 0, 0);
        return dst;
    }

    /// <summary>Scales down so the long side is at most <paramref name="maxSide"/>; never enlarges. Returns the input when it already fits.</summary>
    private static SKBitmap Fit(SKBitmap src, int maxSide)
    {
        var longSide = Math.Max(src.Width, src.Height);
        if (longSide <= maxSide) return src;
        var k = (double)maxSide / longSide;
        var info = new SKImageInfo(Math.Max(1, (int)Math.Round(src.Width * k)), Math.Max(1, (int)Math.Round(src.Height * k)),
            SKColorType.Rgba8888, SKAlphaType.Unpremul);
        return src.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
               ?? throw new InvalidOperationException("Skia could not resize the bitmap.");
    }

    private static byte[] FitAndEncode(SKBitmap src, int maxSide, SKEncodedImageFormat format)
    {
        var fitted = Fit(src, maxSide);
        try { return Encode(fitted, format); }
        finally { if (!ReferenceEquals(fitted, src)) fitted.Dispose(); }
    }

    private static byte[] Encode(SKBitmap bmp, SKEncodedImageFormat format)
    {
        // JPEG has no alpha: flatten onto white, or transparent pixels turn black.
        using var flat = format == SKEncodedImageFormat.Jpeg ? FlattenOnWhite(bmp) : null;
        using var img = SKImage.FromBitmap(flat ?? bmp);
        using var data = img.Encode(format, format == SKEncodedImageFormat.Png ? 100 : JpegQuality);
        return data.ToArray();
    }

    private static SKBitmap FlattenOnWhite(SKBitmap src)
    {
        var dst = new SKBitmap(src.Width, src.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(dst);
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(src, 0, 0);
        return dst;
    }
}
