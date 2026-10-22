namespace Recordum.Core.Models;

/// <summary>A photo already normalized in memory, waiting to be stored as an attachment.</summary>
public sealed record ImportedPhoto(Guid Id, string Filename, string MimeType, byte[] Content, byte[] Thumbnail);
