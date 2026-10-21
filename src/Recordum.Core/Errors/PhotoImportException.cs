namespace Recordum.Core.Errors;

public enum PhotoImportFailure { Unsupported, TooLarge, Unreadable }

/// <summary>A chosen photo could not be imported; carries the file name (name only) and why.</summary>
public sealed class PhotoImportException(string filename, PhotoImportFailure reason)
    : RecordumDomainException($"Cannot import photo '{filename}': {reason}")
{
    public string Filename { get; } = filename;
    public PhotoImportFailure Reason { get; } = reason;
}
