using System.Security.Cryptography;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures.Photos;
using SkiaSharp;
using Xunit;

namespace Recordum.Data.Tests.Services;

public sealed class SkiaPhotoImporterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "recordum-photo-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string> _settings = new();

    public SkiaPhotoImporterTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class Settings(Dictionary<string, string> store) : IAppSettingService
    {
        public ValueTask<string?> GetAsync(string key, CancellationToken ct = default) => new(store.GetValueOrDefault(key));
        public ValueTask SetAsync(string key, string value, CancellationToken ct = default) { store[key] = value; return default; }
        public async ValueTask<T?> GetEnumAsync<T>(string key, CancellationToken ct = default) where T : struct, Enum =>
            await GetAsync(key, ct) is { } raw && Enum.TryParse<T>(raw, true, out var v) ? v : null;
        public ValueTask SetEnumAsync<T>(string key, T value, CancellationToken ct = default) where T : struct, Enum => SetAsync(key, value.ToString(), ct);
    }

    private SkiaPhotoImporter Sut() => new(new Settings(_settings));

    private string Write(string name, byte[] bytes)
    {
        var p = Path.Combine(_dir, name);
        File.WriteAllBytes(p, bytes);
        return p;
    }

    private string BigExifJpeg() => Write("big.jpg", PhotoFixtures.WithExif(PhotoFixtures.Encode(4000, 3000, SKEncodedImageFormat.Jpeg), 6));

    private static (int W, int H) Size(byte[] b) { using var bmp = SKBitmap.Decode(b); return (bmp.Width, bmp.Height); }

    [Fact]
    public async Task Default_mode_resizes_applies_orientation_and_strips_exif()
    {
        var src = BigExifJpeg();
        PhotoFixtures.HasExifSegment(File.ReadAllBytes(src)).Should().BeTrue("the fixture must carry EXIF");
        // Evidence that the hand-made EXIF is valid: Skia reads orientation 6 (full size is 4000x3000 stored).
        using (var codec = SKCodec.Create(src)) codec.EncodedOrigin.Should().Be(SKEncodedOrigin.RightTop);

        var photo = await Sut().ImportAsync(src, default);

        var (w, h) = Size(photo.Content);
        h.Should().Be(2048);
        w.Should().BeLessThan(h);
        photo.MimeType.Should().Be("image/jpeg");
        photo.Filename.Should().Be("big.jpg");
        PhotoFixtures.HasExifSegment(photo.Content).Should().BeFalse();
        // The red marker was top-left; rotated 90 degrees clockwise it lands top-right.
        using var bmp = SKBitmap.Decode(photo.Content);
        bmp.GetPixel(bmp.Width - 5, 5).Red.Should().BeGreaterThan(200);
        bmp.GetPixel(5, 5).Red.Should().BeLessThan(100);
    }

    [Fact]
    public async Task CleanOnly_keeps_size_rotated_without_exif()
    {
        _settings[PhotoImportSettings.Key] = nameof(PhotoImportMode.CleanOnly);
        var photo = await Sut().ImportAsync(BigExifJpeg(), default);
        Size(photo.Content).Should().Be((3000, 4000));
        PhotoFixtures.HasExifSegment(photo.Content).Should().BeFalse();
    }

    [Fact]
    public async Task KeepOriginal_keeps_bytes_and_cleans_the_thumbnail()
    {
        _settings[PhotoImportSettings.Key] = nameof(PhotoImportMode.KeepOriginal);
        var src = BigExifJpeg();
        var photo = await Sut().ImportAsync(src, default);
        photo.Content.Should().Equal(File.ReadAllBytes(src));
        PhotoFixtures.HasExifSegment(photo.Thumbnail).Should().BeFalse();
        Size(photo.Thumbnail).Should().Be((240, 320));
    }

    [Fact]
    public async Task Small_png_stays_png_and_is_not_enlarged()
    {
        var photo = await Sut().ImportAsync(Write("a.png", PhotoFixtures.Encode(100, 60, SKEncodedImageFormat.Png)), default);
        photo.MimeType.Should().Be("image/png");
        Size(photo.Content).Should().Be((100, 60));
        SKCodec.Create(new SKMemoryStream(photo.Content)).EncodedFormat.Should().Be(SKEncodedImageFormat.Png);
        Size(photo.Thumbnail).Should().Be((100, 60));
    }

    [Fact]
    public async Task Thumbnail_long_side_is_320()
    {
        var photo = await Sut().ImportAsync(BigExifJpeg(), default);
        var (w, h) = Size(photo.Thumbnail);
        Math.Max(w, h).Should().Be(320);
        PhotoFixtures.HasExifSegment(photo.Thumbnail).Should().BeFalse();
        var again = Sut().TryMakeThumbnail(photo.Content);
        Size(again!).Should().Be((w, h));
        Sut().TryMakeThumbnail([1, 2, 3]).Should().BeNull();
    }

    [Theory]
    [InlineData("heic")]
    [InlineData("text")]
    [InlineData("truncated")]
    [InlineData("huge")]
    public async Task Bad_files_throw_with_the_reason_and_the_filename(string kind)
    {
        string path; PhotoImportFailure expected;
        switch (kind)
        {
            case "heic": path = Write("p.heic", PhotoFixtures.Heic()); expected = PhotoImportFailure.Unsupported; break;
            case "text": path = Write("fake.jpg", "hello, not a picture"u8.ToArray()); expected = PhotoImportFailure.Unsupported; break;
            case "truncated":
                var full = PhotoFixtures.Encode(800, 600, SKEncodedImageFormat.Jpeg);
                path = Write("cut.jpg", full[..(full.Length / 2)]); expected = PhotoImportFailure.Unreadable; break;
            default:
                path = Path.Combine(_dir, "huge.jpg");
                using (var fs = new FileStream(path, FileMode.Create)) fs.SetLength(51L * 1024 * 1024);   // sparse
                expected = PhotoImportFailure.TooLarge; break;
        }

        var ex = (await Assert.ThrowsAsync<PhotoImportException>(() => Sut().ImportAsync(path, default)));
        ex.Reason.Should().Be(expected);
        ex.Filename.Should().Be(Path.GetFileName(path));
    }

    [Fact]
    public async Task Source_file_is_untouched()
    {
        var src = BigExifJpeg();
        var before = SHA256.HashData(File.ReadAllBytes(src));
        await Sut().ImportAsync(src, default);
        SHA256.HashData(File.ReadAllBytes(src)).Should().Equal(before);
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("99")]
    public async Task Unknown_setting_value_behaves_as_default(string value)
    {
        _settings[PhotoImportSettings.Key] = value;
        var photo = await Sut().ImportAsync(BigExifJpeg(), default);
        Size(photo.Content).Should().Be((1536, 2048));
    }
}
