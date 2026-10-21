namespace Recordum.Core.Enums;

/// <summary>How a chosen photo is stored (D10). Absent or unknown setting means <see cref="ResizeAndClean"/>.</summary>
public enum PhotoImportMode
{
    /// <summary>Long side at most 2048 px, metadata (EXIF, GPS) dropped.</summary>
    ResizeAndClean,
    /// <summary>Original size, metadata dropped.</summary>
    CleanOnly,
    /// <summary>The file's bytes as they are; only the thumbnail is clean.</summary>
    KeepOriginal,
}

public static class PhotoImportSettings
{
    public const string Key = "photos.import_mode";
}
