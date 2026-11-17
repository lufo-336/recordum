using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Core.Text;

namespace Recordum.Data.Services;

internal static class ScaleAnchorTags
{
    /// <summary>Crea gli scaglioni di <paramref name="scaleId"/> (li aggiunge al contesto, non salva) e trova-o-crea
    /// il tag ricavato dal nome di ciascuno.</summary>
    internal static async ValueTask<IReadOnlyList<ScaleAnchor>> BuildAsync(
        RecordumDbContext db, Guid scaleId, IEnumerable<ScaleAnchorDraft> drafts, CancellationToken ct)
    {
        var tags = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ScaleAnchor>();
        foreach (var d in drafts)
        {
            var label = string.IsNullOrWhiteSpace(d.Label) ? null : d.Label.Trim();
            Guid? tagId = null;
            if (TagNames.FromLabel(label) is { } name)
            {
                if (!tags.TryGetValue(name, out var id))
                {
                    id = await db.Tags.Where(t => t.Name == name).Select(t => t.Id).FirstOrDefaultAsync(ct);
                    if (id == Guid.Empty)
                    {
                        id = Guid.NewGuid();
                        db.Tags.Add(new Tag { Id = id, Name = name });
                    }
                    tags[name] = id;
                }
                tagId = id;
            }
            var anchor = new ScaleAnchor { Id = Guid.NewGuid(), ScaleTemplateId = scaleId, Value = d.Value, Label = label, TagId = tagId };
            db.ScaleAnchors.Add(anchor);
            result.Add(anchor);
        }
        return result;
    }
}
