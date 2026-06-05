namespace Recordum.Core.Abstractions.Services.Dtos;

/// <summary>A single saved version of a note, newest-first in history listings.</summary>
public sealed record NoteVersion(Guid Id, string BodyMd, DateTime CreatedAt);
