using Recordum.Core;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeAttachmentService : IAttachmentService
{
    public Dictionary<Guid, Attachment> Store { get; } = [];
    public List<(Guid NoteId, string SourceFilePath)> AddCalls { get; } = [];
    public List<Guid> DeleteCalls { get; } = [];

    /// <summary>When set, AddAsync throws this for a source path that matches (case-insensitive),
    /// so tests can simulate one bad file in a multi-file batch.</summary>
    public Dictionary<string, Exception> ThrowOnAdd { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When set, AddAsync awaits this before completing, so tests can control the exact
    /// interleaving of a batch add against other state changes (e.g. the selection moving on).</summary>
    public TaskCompletionSource<bool>? AddGate { get; set; }

    public async Task<Attachment> AddAsync(Guid noteId, string sourceFilePath, CancellationToken ct)
    {
        AddCalls.Add((noteId, sourceFilePath));
        if (ThrowOnAdd.TryGetValue(sourceFilePath, out var ex)) throw ex;
        if (AddGate is not null) await AddGate.Task.ConfigureAwait(true);

        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            Filename = Path.GetFileName(sourceFilePath),
            MimeType = "image/jpeg",
            FileSizeBytes = 100,
        };
        Store[attachment.Id] = attachment;
        return attachment;
    }

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

    public string ResolveAbsolutePath(Attachment attachment) => Path.Combine("C:\\fake-root", ResolveRelativePath(attachment));
}
