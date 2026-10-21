using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Text;

namespace Recordum.Data.Services;

/// <summary>Allinea le righe <c>note_tag</c> di una nota al suo testo e alla riga Tag, sul contesto del
/// chiamante (stessa transazione del suo <c>SaveChanges</c>). Crea i tag che mancano.</summary>
public static class NoteTagWriter
{
    public static async Task SyncAsync(
        RecordumDbContext db, Guid noteId, string text, IReadOnlyCollection<string>? propertyTags, CancellationToken ct)
    {
        // In memoria e senza badare alle maiuscole: più largo di NOCASE di SQLite, mai un doppione sull'indice unico.
        var tags = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in await db.Tags.ToListAsync(ct)) tags.TryAdd(t.Name, t);

        Guid TagId(string name)
        {
            if (tags.TryGetValue(name, out var t)) return t.Id;
            t = new Tag { Id = Guid.NewGuid(), Name = name };
            db.Tags.Add(t);
            tags[name] = t;
            return t.Id;
        }

        var existing = await db.NoteTags.Where(nt => nt.NoteId == noteId).ToListAsync(ct);

        void Apply(NoteTagSource source, IEnumerable<string> names)
        {
            var wanted = names.Select(TagId).ToHashSet();
            foreach (var row in existing.Where(r => r.Source == source && !wanted.Contains(r.TagId)))
                db.NoteTags.Remove(row);
            var have = existing.Where(r => r.Source == source).Select(r => r.TagId).ToHashSet();
            foreach (var id in wanted.Where(id => !have.Contains(id)))
                db.NoteTags.Add(new NoteTag { NoteId = noteId, TagId = id, Source = source });
        }

        Apply(NoteTagSource.Inline, InlineTags.Parse(text));
        if (propertyTags is not null)
            Apply(NoteTagSource.Property, propertyTags
                .Select(n => n.Trim())
                .Where(n => n.Length is > 0 and <= InlineTags.MaxLength));
    }
}
