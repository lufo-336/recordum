namespace Recordum.Core.Enums;

/// <summary>How a note is classified. Diario = a journal diary entry (paired 1:1 with a Completion);
/// Veloce = a free quick note with no completion. Foundation for richer note types via templates (Future).</summary>
public enum NoteCategory
{
    Diario = 0,
    Veloce = 1,
}
