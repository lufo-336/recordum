using Recordum.Core;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeAttachmentService : IAttachmentService
{
    public Dictionary<Guid, Attachment> Store { get; } = [];
    public List<(Guid NoteId, string Filename)> AddCalls { get; } = [];
    public List<Guid> DeleteCalls { get; } = [];

    /// <summary>Stored photos by attachment id, for the content/thumbnail reads.</summary>
    public Dictionary<Guid, ImportedPhoto> Photos { get; } = [];

    /// <summary>When set, AddAsync awaits this before completing, so tests can control the exact
    /// interleaving of a batch add against other state changes (e.g. the selection moving on).</summary>
    public TaskCompletionSource<bool>? AddGate { get; set; }

    public async Task<Attachment> AddAsync(Guid noteId, ImportedPhoto photo, CancellationToken ct)
    {
        AddCalls.Add((noteId, photo.Filename));
        if (AddGate is not null) await AddGate.Task.ConfigureAwait(true);

        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            Filename = photo.Filename,
            MimeType = photo.MimeType,
            FileSizeBytes = photo.Content.Length,
            HasContent = photo.Content.Length > 0,
        };
        Store[attachment.Id] = attachment;
        Photos[attachment.Id] = photo;
        return attachment;
    }

    public Task<byte[]?> ReadContentAsync(Guid attachmentId, CancellationToken ct) =>
        Task.FromResult(Photos.TryGetValue(attachmentId, out var p) ? p.Content : null);

    public Task<byte[]?> ReadThumbnailAsync(Guid attachmentId, CancellationToken ct) =>
        Task.FromResult(Photos.TryGetValue(attachmentId, out var p) ? p.Thumbnail : null);

    public Task DeleteAsync(Guid attachmentId, CancellationToken ct)
    {
        DeleteCalls.Add(attachmentId);
        Store.Remove(attachmentId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Attachment>> ListByNoteAsync(Guid noteId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Attachment>>(Store.Values.Where(a => a.NoteId == noteId).ToList());

    public Task<IReadOnlyList<Attachment>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Attachment>>(Store.Values.ToList());

    public string ResolveRelativePath(Attachment attachment) => $"attachments/{attachment.NoteId}/{attachment.Id:N}.bin";
}
